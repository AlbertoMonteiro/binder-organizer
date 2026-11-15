# Binder Pokédex

App web (Blazor, .NET 11 / C# 15) para montar a Pokédex Nacional num binder 4x4 de **1024 posições**
(32 folhas × 2 lados × 16 bolsos).

- Ordem da Pokédex Nacional, **#1 a #1024**. **Pecharunt (#1025)** fica de fora.
- Pesquise por nome ou número e veja **geração, folha, lado (frente/verso), linha, coluna e página**.
- Visualize o binder aberto como na vida real (verso de uma folha | frente da próxima); no celular, uma página por vez.
- Marque o que já colocou; o progresso (geral e por geração) fica salvo em SQLite (`binder.db`)
  e é atualizado ao vivo em todos os aparelhos conectados.

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

## Rodando

```bash
dotnet run --project src/BinderPokedex
```

O app escuta em `http://0.0.0.0:5150` (todas as interfaces). No PC: <http://localhost:5150>.

### Acessando pelo celular

1. Descubra o IP do PC na rede local:
   ```powershell
   ipconfig
   ```
   (procure o "Endereço IPv4" do adaptador Wi-Fi/Ethernet, ex.: `192.168.0.10`).
2. Libere a porta no Firewall do Windows (PowerShell como administrador, só redes privadas):
   ```powershell
   New-NetFirewallRule -DisplayName "Binder Pokedex" -Direction Inbound -LocalPort 5150 -Protocol TCP -Action Allow -Profile Private
   ```
3. No celular, conectado à mesma rede: `http://192.168.0.10:5150`.

> A rede do PC precisa estar como **Privada** no Windows para a regra acima valer.

### Publicando para deixar rodando

```bash
dotnet publish src/BinderPokedex -c Release -o publish
```

Depois rode `publish\BinderPokedex.exe`. O banco fica ao lado do executável; para mudar o local,
defina `Binder:DatabasePath` no `appsettings.json` (ex.: `C:\\Users\\voce\\binder.db`).

## Dados

`src/BinderPokedex/Data/pokemon.json` (número, nome em inglês, geração e tipos) foi gerado da
[PokeAPI](https://pokeapi.co) e vai embutido no app. Para regenerar:

```bash
dotnet run --project tools/PokedexDataGen
```

As imagens vêm do repositório de sprites da PokeAPI no GitHub (precisa de internet).

## Testes

```bash
dotnet test
```
