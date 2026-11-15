REGRAS GERAIS: Projeto Unity em D:\Programas SSD\Unity\Unity Projects\UnDFight Remake. Leia Docs/PROGRESSO.md antes. Ao final: marque a fase em PROGRESSO.md, registre em CHANGELOG.md, faca git commit. Nao altere nada fora do escopo da fase.

FASE 1 - Blender: Lucas (man_in_black)
Entrada: D:\Tralhas\Models\Lucas\Meshy_AI_Man_in_Black_biped_Animation_Running_withSkin_RigCorrigido.fbx (texturas meshy_*.png na mesma pasta). Use Blender 4.x via MCP/bpy.
1. Importar, aplicar escala/rotacao, remover vertices soltos, corrigir normais, reduzir para ~30k tris mantendo forma. Altura final 1,81 m.
2. Novo rig humanoide (Rigify ou compativel com Humanoid do Unity): dedos, coluna, pescoco, pes. Refazer pesos e corrigir ombros, quadris, joelhos.
3. Animacoes, 30 fps, sem root motion, nomes exatos: Idle, WalkForward, WalkBackward, Jump, Crouch, BlockStand, LightPunch, HeavyPunch, LightKick, HeavyKick, HitReact, KnockDown, GetUp, Victory, Defeat.
4. Golpes com antecipacao, impacto e recuperacao claros (anote frames de startup/active/recovery de cada golpe em Docs/FrameData_Lucas.md).
5. Exportar FBX (Y up, escala 1, clips separados) para Assets/UnDFight/Art/Models/Lucas/ e salvar o .blend em Art/Source/Blend/ e o script bpy em Tools/blender/.
Entrega: renders de verificacao + relatorio.
