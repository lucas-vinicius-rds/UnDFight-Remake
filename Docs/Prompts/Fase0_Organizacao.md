REGRAS GERAIS: Projeto Unity em D:\Programas SSD\Unity\Unity Projects\UnDFight Remake. Leia Docs/PROGRESSO.md antes. Ao final: marque a fase em PROGRESSO.md, registre em CHANGELOG.md, faca git commit. Nao altere nada fora do escopo da fase.

FASE 0 - Organizacao
1. Verifique versao do Unity e se URP esta ativo; se nao, configure URP.
2. Instale pacotes: Input System (ativar), Cinemachine, TextMeshPro, Test Framework.
3. Crie a estrutura: Assets/UnDFight/{Art/{Source,Models,Animations,Materials,Textures},Scripts/{Core,Fighters,Combat,Camera,Input,UI,AI},Data,Prefabs,Scenes,Audio}.
4. Copie (sem mover) D:\Tralhas\Models para Assets/UnDFight/Art/Source.
5. Revise .gitignore e .gitattributes (FBX, PNG, WAV via Git LFS se fizer sentido).
6. Crie cenas vazias: MainMenu, CharacterSelect, Arena_Favela, Training.
Entrega: relatorio curto + commit.
