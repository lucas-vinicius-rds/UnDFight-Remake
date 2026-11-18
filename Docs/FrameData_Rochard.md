# Frame Data — Rochard (`agile_striker`)

- **Altura:** `1,75 m` | **Peso:** `67 kg` | **Faixa de Peso:** `Leve (<= 75 kg)` | **Arquétipo:** `Agile Striker`
- **Taxa de Quadros:** **`60 fps`** (`1 frame = 1 tick de simulação = 16,667 ms`)
- **Rig:** Humanoid (`Rochard_Armature`, `52` ossos, escala `1.0`, sem *root motion*)
- **FBX:** `Assets/UnDFight/Art/Models/Rochard/Rochard.fbx`

---

## Golpes (Ataques)

| Golpe | Startup (frames) | Active (frames) | Recovery (frames) | Total (frames) | Duração (s) | Janela Ativa (frames 1-based) | Notas |
|---|---:|---:|---:|---:|---:|---|---|
| **LightPunch** | 3 | 3 | 7 | 13 | 0,217 s | 4–6 | Jab veloz com leve esquiva de tronco (`LeftHand`) |
| **HeavyPunch** | 5 | 4 | 12 | 21 | 0,350 s | 6–9 | Cruzado rápido com passo firme para frente (`RightHand`) |
| **LightKick** | 4 | 3 | 9 | 16 | 0,267 s | 5–7 | Chute baixo/médio veloz e elástico (`LeftFoot`) |
| **HeavyKick** | 7 | 4 | 16 | 27 | 0,450 s | 8–11 | Chute alto dinâmico/explosivo com contrapeso do braço (`LeftFoot`) |
| **ComboPunchFinisher** | 6 | 4 | 14 | 24 | 0,400 s | 7–10 | Finalizador de mão de combo string: uppercut veloz com passo curto para frente (`RightHand`) |
| **ComboKickFinisher** | 7 | 4 | 15 | 26 | 0,433 s | 8–11 | Finalizador de chute de combo string: chute duplo/ascendente acrobático rápido (`LeftFoot`) |
| **CrouchKick** | 6 | 4 | 15 | 25 | 0,417 s | 7–10 | Rasteira agachada baixa varrendo o chão com a perna estendida à frente (`LeftFoot`, `HitHeight.Low`, derruba) |
| **JumpAttack** | 4 | 8 | 6 | 18 | 0,300 s | 5–12 | Voadora aérea descendente em diagonal (`LeftFoot`, `HitHeight.Overhead`, quebra guarda agachada) |
| **Throw** | 5 | 3 | 14 | 22 | 0,367 s | 6–8 | Agarrão de curto alcance com ambas as mãos (`relaxed_grip`) seguido de arremesso firme de ombro/tronco (`Unblockable`) |
| **Special** | 6 | 4 | 12 | 22 | 0,367 s | 7–10 | Especial **Tornado Relâmpago**: giro ágil com chute alto explosivo em arco (`LeftFoot`) |

---

## Movimentação, Defesa e Estados

| Clipe | Frames | Duração (60 fps) | Loop | Root Motion | Observações |
|---|---:|---:|:---:|:---:|---|
| **Idle** | 60 (1–61) | 1,000 s | Sim | Não | Postura atlética leve com ginga/bounce ágil e joelhos elásticos |
| **WalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Passada leve e saltitante mantendo guarda ágil (avanço) |
| **WalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Passada leve e saltitante mantendo guarda ágil (recuo) |
| **CrouchWalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Deslocamento tático agachado para frente mantendo guarda baixa e altura de hurtbox reduzida |
| **CrouchWalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Recuo tático agachado mantendo guarda baixa fechada e altura de hurtbox reduzida |
| **DashForward** | 14 (1–15) | 0,233 s | Não | Não | Impulso explosivo para frente com inclinação de tronco, guarda firme e retorno ao `Idle` (cancelável após tick 8) |
| **DashBackward** | 16 (1–17) | 0,267 s | Não | Não | Recuo elástico rápido afastando tronco/cabeça para trás e amortecendo na base (evasão nos 5 primeiros ticks) |
| **Jump** | 36 (1–37) | 0,600 s | Não | Não | Antecipação, elevação vertical local e pouso retornando ao `Idle` |
| **Crouch** | 30 (1–31) | 0,500 s | Não | Não | Agachamento em guarda baixa mantido até o frame 31 enquanto segurado |
| **BlockStand** | 24 (1–25) | 0,400 s | Não | Não | Guarda alta compacta mantida até o último frame |
| **HitReact** | 20 (1–21) | 0,333 s | Não | Não | Recuo de tronco e cabeça ao sofrer golpe, recuperando para `Idle` |
| **KnockDown** | 45 (1–46) | 0,750 s | Não | Não | Queda pesada em cadeia (quadril -> costas -> atraso de cabeça/braços -> quique e repouso relaxado no chão) |
| **GetUp** | 40 (1–41) | 0,667 s | Não | Não | Levantamento a partir do chão até `Idle` |
| **Victory** | 60 (1–61) | 1,000 s | Não | Não | Comemoração vibrante e energética com punho erguido até o último frame |
| **Defeat** | 50 (1–51) | 0,833 s | Não | Não | Queda contínua para frente/lado: joelhos -> apoio breve de mão -> colapso de bruços/lado no chão sem inversão |
| **Turn180** | 8 (1–9) | 0,133 s | Não | Não | Pivô de troca de lado em pé: flexão leve nos joelhos, giro de tronco/cabeça, passo curto e retorno ao `Idle` |
