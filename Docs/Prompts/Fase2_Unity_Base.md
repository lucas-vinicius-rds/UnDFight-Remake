REGRAS GERAIS: Projeto Unity em D:\Programas SSD\Unity\Unity Projects\UnDFight Remake. Leia Docs/PROGRESSO.md antes. Ao final: marque a fase em PROGRESSO.md, registre em CHANGELOG.md, faca git commit. Nao altere nada fora do escopo da fase.

FASE 2 - Base Unity
- FighterData (ScriptableObject): nome, altura, peso, vida, velocidade, forca do pulo, gravidade. Altura escala modelo e hurtbox; peso afeta gravidade, velocidade e knockback.
- Criar FighterData_Lucas (1.81, 92) e FighterData_Tiago (1.79, 87).
- Movimento travado no eixo Z, lutadores sempre se olham, limites do palco.
- Camera lateral com Cinemachine, zoom dinamico pela distancia.
- Input System para 2 jogadores: teclado (P1 WASD+JKL, P2 setas+numpad) e gamepad, com input buffer.
- Maquina de estados: Idle, Walk, Jump, Crouch, Block, Attack, Hit, KnockDown.
- Simulacao em tick fixo de 60 Hz. Teste com capsulas em cena de teste.
Entrega: cena Training jogavel com 2 capsulas.
