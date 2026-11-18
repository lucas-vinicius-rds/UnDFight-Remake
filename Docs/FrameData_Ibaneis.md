# Frame Data — Ibaneis (`boss_executive`)

- **Altura:** `1,70 m` | **Peso:** `120 kg` | **Faixa de Peso:** `Pesado (> 90 kg)` | **Arquétipo:** `Boss Executive`
- **Taxa de Quadros:** **`60 fps`** (`1 frame = 1 tick de simulação = 16,667 ms`)
- **Rig:** Humanoid (`Ibaneis_Armature`, `52` ossos, escala `1.0`, sem *root motion*)
- **FBX:** `Assets/UnDFight/Art/Models/Ibaneis/Ibaneis.fbx`

---

## Golpes (Ataques)

| Golpe | Startup (frames) | Active (frames) | Recovery (frames) | Total (frames) | Duração (s) | Janela Ativa (frames 1-based) | Notas |
|---|---:|---:|---:|---:|---:|---|---|
| **LightPunch** | 7 | 4 | 13 | 24 | 0,400 s | 8–11 | Golpe direto autoritário de comando mantendo o tronco ereto (`LeftHand`) |
| **HeavyPunch** | 12 | 5 | 22 | 39 | 0,650 s | 13–17 | **Martelada do Gabinete**: marretada/direto executivo devastador usando os 120 kg de massa (`RightHand`, Super Armor) |
| **LightKick** | 8 | 4 | 15 | 27 | 0,450 s | 9–12 | Chute baixo/frontal rápido e pesado na canela (`LeftFoot`) |
| **HeavyKick** | 14 | 5 | 26 | 45 | 0,750 s | 15–19 | **Veto Absoluto**: pisão/chutão frontal imperial empurrando o oponente com o calcanhar (`LeftFoot`, derruba) |
| **ComboPunchFinisher** | 11 | 5 | 21 | 37 | 0,617 s | 12–16 | Finalizador de mão (**Ordem Executiva**): marretada executiva pesada (`RightHand`, `HitHeight.Overhead`) |
| **ComboKickFinisher** | 13 | 5 | 22 | 40 | 0,667 s | 14–18 | Finalizador de chute (**Veto Absoluto** / **Demissão Sumária**): empurrão frontal imperial devastador (`LeftFoot`, `causesKnockdown = true`) |
| **CrouchKick** | 11 | 5 | 21 | 37 | 0,617 s | 12–16 | Rasteira agachada baixa varrendo o chão com a perna estendida à frente (`LeftFoot`, `HitHeight.Low`, derruba) |
| **JumpAttack** | 5 | 9 | 7 | 21 | 0,350 s | 6–14 | Voadora aérea descendente em diagonal (`LeftFoot`, `HitHeight.Overhead`, quebra guarda agachada) |
| **Throw** | 6 | 3 | 16 | 25 | 0,417 s | 7–9 | Agarrão de curto alcance com ambas as mãos (`relaxed_grip`) seguido de arremesso firme de ombro/tronco (`Unblockable`) |
| **Special** | 12 | 5 | 20 | 37 | 0,617 s | 13–17 | Especial **Decreto de Ferro**: postura imponente com Super Armor seguida de martelada dupla esmagadora e disparo de onda de choque (`RightHand`/`LeftHand` + projétil) |

---

## Movimentação, Defesa e Estados

| Clipe | Frames | Duração (60 fps) | Loop | Root Motion | Observações |
|---|---:|---:|:---:|:---:|---|
| **Idle** | 60 (1–61) | 1,000 s | Sim | Não | Postura de comando executivo vigilante, tronco imponente, punho dianteiro em guarda de autoridade e base pesada plantada |
| **WalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Passada pesada e autoritária mantendo postura ereta e guarda executiva (avanço) |
| **WalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Passada pesada e autoritária mantendo postura ereta e guarda executiva (recuo) |
| **CrouchWalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Deslocamento tático agachado para frente mantendo guarda baixa e altura de hurtbox reduzida |
| **CrouchWalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Recuo tático agachado mantendo guarda baixa fechada e altura de hurtbox reduzida |
| **DashForward** | 14 (1–15) | 0,233 s | Não | Não | Impulso explosivo para frente com inclinação de tronco, guarda firme e retorno ao `Idle` (cancelável após tick 8) |
| **DashBackward** | 16 (1–17) | 0,267 s | Não | Não | Recuo elástico rápido afastando tronco/cabeça para trás e amortecendo na base (evasão nos 5 primeiros ticks) |
| **Jump** | 48 (1–49) | 0,800 s | Não | Não | Arco aéreo puro (`0.0` decolagem → `0.50` ápice → `1.0` contato no solo) sincronizado com `JumpProgress` |
| **Crouch** | 30 (1–31) | 0,500 s | Não | Não | Agachamento em guarda baixa mantido até o frame 31 enquanto segurado |
| **BlockStand** | 24 (1–25) | 0,400 s | Não | Não | Guarda executiva blindada com antebraços cruzados mantida até o último frame |
| **HitReact** | 20 (1–21) | 0,333 s | Não | Não | Recuo de tronco e cabeça ao sofrer golpe, recuperando para `Idle` |
| **KnockDown** | 45 (1–46) | 0,750 s | Não | Não | Queda pesada em cadeia (quadril -> costas -> atraso de cabeça/braços -> quique e repouso relaxado no chão) |
| **GetUp** | 40 (1–41) | 0,667 s | Não | Não | Levantamento a partir do chão até `Idle` |
| **Victory** | 60 (1–61) | 1,000 s | Não | Não | Postura imperial de comando erguendo o queixo e o punho fechado com autoridade até o último frame |
| **Defeat** | 50 (1–51) | 0,833 s | Não | Não | Queda contínua para frente/lado: joelhos -> apoio breve de mão -> colapso de bruços/lado no chão sem inversão |
| **Turn180** | 8 (1–9) | 0,133 s | Não | Não | Pivô de troca de lado em pé: flexão leve nos joelhos, giro de tronco/cabeça, passo curto e retorno ao `Idle` |
