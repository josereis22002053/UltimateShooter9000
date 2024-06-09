# Projeto de Sistemas de Redes para Jogos - [2023/2024]

## Autoria

José Reis 22002053

## Descrição Técnica

Neste projeto foi desenvolvido um jogo de ação, cliente/servidor, com _login/matchmaking_. O jogo é
um _top-down shooter_ em que dois jogadores se enfrentam numa partida. Cada partida acaba quando um
dos jogadores atinge o número necessário de _kills_ ou quando um dos jogadores abandona a partida.

Ao arrancar a aplicação é determinado se esta será um servidor ou um cliente através dos argumentos
passados durante a execução da mesma. Se não forem passados nenhuns argumentos a aplicação iniciará
como cliente. Se for passada a _flag_ `--server` a aplicação será um servidor de _login/matcmaking_
e caso seja passada a _flag_ `--gameServer` a aplicação será um servidor para uma partida entre dois
jogadores, pelo que serão necessários outros dois argumentos, um correspondente ao _port_ do servidor
em si, e outro correspondente ao _port_ do servidor de _matchmaking_.

Uma aplicação cliente começa no ecrã de _login_, onde o utilizador terá de se registar ou iniciar
sessão. No caso do jogador se registar, o servidor verifica se os dados introduzidos pelo utilizador
são válidos e caso o sejam regista um novo utilizador na base de dados. Para os dados serem válidos,
estes terão de preencher os seguintes requisitos:

- O nome de utilizador tem de ter entre 3 a 20 caracteres.
- O nome de utilizador não pode já existir na base de dados.
- A palavra-passe tem de ter entre 5 a 20 caracteres e não possuir espaços.

Se algum dos requisitos não se verificar, o utilizador é alertado com a respectiva mensagem. Ao se
verificarem todos os requisitos, após registar o novo utilizador, é pedido ao mesmo que faça inicie
sessão.

Ao iniciar sessão as credenciais introduzidas pelo utilizador são verificadas no servidor. Os
requisitos para estas serem válidas são os seguintes:

- As credenciais são as de um utilizador já registado.
- As credenciais não são a de um utilizador que já tenha a sessão iniciada.

Após iniciar a sessão o utilizador poderá então procurar por uma partida.

Do lado do servidor, quando um utilizador começa a procurar por um adversário primeiro verifica-se
se algum dos utilizadores já na _queue_ é considerado um adversário válido. Caso isso não se
verifique, o utilizador que começou a procura é adicionado à _queue_.

Para dois utilizadores serem considerados como adversários válidos um do outro, a diferença entre o
_elo_ de cada um terá de ser menor ou igual ao valor estabelecido por um deles.

Quando um utilizador é adicionado à _queue_ este começa com valor aceitável inicial. À medida que o
tempo passa e esse utilizador não encontra um adversário válido, esse valor vai sendo incrementado
de modo a serem considerados jogadores com maiores diferenças de _elo_ como possíveis adversários.

Ao encontrar um par de utilizadores compatíveis, o servidor de _matchmaking_ retira esses jogadores
da _queue_ e arranca um servidor para uma nova partida, atribuindo-lhe um _port_ para a conexão com
os jogadores e um outro _port_ para enviar notificações para o servidor de _matchmaking_. Quando
este novo servidor se encontra pronto, notifica o servidor de _matchmaking_. Ao receber a notificação,
o servidor de _matchmaking_ manda os jogadores para o servidor da partida.

No final da partida o servidor responsável pela mesma atualiza os dados de cada jogador. Após os
jogadores se desconectarem da partida, o servidor responsável pela mesma desliga-se.
Antes de se desligar, este avisa o servidor de _matchmaking_ de modo atualizar os _ports_ disponíveis
para novas partidas.

## Técnicas Utilizadas

### _Netcode for GameObjects_

Para quase todas as coisas relacionadas com _networking_ foi utilizado o _Netcode for GameObjects_
(NGO). Os componentes disponibilizados pelo NGO foram utilizados para a sincronização das ações dos
jogadores. Para elementos atualizados constantemente ao longo de uma partida, como a _health_ de
cada jogador foram usadas `NetworkVariables`, registando métodos no evento `OnValueChanged` das
mesmas para sincronizar coisas como os valores apresentados nas interfaces dos utilizadores.

Para elementos que apenas sofrem alterações em certos momentos da partida foram utilizados métodos
`RCP` para fazer a sincronização dos mesmos. Este tipo de métodos também foi utilizado para o envio
de notificações entre os clientes e os servidor, como por exemplo a apresentação de mensagens de
erro.

### _Raw Sockets_

Apenas foram utilizados _Raw Sockets_ para o envio de notificações por parte dos servidores de
partidas para o servidor de _matchmaking_, mais especificamente para alertar quando estes estão
prontos para receber jogadores e quando se vão desligar.

### _SQLite_

Para a implementação da base de dados foi utilizado _SQLite_. Inicialmente comecei por implementar
a base de dados utilizando _JSON_, mas logo no início da implementação comecei a prever problemas
que poderiam surgir ao haver acessos concorrentes à base de dados. Por isso procurei por outra
solução e acabei por optar pelo _SQLite_.

