# CHANGELOG - UnDFight Remake

Formato: data | fase | o que mudou | arquivos principais

- 2026-11-15 | Planejamento | Criados Docs/PROGRESSO.md, CHANGELOG.md e Docs/Prompts | Docs/
- 2026-11-15 | Fase 0 | Organização de pastas, pacotes (URP, Input System, Cinemachine, TMP, Test Framework), cópia de modelos base (Lucas/Tiago), criação de cenas e revisão de Git/LFS | Assets/UnDFight/, Packages/manifest.json, ProjectSettings/, .gitignore, .gitattributes, Docs/
- 2026-11-15 | Fase 1 | Lucas no Blender: limpeza de malha (~30k tris preservando cabelo), rig Humanoid escala 1.0, 15 animações a 60 fps, FrameData_Lucas.md, pipeline reutilizável (character_pipeline.py) e exportação FBX | Assets/UnDFight/Art/Models/Lucas/Lucas.fbx, Assets/UnDFight/Art/Source/Blend/, Tools/blender/character_pipeline.py, Docs/FrameData_Lucas.md
- 2026-11-15 | Fase 2 | Base Unity: FighterData/FighterRoster, fórmulas centralizadas (FighterScaling), simulação 60 Hz (FightSimulation), movimento 2D (eixo Z travado), máquina de estados, Input System P1/P2 + Gamepad hot-plug + buffer 6 frames, câmera Cinemachine 2.5D, cena Training com cápsulas e HUD de debug, e testes Edit Mode | Assets/UnDFight/Scripts/, Assets/UnDFight/Data/, Assets/UnDFight/Scenes/Training.unity
- 2026-11-15 | Planejamento | Animacoes mudadas para 60 fps | Docs/Prompts/Fase1
- 2026-11-15 | Fase 3 | Importação do Lucas.fbx (Humanoid 22 ossos, +Z forward, escala 1.0, 15 clips 60 fps sem root motion), material URP Lucas_Mat, Animator Controller base AC_Fighter (tick fixo 60 Hz), prefab Fighter_Lucas, integração em FighterData_Lucas e cena Training (P1) | Assets/UnDFight/Art/Models/Lucas/Lucas.fbx.meta, Assets/UnDFight/Art/Materials/Lucas_Mat.mat, Assets/UnDFight/Art/Animations/AC_Fighter.controller, Assets/UnDFight/Prefabs/Fighter_Lucas.prefab, Assets/UnDFight/Data/FighterData_Lucas.asset, Assets/UnDFight/Scripts/Fighters/FighterController.cs, Assets/UnDFight/Scenes/Training.unity
