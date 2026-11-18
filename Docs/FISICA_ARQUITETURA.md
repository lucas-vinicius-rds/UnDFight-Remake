# Arquitetura de Física do UnDFight Remake (Fase 15.1 — Física de Cenário v2)

Este documento descreve a arquitetura híbrida de física do **UnDFight Remake**, separando a **simulação determinística de combate em 60 Hz** da **física de cenário em tempo real no NVIDIA PhysX**, garantindo precisão de jogo de luta competitivo aliada à riqueza visual e reatividade ambiental.

---

## 1. Por que Arquitetura Híbrida?

Em jogos de luta 2.5D competitivos (*Mortal Kombat*, *Street Fighter*, *Tekken*), o combate exige:
- **Determinismo estrito em 60 Hz** (`1 frame de animação = 1 tick de simulação = 16,667 ms`, independente do FPS de renderização).
- **Plano de combate travado em `Z = 0,000 m`**, *frame data* exato (`startup / active / recovery`), *hit-stop* congelado e *pushboxes* previsíveis sem *jitter* de resolvedor de colisões.

Por outro lado, a interatividade do cenário (caixas/mesas quebráveis, cadeiras e garrafas arremessáveis, destroços que permanecem na arena, latas chutadas pelos lutadores, placas suspensas por dobradiças, rajadas de vento e gotas de chuva 3D colidindo no chão) se beneficia de **dinâmica de corpos rígidos 3D real** fornecida pelo **NVIDIA PhysX**.

A solução adotada é uma **arquitetura híbrida rigorosamente isolada**:
- **Simulação de Combate (`FightSimulation`, `FighterController`)**: clock fixo de 60 Hz (`FixedTickClock`), hitboxes/hurtboxes XY determinísticas e integração cinemática própria em `Z = 0`. **Nenhum lutador jamais recebe força ou empurrão vindo do NVIDIA PhysX.**
- **Física de Cenário (`StageInteractableManager`, `StageWindController`, `StageWeatherController`)**: NVIDIA PhysX completo em 3D, isolado por **5 layers dedicadas** e acoplado de forma **estritamente unidirecional** (Lutador → Cenário).

---

## 2. As 5 Layers PhysX e Matriz de Colisão

Definidas e aplicadas centralmente por [`StagePhysicsLayers.cs`](../Assets/UnDFight/Scripts/Stage/StagePhysicsLayers.cs) e gravadas em `ProjectSettings/TagManager.asset` / `DynamicsManager.asset` via [`StagePhysicsSetupEditor.cs`](../Assets/UnDFight/Scripts/Editor/StagePhysicsSetupEditor.cs):

| Slot | Nome da Layer | Função | Colide com | Ignora |
|---:|---|---|---|---|
| **8** | `StagePhysics` | Chão invisível e paredes laterais da arena alinhados ao `FightSimulation` | `StageDebris` (9), `StageProjectile` (11) | `Default`, `FighterHurtbox`, `FighterDebrisPusher`, `StagePhysics` |
| **9** | `StageDebris` | Destroços persistentes (`StageDebrisFragment`), props soltos (`StageDynamicProp`) e corpos pendurados (`StageHangingProp`) | `StagePhysics` (8), `StageDebris` (9), `FighterDebrisPusher` (12) | `Default`, `FighterHurtbox` (10), `StageProjectile` (11) |
| **10** | `FighterHurtbox` | `CapsuleCollider` trigger + `Rigidbody` cinemático na raiz de cada lutador (`Fighter_*.prefab`) | **Apenas** `StageProjectile` (11) via `OnTriggerEnter` | `Default`, `StagePhysics`, `StageDebris`, `FighterHurtbox`, `FighterDebrisPusher` |
| **11** | `StageProjectile` | Objetos arremessáveis em voo (`StageThrowableProp`, ex.: cadeira, garrafa) | `StagePhysics` (8) e `FighterHurtbox` (10) | `Default`, `StageDebris` (9), `StageProjectile` (11), `FighterDebrisPusher` (12) |
| **12** | `FighterDebrisPusher` | Empurrador cinemático filho em cada lutador (`FighterDebrisPusher.cs`) | **Exclusivamente** `StageDebris` (9) | `Default`, `StagePhysics` (8), `FighterHurtbox` (10), `StageProjectile` (11), `FighterDebrisPusher` (12) |

### Matriz de Colisão Resumida (`✓` colide / `—` ignorado)

| Layer | `Default (0)` | `StagePhysics (8)` | `StageDebris (9)` | `FighterHurtbox (10)` | `StageProjectile (11)` | `FighterDebrisPusher (12)` |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **`StagePhysics (8)`** | — | — | ✓ | — | ✓ | — |
| **`StageDebris (9)`** | — | ✓ | ✓ | — | — | ✓ |
| **`FighterHurtbox (10)`** | — | — | — | — | ✓ | — |
| **`StageProjectile (11)`** | — | ✓ | — | ✓ | — | — |
| **`FighterDebrisPusher (12)`** | — | — | ✓ | — | — | — |

---

## 3. Acoplamento Unidirecional Cinemático → Dinâmico (`FighterDebrisPusher`)

Para permitir que lutadores chutem latas, espalhem cacos de vidro e empurrem destroços ao caminhar, dar *dash* ou golpear **sem violar o determinismo de 60 Hz**:

1. Cada lutador possui um filho dedicado `FighterDebrisPusher` na layer **12** com:
   - `Rigidbody` com `isKinematic = true`, `useGravity = false`, `interpolation = None`, `collisionDetectionMode = ContinuousSpeculative`.
   - `CapsuleCollider` sólido (`isTrigger = false`) cujas dimensões acompanham em tempo real o estado do `FighterController` (em pé, agachado e caído em `KnockDown`).
