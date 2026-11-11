# Nicotório

Protótipo de jogo de automação em visão de cima, feito em **Unity 6** (URP 2D). Você minera ferro na mão, monta mineradoras, liga tudo com esteiras e junta a produção em baús.

![Fábrica com mineradoras alimentando uma esteira que desce até um baú](docs/images/fabrica.png)

## O que tem

- **Mapa 64x64** com manchas de minério de ferro, geradas a partir de uma seed (o mapa é sempre o mesmo).
- **Mineração manual**: segure o botão direito sobre um minério próximo para ir enchendo o inventário.
- **Mineradora**: só pode ser colocada em minério. Produz 1 minério por segundo e entrega no tile para onde a seta aponta. Se não tiver onde entregar, ela segura o item e para.
- **Esteira**: leva os itens na direção da seta e entrega na próxima esteira ou num baú. Quando uma esteira enche, ela trava a de trás.
- **Baú**: guarda tudo o que chega. Clique nele para pegar o conteúdo.

| Mineração manual | Esteira cheia trava a de trás |
|---|---|
| ![Jogador minerando um tile de minério, com a barra de progresso](docs/images/mineracao-manual.png) | ![Linha com curva entregando num baú e uma linha travada sem saída](docs/images/esteiras.png) |

| Conteúdo do baú ao passar o mouse |
|---|
| ![Painel no canto mostrando o conteúdo do baú](docs/images/bau.png) |

## Controles

| Tecla | Ação |
|---|---|
| **WASD** | andar |
| **Botão direito** (segurar) | minerar o minério sob o mouse (alcance de 3 tiles) |
| **1 / 2 / 3** | escolher mineradora / esteira / baú (aperte de novo para desmarcar) |
| **R** | girar a construção |
| **Botão esquerdo** | construir; segure e arraste para fazer uma linha |
| **Botão esquerdo no baú** | pegar tudo o que está nele |
| **X** | remover a construção sob o mouse; o que ela tinha volta para você |

## Como rodar

1. Abra a pasta no **Unity 6000.6.8f1**.
2. Abra a cena `Assets/Scenes/Factory` e dê **Play**.

A cena está vazia de propósito: a câmera, o mapa, o jogador, a interface e todos os sprites são criados por código quando o jogo começa. Não há arte importada.

## Como foi feito

- **Simulação separada da apresentação.**
  - A simulação (`Assets/Scripts/Simulation`) é C# puro, sem `UnityEngine`. Ela roda num tick fixo de 20 por segundo e é determinística.
  - A apresentação (`Assets/Scripts/Presentation`) só desenha o estado da simulação e transforma o input em comandos.
- **Os itens nas esteiras são dados**, não GameObjects: cada item tem uma posição inteira ao longo da esteira, e a apresentação só desenha esses itens.
- **Há um único ponto de entrada**, o `GameBootstrap`, que cria a simulação e entrega para quem precisa. Não há singletons espalhados.

Detalhes da arquitetura e das convenções estão em [`CLAUDE.md`](CLAUDE.md).

## Testes

Os testes EditMode (`Assets/Tests/EditMode`) cobrem:
- geração do mapa;
- mineração manual;
- regras de construção;
- ritmo da mineradora;
- movimento e bloqueio das esteiras;
- acúmulo no baú.

Para rodar, abra no Unity **Window > General > Test Runner > EditMode > Run All**. Saia do Play mode antes.
