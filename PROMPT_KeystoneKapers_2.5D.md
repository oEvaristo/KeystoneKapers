# Prompt — Keystone Kapers 2.5D na Unity

> Cole este texto inteiro como primeira mensagem. Ele assume um projeto Unity 6 (URP + Input System) com a Unity CLI e o MCP da Unity conectados ao Editor aberto, e com os assets descritos em "Assets existentes".

---

Crie neste projeto Unity um jogo completo e jogável inspirado no Keystone Kapers (Atari 2600), com personagens e cenário em 3D e movimento em um plano lateral (2.5D). Use a Unity CLI e o MCP da Unity para inspecionar o projeto, escrever os scripts, montar a cena, configurar componentes e testar em Play Mode. Execute tudo até entregar uma cena jogável; não fique só descrevendo instruções. Trabalhe de forma autônoma e só me pergunte algo se for realmente impossível decidir sozinho.

## 1. Preparação e regras de trabalho

- Confirme com `unity status` que há um Editor conectado e liste os comandos disponíveis. Use o Editor ao vivo (comandos, `eval`/`run_script`) em vez de editar arquivos `.unity`/`.prefab` à mão.
- Ative `PlayerSettings.runInBackground = true`. Sem isso o Play Mode fica parado no primeiro frame quando o Editor está sem foco e os testes pela CLI não avançam.
- Salve todos os arquivos de texto em UTF-8 (o projeto usa acentos em português). Não use comandos de shell que regravem arquivos em outra codificação.
- Monte a cena por meio de um script de Editor idempotente (menu "Keystone Kapers/Construir Cena Principal"). Ao rodar de novo, ele deve reaproveitar assets existentes (clips, Animator Controllers, configs, prefabs) mantendo o mesmo GUID — nunca apague e recrie assets referenciados — e atribuir explicitamente os controllers também nas instâncias da cena.
- Não reconstrua a cena só para pequenos ajustes: corrija prefabs/cena/assets no lugar.
- Nunca entre em Play Mode logo após apagar/recriar assets; espere a reimportação terminar.
- Pastas novas: `Assets/Scripts/{Core,Player,Fugitive,Transport,Obstacles,UI,Config}`, `Assets/Editor`, `Assets/Animations`, `Assets/Materials`, `Assets/Config`, `Assets/Prefabs/Gameplay`. Scripts de teste da IA ficam fora de `Assets` (ex.: `AgentScripts/`), e capturas de tela também fora de `Assets` (ex.: `Screenshots/`).
- Não altere os modelos, clips e prefabs originais. Para adaptar algo, crie prefabs que envolvem o original (wrapper) ou cópias.
- Crie e salve a cena principal em `Assets/Scenes`, coloque-a como primeira no Build Settings e não apague as cenas existentes.

## 2. Assets existentes — inspecione antes de usar

Pastas: `Assets/Modelos/{Cenarios,Fugitivo,Inimigos,Policial}`, `Assets/Prefabs/{Cenario,Fugitivo,Inimigos,Policial}`, `Assets/Scenes`, `Assets/Settings`.

Prefabs: Policial (jogador), Fugitivo, carrinho de compras, bola de praia, rádio antigo, avião biplano de brinquedo, escada rolante e elevador.

Antes de escrever código, inspecione via script de Editor e registre: hierarquia, escala real (bounds dos renderers), colliders, Animator/Avatar, nomes reais dos clips, número de keyframes e se o clip realmente se move. Atenção aos pontos que costumam enganar:

- **Escalas muito diferentes entre prefabs** (ex.: um prefab pode medir ~16 m e outro ~0,3 m). Ajuste cada wrapper à escala dos personagens (policial e fugitivo com ~1,75 m de altura).
- **Personagens podem não ter Animator, collider nem Animator Controller.** Crie o que faltar no wrapper.
- **Clips Mixamo costumam se chamar `mixamo.com` e vir sem loop.** Crie cópias em `Assets/Animations` com loop nos ciclos (parado, correndo, abaixado). Na cópia do pulo, remova a subida do quadril (o controlador já faz a altura).
- **Verifique se o clip do fugitivo é mesmo uma corrida** (as pernas variam de rotação? tem mais de 3 keyframes?). Se o modelo do fugitivo trouxer só uma pose parada e os dois esqueletos forem Mixamo com os mesmos ossos, gere a corrida do fugitivo a partir de uma cópia da corrida do policial, ajustando a altura do quadril. Confirme em Play Mode que o estado "Correndo" toca em loop e as pernas se movem.
- **Portas do elevador:** descubra quais objetos são as portas (ex.: `PortaEsquerda`/`PortaDireita`). Se forem SkinnedMeshRenderer com blendshape de abertura, anime o peso do blendshape; senão, mova as portas.
- **Orientação dos obstáculos:** verifique por captura de tela que a frente de cada modelo fica visível para a câmera (o rádio, por exemplo, não pode aparecer de costas nem de perfil). O avião voa de nariz para a frente na direção do movimento.
- Use os colliders originais dos obstáculos como volume de impacto (como trigger); não duplique colliders.