2. Em `FixedUpdate()`, o pusher se move via `Rigidbody.MovePosition` diretamente para `(SimPosition.x, SimPosition.y, 0)`. Como é um corpo **cinemático** (massa efetiva infinita no resolvedor do PhysX), ele transfere momento para corpos dinâmicos na layer `StageDebris`, mas **jamais sofre reação de volta**.
3. Em `OnCollisionEnter` / `OnCollisionStay`, o `FighterDebrisPusher` acorda corpos em *sleep* (`WakeUp()`) e aplica um impulso calibrado proporcional à velocidade horizontal do lutador (`SimVelocity.x`), amplificado durante `DashForward` (`1.65x`), `DashBackward` (`1.35x`) e estados de `Attack` (`1.50x`).

---

## 4. Persistência de Destroços com PhysX Auto-Sleep e Ondas de Choque

- **`StageDebrisFragment`** (`persistUntilReset = true` por padrão):
  - Ao quebrar caixas, mesas, cadeiras ou garrafas, os fragmentos permanecem **ativos e dinâmicos durante todo o round**, sem desaparecer após alguns segundos e sem virar cinemáticos.
  - A estabilidade de 60 FPS é garantida pelo **PhysX Auto-Sleep** nativo (`sleepThreshold = 0.08`, `linearDamping = 0.45`, `angularDamping = 0.65`): assim que um destroço assenta no chão, o PhysX suspende sua integração até que um lutador, uma rajada de vento ou uma onda de choque o acorde novamente.
  - Uma rede de segurança (`y < -5 m`) impede que qualquer objeto caia infinitamente caso escape da arena.
  - Todos os destroços, props soltos e placas retornam perfeitamente ao estado inicial em `MatchManager.StartRound()` (e pela tecla `R` no Modo Treino) via interface `IStageResettable`.
- **Ondas de Choque de Combate (`StageInteractableManager.ApplyStageShockwave`)**:
  - Golpes fortes, especiais e quedas de lutadores no chão (`KnockDown`) propagam um impulso radial físico com atenuação por distância para todos os `StageDebrisFragment`, `StageDynamicProp` e `StageHangingProp` dentro do raio — criando impacto tátil real no cenário sem tocar na simulação dos lutadores.

---

## 5. Vento Aerodinâmico Real (`StageWindController` + `StageWindAffectable`)

O vento da arena não usa acelerações arbitrárias: aplica a **equação real de arrasto aerodinâmico quadrático** sobre todo corpo dinâmico que possua [`StageWindAffectable.cs`](../Assets/UnDFight/Scripts/Stage/StageWindAffectable.cs):

$$\vec{v}_{\text{rel}} = \vec{v}_{\text{vento}}(t) - \vec{v}_{\text{corpo}}$$

$$\vec{F}_{\text{drag}} = \frac{1}{2}\,\rho\,C_d\,A\,|\vec{v}_{\text{rel}}|\,\vec{v}_{\text{rel}}$$

Onde:
- $\rho = 1,225\text{ kg/m}^3$ é a densidade padrão do ar ao nível do mar (`airDensity`).
- $C_d$ é o coeficiente de arrasto do objeto (`dragCoefficient`, ex.: `1.05` para latas/cacos, `1.25` para placas planas).
- $A$ é a área de seção transversal em $\text{m}^2$ (`crossSectionArea`, ex.: `0.09–0.36 m²`).
- $\vec{v}_{\text{vento}}(t)$ combina velocidade base e rajadas contínuas geradas por **Perlin Noise + senoide**, variando conforme o clima escolhido (`Day`: brisa leve; `Night`: vento moderado; `RainyNight`: rajadas fortes de tempestade).
- **Limiar de Rajada (`wakeGustThreshold`)**: corpos em *Auto-Sleep* só acordam quando $|\vec{v}_{\text{vento}}| \ge \text{wakeGustThreshold}$, evitando gasto de CPU e tremores em brisas fracas enquanto latas leves e placas balançam de forma expressiva em rajadas e tempestades.

---

## 6. Pendulares (`HingeJoint`) e Chuva 3D com Colisão Física

- **`StageHangingProp` (`HingeJoint`)**:
  - Utiliza uma âncora cinemática fixa no teto/poste ligada por `HingeJoint` real do PhysX (com mola de retorno `JointSpring`, amortecimento angular e limites `[-55°, +55°]`) a um corpo dinâmico na layer `StageDebris`. Reage tanto ao arrasto aerodinâmico do vento quanto às ondas de choque dos golpes.
- **Sistema Reutilizável de Clima (`StageWeatherVariant` + `StageWeatherController`)**:
  - Suporta `Day` (ex.: `PÔR DO SOL` na Favela), `Night` e `RainyNight` em qualquer arena com `StageData.supportsWeatherVariants = true`, com seleção direta na tela **SELECIONE A ARENA** (`A/D` ou `←/→` e botões `<`/`>`) e troca ao vivo pela tecla **`F6`** no Modo Treino.
  - Em `RainyNight`:
    - Um `ParticleSystem` de chuva 3D cobre toda a arena com a inclinação das gotas acoplada em tempo real ao vetor `StageWindController.CurrentWindVelocity`.
    - O módulo **Collision (World 3D)** testa colisão real das gotas contra `StagePhysics` (8) e `StageDebris` (9), disparando um **Sub Emitter** de respingo (*Splash*) exatamente no ponto de impacto.
    - O piso molhado é aplicado em runtime via `MaterialPropertyBlock` (`_Smoothness` elevado + leve escurecimento do albedo), preservando intactos os materiais `.mat` do projeto no disco.
    - Áudio procedural contínuo de chuva/vento (`ProceduralAudioFactory` + `FightAudioManager`) acompanha o clima respeitando o volume de SFX/Master em `SettingsManager`.
