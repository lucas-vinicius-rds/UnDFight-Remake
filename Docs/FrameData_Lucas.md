# Frame Data — Lucas (`heavy_brawler`)

- **Altura:** `1,81 m` | **Peso:** `92 kg` | **Faixa de Peso:** `Pesado (> 90 kg)` | **Arquétipo:** `Heavy Brawler`
- **Taxa de Quadros:** **`60 fps`** (`1 frame = 1 tick de simulação = 16,667 ms`)
- **Rig:** Humanoid (`Lucas_Armature`, `52` ossos, escala `1.0`, sem *root motion*)
- **FBX:** `Assets/UnDFight/Art/Models/Lucas/Lucas.fbx`

---

## Golpes (Ataques)

| Golpe | Startup (frames) | Active (frames) | Recovery (frames) | Total (frames) | Duração (s) | Janela Ativa (frames 1-based) | Notas |
|---|---:|---:|---:|---:|---:|---|---|
| **LightPunch** | 5 | 3 | 11 | 19 | 0,317 s | 6–8 | Jab/direto pesado com forte rotação de ombro e tronco (`LeftHand`) |
| **HeavyPunch** | 9 | 5 | 19 | 33 | 0,550 s | 10–14 | Cruzado/gancho potente com grande preparação de quadril e extensão total (`RightHand`) |
| **LightKick** | 6 | 4 | 13 | 23 | 0,383 s | 7–10 | Pisão/chute baixo pesado na altura da canela/joelho (`LeftFoot`) |
| **HeavyKick** | 11 | 5 | 24 | 40 | 0,667 s | 12–16 | Chutão frontal/lateral empurrando com o calcanhar e recuo sólido (`LeftFoot`) |
| **Special** | 10 | 5 | 17 | 32 | 0,533 s | 11–15 | Especial **Martelo do Asfalto**: preparação profunda com as duas mãos e martelada dupla esmagadora (`RightHand`/`LeftHand`) |

---

## Movimentação, Defesa e Estados

| Clipe | Frames | Duração (60 fps) | Loop | Root Motion | Observações |
|---|---:|---:|:---:|:---:|---|
| **Idle** | 60 (1–61) | 1,000 s | Sim | Não | Base plantada e baixa, ombros firmes, guarda alta e fechada protegendo queixo/têmpora, respiração pesada |
| **WalkForward** | 48 (1–49) | 0,800 s | Sim | Não | Passada firme e pesada mantendo guarda dupla fechada (avanço) |
| **WalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | Passada firme e pesada mantendo guarda dupla fechada (recuo) |
| **Jump** | 36 (1–37) | 0,600 s | Não | Não | Antecipação, elevação vertical local e pouso retornando ao `Idle` |
| **Crouch** | 30 (1–31) | 0,500 s | Não | Não | Agachamento em guarda baixa mantido até o frame 31 enquanto segurado |
| **BlockStand** | 24 (1–25) | 0,400 s | Não | Não | Guarda dupla fechada e imóvel mantida até o último frame |
| **HitReact** | 20 (1–21) | 0,333 s | Não | Não | Recuo de tronco e cabeça ao sofrer golpe, recuperando para `Idle` |
| **KnockDown** | 45 (1–46) | 0,750 s | Não | Não | Desequilíbrio e queda de costas no chão |
| **GetUp** | 40 (1–41) | 0,667 s | Não | Não | Levantamento a partir do chão até `Idle` |
| **Victory** | 60 (1–61) | 1,000 s | Não | Não | Postura dominante erguendo e fechando o punho com firmeza até o último frame |
| **Defeat** | 50 (1–51) | 0,833 s | Não | Não | Queda de joelhos e colapso final no chão |
