# Binder Pokédex

App web (Blazor WebAssembly, .NET 11 / C# 15) para montar a Pokédex Nacional num binder 4x4 de **1024 posições**
(32 folhas × 2 lados × 16 bolsos). Publicado no GitHub Pages: <https://albertomonteiro.github.io/binder-organizer/>.

- Ordem da Pokédex Nacional, **#1 a #1024**. **Pecharunt (#1025)** fica de fora.
- Pesquise por nome ou número e veja **geração, folha, lado (frente/verso), linha, coluna e página**,
  se você já tem a carta e quantas folhas virar a partir de onde o binder está aberto.
- Visualize o binder aberto como na vida real (verso de uma folha | frente da próxima); no celular, uma página por vez.
- Marque o que já colocou e o tipo da carta (normal, foil, ultra rara, arte rara); o progresso
  (geral, por geração e por tipo) fica num **gist secreto** da sua conta do GitHub.

## Sincronização (gist)

- Em cada aparelho, abra ⚙ e cole um token do GitHub com a permissão **gist**
  ([criar token](https://github.com/settings/tokens/new?scopes=gist&description=Binder%20Pok%C3%A9dex)).
  O token fica só no `localStorage` daquele aparelho.
- O app procura na sua conta o gist com o arquivo `binder-pokedex.json`; se não existir, cria um (secreto).
- Cada alteração relê o gist antes de gravar, então o que outro aparelho marcou não se perde.
  O arquivo tem um número de revisão para ignorar leituras atrasadas da API.
- **Offline**: o service worker guarda o app e o aparelho guarda a última cópia da coleção.
  Sem internet o app abre normalmente, mas fica **só leitura**. As imagens ficam guardadas conforme são vistas.

Formato do arquivo:

```json
{ "revision": 12, "updatedAt": "2026-11-15T19:39:04Z", "cards": { "6": "ArteRara", "25": "Normal" } }
```

## Como a posição é calculada

Para o Pokémon `n` (1..1024), com `i = n - 1`:

| | Fórmula |
|---|---|
| Folha | `i / 32 + 1` |
| Lado | `(i % 32) / 16` → 0 = frente, 1 = verso |
| Linha | `(i % 16) / 4 + 1` |
| Coluna | `(i % 16) % 4 + 1` |
| Página | `(folha - 1) * 2 + lado + 1` |

Exemplo: Chikorita (#152) → Folha 5 · Verso · Linha 2 · Coluna 4 · Página 10.

## Rodando localmente

```bash
dotnet run --project src/BinderPokedex
```

## Publicação

Cada push na `main` roda os testes e publica no GitHub Pages (`.github/workflows/pages.yml`).
O `index.html` ajusta o `<base href>` sozinho quando está em `*.github.io/<repositório>/`.

## Dados

`src/BinderPokedex/Data/pokemon.json` (número, nome em inglês, geração e tipos) foi gerado da
[PokeAPI](https://pokeapi.co) e vai embutido no app. Para regenerar:

```bash
dotnet run --project tools/PokedexDataGen
```

As imagens vêm do repositório de sprites da PokeAPI no GitHub.

## Testes

```bash
dotnet test
```
