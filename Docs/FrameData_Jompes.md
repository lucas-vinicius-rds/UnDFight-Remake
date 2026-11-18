# Frame Data — Jompes (`technical_striker`)

- **Altura:** `1,65 m` | **Peso:** `70 kg` | **Faixa de Peso:** `Leve (<= 75 kg)` | **Arquétipo:** `Technical Striker`
- **Taxa de Quadros:** **`60 fps`** (`1 frame = 1 tick de simulação = 16,667 ms`)
- **Rig:** Humanoid (`Jompes_Armature`, `52` ossos, escala `1.0`, sem *root motion*)
- **FBX:** `Assets/UnDFight/Art/Models/Jompes/Jompes.fbx`

---

## Golpes (Ataques)

| Golpe | Startup (frames) | Active (frames) | Recovery (frames) | Total (frames) | Duração (s) | Janela Ativa (frames 1-based) | Notas |
|---|---:|---:|---:|---:|---:|---|---|
| **LightPunch** | 5 | 3 | 9 | 17 | 0,283 s | 6–8 | Jab/direto marcial rápido com guarda traseira fechada (`LeftHand`) |
| **HeavyPunch** | 8 | 4 | 15 | 27 | 0,450 s | 9–12 | Golpe penetrante/cruzado firme com giro limpo de quadril (`RightHand`) |
| **LightKick** | 6 | 3 | 11 | 20 | 0,333 s | 7–9 | Chute frontal/snap rápido na altura baixa/média (`LeftFoot`) |
| **HeavyKick** | 10 | 4 | 19 | 33 | 0,550 s | 11–14 | Chute alto circular técnico com rotação de quadril e guarda fechada (`LeftFoot`) |
| **ComboPunchFinisher** | 8 | 4 | 16 | 28 | 0,467 s | 9–12 | Finalizador de mão de combo string: palma/estocada dupla marcial rápida com retorno preciso à guarda (`RightHand`) |
| **ComboKickFinisher** | 9 | 4 | 18 | 31 | 0,517 s | 10–13 | Finalizador de chute de combo string: chute giratório alto técnico (*back kick* / *spinning hook*) com controle limpo de eixo (`LeftFoot`) |
| **CrouchKick** | 9 | 4 | 17 | 30 | 0,500 s | 10–13 | Rasteira agachada baixa varrendo o chão com a perna estendida à frente (`LeftFoot`, `HitHeight.Low`, derruba) |
| **JumpAttack** | 5 | 9 | 7 | 21 | 0,350 s | 6–14 | Voadora aérea descendente em diagonal (`LeftFoot`, `HitHeight.Overhead`, quebra guarda agachada) |
| **Throw** | 6 | 3 | 16 | 25 | 0,417 s | 7–9 | Agarrão de curto alcance com ambas as mãos (`relaxed_grip`) seguido de arremesso firme de ombro/tronco (`Unblockable`) |
| **Special** | 9 | 4 | 16 | 29 | 0,483 s | 10–13 | Especial **Golpe Sombrio**: avanço baixo em postura fechada seguido de estocada/palma perfurante central (`RightHand`) |

---

## Movimentação, Defesa e Estados

| Clipe | Frames | Duração (60 fps) | Loop | Root Motion | Observações |
|---|---:|---:|:---:|:---:|---|
| **Idle** | 60 (1–61) | 1,000 s | Sim | Não | Guarda marcial disciplinada e fechada (mão da frente alta, mão de trás junto ao peito/queixo) |
| **WalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Deslocamento técnico compacto mantendo a guarda marcial travada (avanço) |
| **WalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Deslocamento técnico compacto mantendo a guarda marcial travada (recuo) |
| **CrouchWalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Deslocamento tático agachado para frente mantendo guarda baixa e altura de hurtbox reduzida |
| **CrouchWalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Recuo tático agachado mantendo guarda baixa fechada e altura de hurtbox reduzida |
| **DashForward** | 14 (1–15) | 0,233 s | Não | Não | Impulso explosivo para frente com inclinação de tronco, guarda firme e retorno ao `Idle` (cancelável após tick 8) |
| **DashBackward** | 16 (1–17) | 0,267 s | Não | Não | Recuo elástico rápido afastando tronco/cabeça para trás e amortecendo na base (evasão nos 5 primeiros ticks) |
| **Jump** | 36 (1–37) | 0,600 s | Não | Não | Antecipação, elevação vertical local e pouso retornando ao `Idle` |
| **Crouch** | 30 (1–31) | 0,500 s | Não | Não | Agachamento em guarda baixa mantido até o frame 31 enquanto segurado |
| **BlockStand** | 24 (1–25) | 0,400 s | Não | Não | Bloqueio marcial peek-a-boo fechado até o último frame |
| **HitReact** | 20 (1–21) | 0,333 s | Não | Não | Recuo de tronco e cabeça ao sofrer golpe, recuperando para `Idle` |
| **KnockDown** | 45 (1–46) | 0,750 s | Não | Não | Queda pesada em cadeia (quadril -> costas -> atraso de cabeça/braços -> quique e repouso relaxado no chão) |
| **GetUp** | 40 (1–41) | 0,667 s | Não | Não | Levantamento a partir do chão até `Idle` |
| **Victory** | 60 (1–61) | 1,000 s | Não | Não | Postura firme e disciplinada de sentinela/artes marciais até o último frame |
| **Defeat** | 50 (1–51) | 0,833 s | Não | Não | Queda contínua para frente/lado: joelhos -> apoio breve de mão -> colapso de bruços/lado no chão sem inversão |
| **Turn180** | 8 (1–9) | 0,133 s | Não | Não | Pivô de troca de lado em pé: flexão leve nos joelhos, giro de tronco/cabeça, passo curto e retorno ao `Idle` |
