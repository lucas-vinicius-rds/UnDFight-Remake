# UnDFight Remake - Acompanhamento de Fases

Legenda: [ ] pendente | [~] em andamento | [x] concluida

Regra para TODO agente: ao terminar uma fase, marcar aqui, adicionar entrada em CHANGELOG.md e fazer commit Git ("Fase X: resumo").

## Decisoes
- Unity (C#, URP, Input System novo), 2.5D lateral, simulacao em 60 Hz
- Versus local + CPU simples; teclado e gamepad
- Lutadores: Lucas (man_in_black) 1,81 m / 92 kg | Tiago 1,79 m / 87 kg | Jompes 1,65 m / 70 kg | Rochard 1,75 m / 67 kg | Isaac 1,73 m / 73 kg | Enomoto 1,77 m / 80 kg | Gabubu 1,62 m / 67 kg | Ibaneis (the_watchful_executive, Chefe Final) 1,70 m / 120 kg
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
- [x] Fase 6.1 - Redesign dos menus estilo MKX, diorama 3D no MainMenu e selecao de mapas (`Arena_Favela` jogavel)
- [x] Fase 7 - Mapa 3D (favela)
- [x] Fase 7.1 - Eliminacao de Z-fighting na `Arena_Favela` e inicio automatico no Menu Principal
- [x] Fase 8 - CPU e polimento
- [x] Fase 9 - Faixas de peso, arquetipos e animacoes exclusivas (Lucas e Tiago)
- [x] Fase 9.1 - Rig de dedos 52 ossos Humanoid e punhos fechados (Lucas e Tiago)
- [x] Fase 10 - Lutadores leves Jompes (1,65 m / 70 kg, TechnicalStriker) e Rochard (1,75 m / 67 kg, AgileStriker) com 52 ossos e 60 fps
- [x] Fase 11 - Pes plantados no chao, chain combos, barra de especial e golpe especial
- [x] Fase 11.1 - Correcao da postura e centro de gravidade dos 4 lutadores
- [x] Fase 11.2 - Correcao do nivelamento anatomico de joelhos e ataques HeavyPunch/Special do Tiago (e yaw do Special do Jompes)
- [x] Fase 11.3 - Giro completo de 180 graus ao trocar de lado (clipe Turn180 60fps, AC_Fighter cancelavel e rotacao visual frontal sem atraso logico)
- [x] Fase 12.1 - Base reutilizavel de interacao de cenario com NVIDIA PhysX (layers StagePhysics/StageDebris/FighterHurtbox) e objetos quebraveis na Arena_Favela
- [x] Fase 12.2 - Interacao de mapa estilo Mortal Kombat (arremesso de objetos com PhysX, combo HoldBlock+LightPunch, prompt contextual na HUD e Kinematic Rigidbody nas hurtboxes)
- [x] Fase 13.1 - Movimentacao avancada e golpes direcionais (Dash/Backdash, Throw + Throw Tech, CrouchKick e JumpAttack em 60 Hz)
- [x] Fase 13.2 - Especiais unicos por arquetipo, golpes EX (50%) e Super Flash com zoom de camera (100%)
- [x] Fase 13.3 - Polimento cinematografico e detalhamento das 22 animacoes 60 fps por lutador (`character_pipeline.py`)
- [x] Fase 13.4 - Correcao de queda/derrota (`Defeat`, `KnockDown`, `GetUp`) e sistema de andar agachado (`CrouchWalkForward` / `CrouchWalkBackward`, 24 clipes)
- [x] Fase 13.5 - Correcao da transicao `CrouchWalk` / `CrouchKick` / bloqueio agachado para `Crouch` sem levantar (`AlreadyCrouched`, `offset = 0.25f`)
- [x] Fase 14.1 - Novos lutadores regulares: Isaac (1,73 m / 73 kg, AgileStriker), Enomoto (1,77 m / 80 kg, TechnicalStriker) e Gabubu (1,62 m / 67 kg, StreetScrappy) — elenco expandido de 4 para 7 lutadores
- [x] Fase 14.1.1 - Sistema de Combo Strings estilo Mortal Kombat (Dial-a-Combo queue, blockstrings, mix-ups Overhead/Low, finalizadores `ComboPunchFinisher` / `ComboKickFinisher`, 26 clipes a 60 fps nos 7 lutadores, nome da string na HUD e suporte na IA)
- [x] Fase 14.1.2 - Desaceleracao calibrada e peso do combate em 60 Hz (nova tabela de frame data +25%–35% em startup/recovery, reexportacao dos 26 clipes a 60 fps nos 7 lutadores com `MoveData.TotalTicks == clipFrames`, hitstop/hitstun/blockstun ampliados, velocidades de locomocao/pulo/gravidade/projeteis recalibradas e janela Dial-a-Combo de 22 ticks)
- [x] Fase 14.1.3 - Pulo padrao sincronizado com a fisica 60 Hz (clipe `Jump` de 48 frames `1..49` em arco aereo puro `0.00..1.00`, parametro `Float JumpProgress` com `timeParameterActive = true` em `AC_Fighter.controller` e arco de pulo calibrado `jumpForce = 10,6` / `baseGravity = 28,0` nos 7 lutadores)
- [x] Fase 14.2 - Chefe Final Secreto: Ibaneis (`the_watchful_executive`, `1,70 m / 120 kg`, `Heavy`, `BossExecutive`, `maxHealth = 1100`, `isSecretBoss = true`, Especial **`Decreto de Ferro`** com `SpecialMechanicType.BossDecree` combinando Super Armor + impacto melee + onda de choque 60 Hz a `9,0 m/s`, 4 Combo Strings exclusivas e grade `4x2` completa com 8 lutadores)
- [x] Fase 15.1 - Fisica de Cenario v2: 5a layer PhysX `FighterDebrisPusher` (slot 12) com acoplamento unidirecional cinematico->dinamico em todos os 8 lutadores, destrocos persistentes com PhysX Auto-Sleep (`StageDebrisFragment`), props soltos (`StageDynamicProp`), pendulares `HingeJoint` (`StageHangingProp`), ondas de choque (`ApplyStageShockwave`), vento aerodinamico real (`StageWindController`/`StageWindAffectable`, $F_{drag} = \frac{1}{2}\rho C_d A |v_{rel}| v_{rel}$), chuva 3D com colisao fisica + respingos + piso molhado runtime (`StageWeatherController`), seletor de clima MKX (`Dia / Noite / Noite Chuvosa`) em `CharacterSelectController`, audio procedural de clima e `Docs/FISICA_ARQUITETURA.md`
- [x] Fase 15.2 - Revitalizacao detalhada da `Arena_Favela.unity` baseada em `Docs/Referencias/mapa.jpg`: suporte a `dayOnlyObjects`/`clearNightOnlyObjects`/`rainOnlyObjects` em `StageWeatherController`, utilitario idempotente `ArenaFavelaUpgradeEditor.cs` (detalhes do Bar do Jorge, toldo corrugado, spots pescoco-de-ganso, cartazes/tabela de precos, caixa de som, corrimao na escadaria, grafites, medidor/conduites/lixeira no poste, bueiros, fechamento completo dos cantos `x < -8` e `x > 8`, antenas/parabolicas/caixas d'agua extras no morro), novos prefabs fisicos (`Prop_HangingCloth` x3 no varal `HingeJoint`, `Prop_HangingLantern` sob beiral direito, `Prop_DynamicCone` x2) e 3 climas completos (Sol no Por do Sol, Lua/estrelas na Noite Limpa, 22+ janelas quentes acesas + luzes `LightShadows.None` na Noite/Chuva e 6 pocas especulares sem Z-fighting na Noite Chuvosa)
- [x] Fase 15.2.1 - Eliminacao definitiva da "barra preta" inferior no zoom-out maximo (`12,0 m`), preenchimento detalhado do horizonte direito (vao da rampa) nas 3 variacoes climaticas da `Arena_Favela.unity` e padrao reutilizavel + teste anti-regressao para todos os futuros cenarios (`StageFramingStandards.cs` + `Docs/PADRAO_CENARIOS.md`): `SandShoulder_Front` estendido ate `z = -22,5 m` (largura `68 m`, emenda exata `z = -2,60` sem Z-fighting), `FavelaHillsideRightFill` (14 casinhas escalonadas com telhados/caixas d'agua/antenas/varais + 18+ janelas noturnas), Pao de Acucar/Morro da Urca/Baia de Guanabara/luzes da orla enquadrados, palmeiras/poste alto/cabos no topo direito e 3 camadas reutilizaveis de nuvens climaticas
- [x] Fase 15.3 - Novo mapa jogavel `Arena_UnDF.unity` (**Frente da UnDF** — Campus Universitario, Brasilia - DF) fiel a `Docs/Referencias/UnDF Fight Arena Concept.jpg`: copia preservada de `Stage_UnDF_Preview.jpg`, `StageData_UnDF.asset` (`dayVariantLabel = "DIA"`), registro em `StageRoster.asset` (Favela -> UnDF -> Treino) e `EditorBuildSettings`, construtor idempotente `ArenaUnDFBuilderEditor.cs` com texturas PNG procedurais (fachada UnDF + losangos + selo GDF/Ipe, bandeiras DF/Brasil/UnDF, placa Proibido Estacionar, asfalto), fachada principal + entrada de vidro + escada vermelha interna + marquise curva + alas 3 andares com ares-condicionados/rampa/bicicletario/grande arvore verde-dourada + predios vizinhos fechando alem de ±22m + piso frontal ate `z = -22,5 m`, Fisica v2 completa (3 bandeiras `HingeJoint`, banner academico suspenso, 2 quebraveis, 2 arremessaveis MK Cone/Lixeira, 5 props dinamicos) e 3 climas completos (Dia, Noite com 20+ janelas acesas + luzes `LightShadows.None` e Noite Chuvosa com 7 pocas + nuvens + chuva 3D)

- Decisao: animacoes em 60 fps (1 frame anim = 1 tick de simulacao)
