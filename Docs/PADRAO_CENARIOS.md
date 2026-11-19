# Padrão Obrigatório de Cenários 2.5D — UnDFight Remake

Este documento define as regras técnicas e artísticas obrigatórias para **todas as arenas jogáveis** do projeto (`Arena_Favela`, futuras `Arena_UnDF`, `Arena_Cadeia`, etc.), garantindo enquadramento limpo sem bordas pretas, leitura imediata dos lutadores, 60 FPS estáveis e compatibilidade total com a **Física de Cenário v2** e o **Sistema de Clima**.

Os utilitários reutilizáveis estão implementados em `Assets/UnDFight/Scripts/Editor/StageFramingStandards.cs`.

---

## 1. Parâmetros Base da Câmera 2.5D (`FightCameraController`)
- Plano de combate determinístico: **`Z = 0.000 m`**, chão lógico **`Y = 0.000 m`**.
- Limites padrão de arena: **`stageMinX = -8.0 m`** até **`stageMaxX = +8.0 m`**.
- Zoom dinâmico: **`minDistance = 6.5 m`** até **`maxDistance = 12.0 m`**, **`heightOffset = 1.35 m`**, **`FOV ≈ 40°`**.
- Impact shake máximo: `±0.25 m`.

---

## 2. Cobertura Obrigatória de Chão Frontal (Anti "Barra Preta")
No zoom-out máximo (`CameraDistance = 12.0 m`, `camY = 1.35 m`, `FOV = 40°`), a borda inferior do frustum da câmera intercepta o plano `Y = 0` em `Z ≈ -8.3 m` (e ainda mais atrás sob shake vertical ou monitores ultrawide `21:9`).

**Regras obrigatórias:**
1. **Profundidade frontal mínima:** o chão visual frontal DEVE estender-se continuamente até pelo menos **`Z <= -22.0 m`** (`StageFramingStandards.MinForegroundFloorZ = -22.0f`).
2. **Largura horizontal mínima:** todas as faixas principais de chão (faixa frontal, faixa de combate e acostamento) DEVEM ter largura mínima **`>= 64.0 m`** (`StageFramingStandards.MinFloorWidthX = 64.0f`, cobrindo `X ∈ [-32, +32]`).
3. **Emenda contígua sem Z-fighting:** utilize `StageFramingStandards.EnsureForegroundFloorCoverage(floorTransform, backEdgeZ, targetFrontZ, minWidthX)` para estender a faixa frontal mantendo a emenda traseira travada exatamente em `backEdgeZ` (sem sobreposição vertical).
4. **Superfície molhada uniforme (`RainyNight`):** inclua TODAS as faixas de chão frontal, pista de combate, calçadas e extensões laterais em `StageWeatherController.wetSurfaceRenderers`.
5. **Teste automatizado obrigatório:** toda arena deve passar em `StageFramingStandards.ValidateCameraBottomFloorCoverage(mainCam, camCtrl, combinedFloorBounds)`, que testa analiticamente os 3 raios inferiores (esquerda, centro, direita) em `16:9` e `21:9`, no centro e extremos da câmera, com margem de segurança.

---

## 3. Fechamento Lateral e Composição em 4 Camadas (Sem Buracos de Horizonte)
Para evitar vazios visuais quando a câmera alcança `X = ±8 m` ou durante o zoom do Super Flash:

1. **Fechamento lateral (`MinSideBackdropExtentX = 22.0f`):**
   - Arquitetura, muros, fachadas, calçadas e postes devem fechar continuamente além de **`X = -18 m`** e **`X = +18 m`** (recomendado até `±22 m`).
2. **Arquitetura em 4 camadas de profundidade:**
   - **Camada A — Piso e Faixa de Combate (`Z ∈ [-22.5, +4.0]`):** faixa `Z = 0` sempre plana, limpa e de alto contraste para leitura das silhuetas dos lutadores.
   - **Camada B — Props Interativos PhysX v2 (`Z ∈ [-0.5, +2.5]`):** quebráveis, arremessáveis, latas/cones dinâmicos (`StageDynamicProp`) e pendulares `HingeJoint` (`StageHangingProp`) posicionados nas bordas/faixa traseira ou acima da cabeça, sem obstruir o centro do combate.
   - **Camada C — Midground Arquitetural (`Z ∈ [+2.5, +10.0]`):** fachadas principais, portas, escadas, postes, cabos e detalhes urbanos.
   - **Camada D — Fundo & Horizonte com Parallax (`StageParallaxLayer`, `Z ∈ [+10.0, +58.0]`):** construções escalonadas, relevo/silhueta de horizonte (ex.: Pão de Açúcar/Baía na Favela, campus/Eixo Monumental na UnDF, pátio/muralhas na Cadeia) e camada atmosférica de céu/nuvens preenchendo qualquer vão aberto.

---

## 4. Clima, Iluminação e Nuvens (`StageWeatherController`)
Arenas com `supportsWeatherVariants = true` devem separar claramente seus elementos nos 4 arrays do `StageWeatherController`:

- **`dayOnlyObjects`:** disco solar/halo, nuvens diurnas/pôr do sol (`StageFramingStandards.BuildWeatherCloudLayer`).
- **`clearNightOnlyObjects`:** lua, estrelas e nuvens noturnas sutis.
- **`nightOnlyObjects` (ativo em `Night` e `RainyNight`):** luzes pontuais quentes, focos de postes, janelas acesas em edifícios/encosta e luzes distantes da orla/cidade.
- **`rainOnlyObjects`:** nuvens densas de tempestade e poças d'água refletivas (sem modificar `.mat` em disco em Play Mode).

**Regras de vento e áudio:**
- `Day` e `Night`: brisa suave (`<= 1.8 m/s` de pico), sem som climático contínuo, e objetos pesados de chão (`wakeThreshold >= 4.2`) permanecem parados até serem chutados por `FighterDebrisPusher`.
- `RainyNight`: vento de tempestade (`6.2 + 4.5 m/s`), chuva 3D com colisão `World 3D` em `StagePhysics`/`StageDebris` + respingos e áudio procedural de chuva.

---

## 5. Prevenção de Z-Fighting e Performance 60 FPS
- **Separação mínima obrigatória (`MinSurfaceSeparation = 0.025f`):** todo elemento sobreposto (cartazes, grafites, janelas acesas, grelhas de bueiro, placas, faixas) deve ter folga mínima de **`0.025 m` a `0.045 m`** em relação à superfície base. Poças d'água rasas devem ficar pelo menos **`0.012 m`–`0.015 m`** acima do piso.
- **Sombras otimizadas:** apenas a **`Directional Light`** principal projeta sombras (`LightShadows.Soft`). Todas as luzes pontuais noturnas adicionais DEVEM usar **`LightShadows.None`**.
- **Materiais URP limpos:** sempre criar/referenciar arquivos `.mat` persistidos em disco (`Assets/UnDFight/Art/Materials/...`) e usar `MaterialPropertyBlock` ou instâncias runtime descartadas em `OnDestroy()` para efeitos temporários de clima.
- **Determinismo inviolável:** nenhuma geometria visual ou prop PhysX pode aplicar força aos lutadores (`FighterHurtbox` = layer 10, `FighterDebrisPusher` = layer 12 colidindo exclusivamente com `StageDebris` = layer 9).