## 3. Cenário: loja de departamentos em blocos de tela

Monte a loja com primitivos da Unity (pisos, paredes de fundo, lajes, acabamentos simples), materiais URP simples e iluminação coerente com o pipeline instalado (não migre o pipeline).

- **Três andares internos + terraço**, com 4 m entre andares e lajes de 0,3 m. Um bloco escuro de fundação abaixo do térreo.
- **Plano de jogo em Z = 0.** O corredor da frente de cada laje é contínuo (sem buracos). Todas as passagens verticais (escadas rolantes e poço do elevador) ficam numa faixa de fundo atrás do corredor (Z ≈ 1,7 a 4,2), com aberturas reais nas lajes só nessa faixa. Assim o jogador nunca cai num buraco.
- **Blocos de tela, como no original:** cada andar tem **7 blocos de tela** — o central e **3 para cada lado**. Use um corredor de 96 m (x de −48 a +48), com blocos de ~13,7 m.
- **Elevador no bloco central** (x = 0), atendendo apenas os três andares internos.
- **Escadas rolantes nos blocos das pontas, alternando o lado:** térreo→1º à direita, 1º→2º à esquerda, 2º→terraço à direita (entrada em x ≈ ±38). O acesso ao terraço é só pela escada.
- **Ponto de fuga no terraço**, na ponta oposta à chegada da escada (x ≈ −47), sinalizado com bandeira/placa.
- Paredes laterais nas pontas, nenhuma parede na frente da câmera, prateleiras e placas de andar como decoração.

## 4. Câmera

- Ortográfica, lateral, Z fixo, leve inclinação (~8°) para leitura 2.5D.
- **Câmera em blocos (flip-screen):** a largura visível é exatamente um bloco (ajuste o `orthographicSize` pela proporção da tela). A câmera fica parada mostrando o bloco do policial e **troca de uma vez** para o bloco vizinho quando ele passa da borda (corte instantâneo; deixe uma opção de transição curta no Inspector).
- Em Y, a câmera acompanha o andar do policial com uma transição curta, mantendo-o sempre visível (também durante escadas e elevador).

## 5. Policial (jogador)

Controles via Input System: A/D ou ←/→ correr; Espaço pular; S/↓ abaixar; W/↑ entrar no elevador (portas abertas); S/↓ sair do elevador (portas abertas); Esc pausar. Aceite gamepad também. A classe de input deve rodar antes das demais (`DefaultExecutionOrder`) e ter entradas de depuração para testes automatizados.

- Apenas `CharacterController` (sem Rigidbody, sem root motion). Movimento só em X/Y, Z travado.
- **Corrida 8 m/s**, salto de 1,6 m só a partir do chão (sem salto duplo), gravidade ~30 m/s², detecção de chão confiável (CharacterController + SphereCast).
- Vira o visual para a direção da corrida. Abaixar reduz o collider de 1,7 m para 1,0 m e impede andar; só levanta se houver espaço acima.
- Animator Controller com estados Parado, Correndo, Pulando e Abaixado (parâmetros Speed, Grounded, Crouch). O pulo funciona sem animação de queda.
- **Começa no bloco da ponta esquerda do térreo** (x ≈ −41), a 3 blocos do elevador. Nunca o coloque ao lado do elevador nem da escada.
- Após um impacto: proteção de ~1,5 s com o modelo piscando, sem precisar de clip novo.

## 6. Escadas rolantes

- Use o prefab existente escalado para vencer 4 m de altura, sem colliders bloqueando o caminho.
- Transporte automático por uma trajetória com pontos ajustáveis no Inspector (entrada no corredor → início da rampa → fim da rampa → saída no corredor), velocidade constante, sem teletransporte e sem ativação repetida durante o percurso. Desligue o CharacterController durante o transporte e devolva o controle no fim.
- **Para o policial, só sobem.** Ele entra ao encostar na entrada inferior andando em direção a ela.
- **Escadas de descida exclusivas do fugitivo**, ao lado das de subida (atrás, com material de cor diferente e placa "SERVIÇO"). O policial não é afetado por elas.
- **A velocidade do fugitivo na escada nunca pode ser maior que a do policial** (ex.: policial 3,2 m/s, fugitivo 3,0 m/s).

## 7. Elevador