A suas vantagens foram que a implementação é bastante simples, trata dos problemas que muito
provavelmente iriam surgir se tivesse optado pelo _JSON_ e permitiu-me ganhar experiência com uma
técnica que nunca tinha utilizado anteriormente.

A maior desvantagem de usar _SQLite_ é que sendo uma base de dados local torna mais difícil escalar
a aplicação.

## Descrição de Mensagens de Rede

Da parte que o NGO trata automaticamente grande parte das mensagens são relativas à sincronização dos
`Transforms` dos `NetworkObjects` presentes nas cenas. São também relevantes as mensagens
relacionadas com a atualização de `NetworkVariables`.

Quanto ao que trato manualmente, praticamente todas as mensagens são chamadas de métodos `RPC`. Mais
especificamente. Por classe estas são:

- `Bullet`

    Classe que representa um projétil disparado pelos jogadores.

  - `SyncClientsClientRpc()`

    Chamado quando uma bala é instanciada pelo servidor e executado pelos clientes. Sincroniza as
    variáveis da bala instanciada e destrói a bala local equivalente.

- `LoginManager`

    Classe responsável pela gestão de novos registos e inícios de sessão.

  - `CreateAccountServerRpc()`

    Chamado por um cliente e executado no servidor. Regista uma nova conta na base de dados se
    receber credenciais válidas.

  - `LoginServerRpc()`

    Chamado por um cliente e executado no servidor. Verifica se as credenciais dadas são válidas.
    Caso isso seja verdade permite ao utilizador iniciar sessão.

  - `LoginClientRpc()`

    Chamado pelo servidor e executado num cliente específico. Inicia a sessão no cliente.

  - `DisplayMessageClientRpc()`

    Chamado pelo servidor e executado num cliente específico. Apresenta uma mensagem no cliente.

- `Matchmaking`

    Classe responsável pelo _matchmaking_.

  - `AddPlayerToQueueServerRpc()`

    Chamado pelos clientes e executado no servidor. Adiciona o cliente que chamou o método à _queue_
    caso não esteja já presente na _queue_ um utilizador considerado como um adversário compatível
    com base na diferença de _elo_ entres os dois.

  - `UpdateMatchmakingStatusClientRpc()`

    Chamado pelo servidor e executado nos clientes especificados. Atualiza a informação acerca do
    estado atual do _matchmaking_.

  - `JoinMatchClientRpc()`

    Chamado pelo servidor e executado nos clientes especificados. Notifica dois clientes emparelhados
    durante o _matchmaking_ para que estes se juntem ao servidor da partida que foi lançado pelo
    _matchmaking_.

- `MatchManager`

    Classe responsável pela gestão de uma partida entre dois utilizadores.

  - `StartingGameClientRpc()`

    Chamado pelo servidor e executado nos clientes. Invoca os métodos que subscrevam ao evento
    acionado quando a partida vai ser iniciada.

  - `StartGameClientRpc()`

    Chamado pelo servidor e executado nos clientes. Invoca os métodos que subscrevam ao evento
    acionado quando a partida inicia.

  - `EndGameClientRpc()`

    Chamado pelo servidor e executado nos clientes. Invoca os métodos que subscrevam ao evento
    acionado quando a partida termina.

  - `SyncPlayersInfoUIClientRpc()`

    Chamado pelo servidor e executado nos clientes. Atualiza a interface de modo a mostrar as
    informações dos jogadores presentes na partida.

- `Player`

    Classe que representa um jogador.

  - `RequestBulletServerRpc()`

    Chamado pelos clientes e executado no servidor. Instancia uma bala no servidor e sincroniza as
    suas variáveis nos clientes.

  - `InitializePlayerClientRpc()`

    Chamado pelo servidor e executado nos clientes. Inicializa as variáveis de um jogador.

  - `SyncClientInfoServerRpc()`

    Chamado pelos clientes e executado no servidor. Sincroniza as informações acerca dos jogadores.

  - `SetPositionClientRpc()`

    Chamado pelo servidor e executado nos clientes. Atualiza a posição dos jogadores quando fazem
    _respawn_.

## Diagrama de Arquitectura de Rede

![DiagramaRede](Img/Diagrama_Arquitectura_Rede.png)

## Bibliografia

- Slides e vídeos disponibilizados pelo professor

- ChatGPT para criar as _queries_ para acesso e manipulação da base de dados

- [Dotnet API](https://learn.microsoft.com/en-us/dotnet/api/?view=net-8.0)

- [Netcode RPC](https://docs-multiplayer.unity3d.com/netcode/1.0.0/advanced-topics/message-system/serverrpc/)

- [Unity 3d Tutorial: Chat box and message system](https://youtu.be/IRAeJgGkjHk?si=p3H2TJUFwj5JLuUv)

- [Using Sqlite in Unity](https://www.youtube.com/watch?v=8bpYHCKdZno)
