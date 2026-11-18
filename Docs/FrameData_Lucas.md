# Frame Data — Lucas (`heavy_brawler`)

- **Altura:** `1,81 m` | **Peso:** `92 kg` | **Faixa de Peso:** `Pesado (> 90 kg)` | **Arquétipo:** `Heavy Brawler`
- **Taxa de Quadros:** **`60 fps`** (`1 frame = 1 tick de simulação = 16,667 ms`)
- **Rig:** Humanoid (`Lucas_Armature`, `52` ossos, escala `1.0`, sem *root motion*)
- **FBX:** `Assets/UnDFight/Art/Models/Lucas/Lucas.fbx`

---

## Golpes (Ataques)

| Golpe | Startup (frames) | Active (frames) | Recovery (frames) | Total (frames) | Duração (s) | Janela Ativa (frames 1-based) | Notas |
|---|---:|---:|---:|---:|---:|---|---|
| **LightPunch** | 7 | 4 | 13 | 24 | 0,400 s | 8–11 | Jab/direto pesado com forte rotação de ombro e tronco (`LeftHand`) |
| **HeavyPunch** | 12 | 5 | 22 | 39 | 0,650 s | 13–17 | Cruzado/gancho potente com grande preparação de quadril e extensão total (`RightHand`) |
| **LightKick** | 8 | 4 | 15 | 27 | 0,450 s | 9–12 | Pisão/chute baixo pesado na altura da canela/joelho (`LeftFoot`) |
| **HeavyKick** | 14 | 5 | 26 | 45 | 0,750 s | 15–19 | Chutão frontal/lateral empurrando com o calcanhar e recuo sólido (`LeftFoot`) |
| **ComboPunchFinisher** | 11 | 5 | 21 | 37 | 0,617 s | 12–16 | Finalizador de mão de combo string: gancho/uppercut duplo ascendente pesado com grande rotação de tronco e recuo firme (`RightHand`) |
| **ComboKickFinisher** | 13 | 5 | 22 | 40 | 0,667 s | 14–18 | Finalizador de chute de combo string: joelhada/pisão frontal pesado com empurrão potente (`LeftFoot`) |
| **CrouchKick** | 11 | 5 | 21 | 37 | 0,617 s | 12–16 | Rasteira agachada baixa varrendo o chão com a perna estendida à frente (`LeftFoot`, `HitHeight.Low`, derruba) |
| **JumpAttack** | 5 | 9 | 7 | 21 | 0,350 s | 6–14 | Voadora aérea descendente em diagonal (`LeftFoot`, `HitHeight.Overhead`, quebra guarda agachada) |
| **Throw** | 6 | 3 | 16 | 25 | 0,417 s | 7–9 | Agarrão de curto alcance com ambas as mãos (`relaxed_grip`) seguido de arremesso firme de ombro/tronco (`Unblockable`) |
| **Special** | 12 | 5 | 20 | 37 | 0,617 s | 13–17 | Especial **Martelo do Asfalto**: preparação profunda com as duas mãos e martelada dupla esmagadora (`RightHand`/`LeftHand`) |

---

## Movimentação, Defesa e Estados

| Clipe | Frames | Duração (60 fps) | Loop | Root Motion | Observações |
|---|---:|---:|:---:|:---:|---|
| **Idle** | 60 (1–61) | 1,000 s | Sim | Não | Base plantada e baixa, ombros firmes, guarda alta e fechada protegendo queixo/têmpora, respiração pesada |
| **WalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Passada firme e pesada mantendo guarda dupla fechada (avanço) |
| **WalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Passada firme e pesada mantendo guarda dupla fechada (recuo) |
| **CrouchWalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Deslocamento tático agachado para frente mantendo guarda baixa e altura de hurtbox reduzida |
| **CrouchWalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Recuo tático agachado mantendo guarda baixa fechada e altura de hurtbox reduzida |
| **DashForward** | 14 (1–15) | 0,233 s | Não | Não | Impulso explosivo para frente com inclinação de tronco, guarda firme e retorno ao `Idle` (cancelável após tick 8) |
| **DashBackward** | 16 (1–17) | 0,267 s | Não | Não | Recuo elástico rápido afastando tronco/cabeça para trás e amortecendo na base (evasão nos 5 primeiros ticks) |
| **Jump** | 48 (1–49) | 0,800 s | Não | Não | Arco aéreo puro (`0.0` decolagem → `0.50` ápice → `1.0` contato no solo) sincronizado com `JumpProgress` |
| **Crouch** | 30 (1–31) | 0,500 s | Não | Não | Agachamento em guarda baixa mantido até o frame 31 enquanto segurado |
| **BlockStand** | 24 (1–25) | 0,400 s | Não | Não | Guarda dupla fechada e imóvel mantida até o último frame |
| **HitReact** | 20 (1–21) | 0,333 s | Não | Não | Recuo de tronco e cabeça ao sofrer golpe, recuperando para `Idle` |
| **KnockDown** | 45 (1–46) | 0,750 s | Não | Não | Queda pesada em cadeia (quadril -> costas -> atraso de cabeça/braços -> quique e repouso relaxado no chão) |
| **GetUp** | 40 (1–41) | 0,667 s | Não | Não | Levantamento a partir do chão até `Idle` |
| **Victory** | 60 (1–61) | 1,000 s | Não | Não | Postura dominante erguendo e fechando o punho com firmeza até o último frame |
| **Defeat** | 50 (1–51) | 0,833 s | Não | Não | Queda contínua para frente/lado: joelhos -> apoio breve de mão -> colapso de bruços/lado no chão sem inversão |
| **Turn180** | 8 (1–9) | 0,133 s | Não | Não | Pivô de troca de lado em pé: flexão leve nos joelhos, giro de tronco/cabeça, passo curto e retorno ao `Idle` |