- Separe cabine móvel (o prefab), portas (dentro do prefab) e elementos fixos (poço, trilhos, molduras de primitivos).
- Ciclo automático: chegar → abrir portas → esperar ~2,6 s → fechar → viajar, subindo e descendo entre os três andares internos.
- **Posição inicial:** a cada rodada (e a cada tentativa), a cabine começa parada no **2º andar com as portas fechadas**, espera ~1 s e **desce até o térreo**. Calibre para que o policial, correndo direto do ponto inicial, chegue com as portas abertas e consiga subir (portas abertas no térreo de ~3,4 s a ~6,0 s; o policial chega em ~5 s). Quem demorar perde o elevador. Deixe andar inicial e espera configuráveis no `GameConfig`.
- O policial entra só se estiver em frente à porta, com a cabine alinhada ao andar e as portas abertas. Dentro da cabine: segue a cabine, sem gravidade nem colisões, corrida e salto bloqueados. Sai quando as portas abrem, num ponto seguro do corredor.
- Ajuste a luz que vier dentro do prefab para não estourar a cena (altere a instância, não o original).

## 8. Fugitivo (IA determinística)

- Corre continuamente com a animação de corrida (ritmo da animação proporcional à velocidade), Z fixo, sem colisões, sem usar o elevador.
- Rota: sobe pelas escadas rolantes até o terraço e foge pela ponta sinalizada (perde-se uma vida).
- Reage ao policial: se ele estiver no mesmo andar bloqueando o caminho, inverte a direção e usa a descida exclusiva; não sobe se o policial estiver esperando perto da saída da escada no andar de cima; patrulha o corredor quando não pode avançar. Tempo mínimo entre inversões para não tremer.
- Limite o deslocamento por frame (quedas de framerate não podem fazê-lo "pular").
- **Velocidade fixa em todas as rodadas: 4,0 m/s** (50% da do policial). Ela **não muda** com a rodada. Fica num único campo do `GameConfig`, não na tabela de dificuldade. Mesmo assim, trave a velocidade no próprio script do fugitivo (setter) em no máximo **80% da velocidade do policial**, para valer mesmo que alguém edite o valor.
- **Captura:** mesmo andar, nenhum dos dois em transporte e distância horizontal ≤ 0,9 m. Nunca captura através da laje.

## 9. Obstáculos

Wrappers dos prefabs originais, escalados à altura do policial, com o collider original como trigger:

| Obstáculo | Comportamento | Como evitar | Penalidade |
|---|---|---|---|
| Carrinho (~0,9 m) | Rola no chão | Saltar | −9 s |
| Bola (~0,55 m) | Quica em parábola determinística; baixa nas primeiras rodadas, alta nas avançadas | Saltar (baixa) / passar abaixado (alta) | −9 s |
| Rádio (~0,6 m) | Parado no chão, frente para a câmera | Saltar | −9 s |
| Avião | Voa com a base a ~1,15 m do piso (acerta o policial em pé, passa sobre ele abaixado) | Abaixar | Perde uma vida |

**Regra de geração (importante):**
- Os obstáculos móveis pertencem ao **bloco de tela em que o policial está**.
- Ao **entrar em um bloco** (ou trocar de andar), remova os obstáculos móveis do bloco anterior e, ~0,5 s depois, gere a onda **fora da tela, na ponta do bloco mais distante do policial**, vindo em direção a ele. Nos blocos das pontas do mapa, o obstáculo nasce escondido dentro da parede lateral.
- **Carrinhos da mesma onda vêm colados**, a ~1,5 m um do outro (campo próprio na tabela, separado do espaçamento das bolas e aviões). Assim o policial correndo consegue **pular dois ou três carrinhos num único pulo**.
- Ondas seguintes também saem da ponta mais distante. Quando o obstáculo atravessa o bloco e sai, ele é reciclado (pool, sem acúmulo).
- **Nunca gere um obstáculo na borda pela qual o policial acabou de entrar**, nem visível na tela, nem dentro das escadas ou na frente do elevador.
- **Rádios** são estáticos: posicionados no início da tentativa, longe do policial, das escadas, do elevador e a pelo menos 3 m das bordas dos blocos.
- Movimento previsível de arcade, sem física aleatória.

## 10. Rodadas, cronômetro e pontuação

