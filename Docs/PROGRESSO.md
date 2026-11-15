# UnDFight Remake - Acompanhamento de Fases

Legenda: [ ] pendente | [~] em andamento | [x] concluida

Regra para TODO agente: ao terminar uma fase, marcar aqui, adicionar entrada em CHANGELOG.md e fazer commit Git ("Fase X: resumo").

## Decisoes
- Unity (C#, URP, Input System novo), 2.5D lateral, simulacao em 60 Hz
- Versus local + CPU simples; teclado e gamepad
- Lutadores: Lucas (man_in_black) 1,81 m / 92 kg | Tiago 1,79 m / 87 kg | Jompes 1,65 m / 70 kg | Rochard 1,75 m / 67 kg
- Golpes: soco fraco/forte, chute fraco/forte, golpe especial exclusivo (100% barra), chain combos no acerto, agachar, pular, bloquear
- Modelos originais: D:\Tralhas\Models (copiar, nao mover)
- Mapa: favela carioca ao por do sol (referencia enviada)

## Fases
- [x] Fase 0 - Organizacao do projeto, pacotes e docs
- [x] Fase 1 - Blender: Lucas (limpeza, rig, 14 animacoes, FBX)
- [x] Fase 2 - Base Unity: camera 2.5D, movimento, FighterData, input
- [x] Fase 3 - Importar Lucas no Unity + Animator
- [x] Fase 4 - Blender: Tiago (reuso do script)
- [x] Fase 5 - Combate: hitbox/hurtbox, vida, rounds, HUD
- [x] Fase 6 - Menus: principal, opcoes, selecao, pausa
- [x] Fase 7 - Mapa 3D (favela)
- [x] Fase 8 - CPU e polimento
- [x] Fase 9 - Faixas de peso, arquetipos e animacoes exclusivas (Lucas e Tiago)
- [x] Fase 9.1 - Rig de dedos 52 ossos Humanoid e punhos fechados (Lucas e Tiago)
- [x] Fase 10 - Lutadores leves Jompes (1,65 m / 70 kg, TechnicalStriker) e Rochard (1,75 m / 67 kg, AgileStriker) com 52 ossos e 60 fps
- [x] Fase 11 - Pes plantados no chao, chain combos, barra de especial e golpe especial
- [x] Fase 11.1 - Correcao da postura e centro de gravidade dos 4 lutadores

- Decisao: animacoes em 60 fps (1 frame anim = 1 tick de simulacao)