- Configurações centralizadas em ScriptableObjects editáveis: `GameConfig` (velocidades, inclusive a velocidade fixa do fugitivo, salto, tempos, elevador, proteção, câmera, pontuação) e `DifficultySettings` (tabela por rodada: tipos permitidos, obstáculos por onda, intervalo, espaçamento da onda e dos carrinhos, velocidades dos obstáculos, altura do quique, rádios por andar, coletáveis).
- Dificuldade crescente só nos obstáculos: rodada 1 só carrinhos → rádios → bolas baixas → aviões → ondas maiores → bolas altas → mais rápido. Depois da última linha da tabela, continue aumentando com limites. O fugitivo não acelera.
- **Cronômetro garantindo a fuga:** calcule a cada rodada o tempo que o fugitivo leva do início até o ponto de fuga sem ser incomodado (distâncias reais dos corredores + comprimento das trajetórias das escadas ÷ velocidades). O tempo da rodada é `max(50 s, tempo de fuga + 6 s)`, o que dá ~69 s com o fugitivo a 4 m/s. Assim o fugitivo sempre consegue fugir antes do tempo acabar, e o jogador perde a vida pela fuga, não por um cronômetro curto demais. Deixe opção para desligar e usar 50 s fixos.
- Captura: encerra a rodada uma única vez, soma pontos, limpa obstáculos, reposiciona personagens, reinicia o cronômetro e avança a rodada.
- Pontuação: tempo restante (arredondado para cima) × 100 nas rodadas 1–8, × 200 nas 9–16, × 300 depois.
- Vidas: 1 policial ativo + 3 reservas (configurável). Perde-se uma vida com o avião, o tempo esgotado ou a fuga pelo terraço; a tentativa da rodada atual reinicia. Sem reservas → Game Over. Vida extra a cada 10.000 pontos, até 3 reservas.
- Coletáveis opcionais (maleta e saco de dinheiro com primitivos), 50 pontos.
- Estados explícitos da partida (Título, Jogando, Pausado, Rodada vencida, Vida perdida, Game Over) e do policial (Normal, Escada, Elevador, Congelado) para evitar conflitos.

## 11. Interface

- HUD discreto: pontuação, cronômetro (destaque pulsando nos últimos 10 s), rodada, reservas em ícones e mensagens centrais ("RODADA N", "CAPTURADO!", "TEMPO ESGOTADO!", "O FUGITIVO ESCAPOU!", "−9s").
- Telas: inicial com instruções e botão Jogar; pausa com Continuar e Reiniciar; Game Over com pontuação e Jogar novamente. Use EventSystem com o módulo do Input System. Ao entrar em jogo, limpe a seleção de UI (Espaço não pode acionar um botão escondido).
- Minimapa gerado a partir dos dados reais da fase: três andares e terraço, escadas de subida e de descida (cores diferentes), poço e cabine do elevador, ponto de fuga, policial e fugitivo em tempo real.

## 12. Testes obrigatórios em Play Mode

Escreva cenários automatizados (async, rodando no Play Mode pela CLI) que imprimam PASS/FAIL. Quando um teste falhar, investigue se o erro está no jogo ou no próprio teste antes de mudar o jogo. Cubra:

1. Corrida, virada, pulo parado e em movimento, sem salto duplo, abaixar/levantar com e sem espaço, parede lateral e transições de animação (inclusive a do fugitivo correndo em loop).
2. Escada: início ao encostar, trajetória contínua, sem reativação, chegada ao andar certo, terraço só pela escada, descida exclusiva não afeta o policial.
3. Elevador: começa no 2º andar com portas fechadas e desce; o policial correndo direto do início consegue entrar no térreo e subir; ciclo completo, entrada só com porta aberta, viagem, saída, corredor sem queda na frente do poço.
4. Cada obstáculo: penalidade exata, proteção contra penalidade repetida, pode ser evitado (salto, ou abaixado contra avião e bola alta); avião em pé tira vida; dois carrinhos seguidos (e três nas rodadas avançadas) são vencidos com um único pulo correndo.
5. Blocos: a tela mostra exatamente um bloco; a câmera troca de uma vez na borda; são 3 blocos do centro até a escada; ao entrar em um bloco o obstáculo nasce fora da tela na ponta oposta e vem em direção ao policial, sem impacto logo na entrada (testar nos dois sentidos).
6. Fugitivo: rota completa até a fuga, reação ao bloqueio, sem teletransporte; em todas as rodadas (inclusive 20, 30, 60) a velocidade é a mesma (4 m/s), menor que a do policial, e o cronômetro é maior que o tempo de fuga; fuga real nas rodadas 1 e 8 acontece antes do tempo acabar; o policial alcança o fugitivo numa perseguição no mesmo andar.
7. Partida: captura e pontuação, rodada encerrada uma vez, reinício da rodada, tempo esgotado, pausa (cronômetro parado), vida extra, Game Over e jogar novamente.
8. Capturas de tela para conferir visualmente: tela inicial, elevador aberto, obstáculos (rádio de frente), fugitivo correndo, minimapa.

Verifique o Console (sem erros nem avisos dos seus scripts) e salve a cena e os assets.

## 13. Entrega

Ao terminar, informe: cena principal criada, controles, scripts e recursos adicionados, o que foi encontrado nos assets e como foi resolvido, resultado dos testes (com números) e limitações restantes.
