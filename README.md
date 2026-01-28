# Rollout por percentual — Outbox Feature Flag

Console .NET que demonstra e documenta a **lógica de rollout gradual** usada para decidir se cada mensagem deve ir para o **Outbox** ou manter o comportamento atual. O mesmo mecanismo pode ser usado em qualquer serviço para liberar uma nova feature em produção aos poucos, por exemplo 10% → 25% → 50% → 100%.

---

## Índice

1. [O que é e para que serve](#1-o-que-é-e-para-que-serve)
2. [Como funciona a decisão (passo a passo)](#2-como-funciona-a-decisão-passo-a-passo)
3. [O algoritmo do percentual (hash)](#3-o-algoritmo-do-percentual-hash)
4. [Tabela no banco e colunas](#4-tabela-no-banco-e-colunas)
5. [Como rodar o demo](#5-como-rodar-o-demo)
6. [O que o demo mostra](#6-o-que-o-demo-mostra)
7. [Estrutura do projeto](#7-estrutura-do-projeto)
8. [Uso em produção](#8-uso-em-produção)
9. [Kubernetes / múltiplos pods](#9-kubernetes--múltiplos-pods)

---

## 1. O que é e para que serve

- **Feature flag de Outbox**: uma configuração no banco que controla se as mensagens de um processo usam o **padrão Outbox** (gravar na tabela de outbox e publicar assincronamente) ou o **fluxo atual** (publicar direto, etc.).
- **Rollout por percentual**: em vez de ligar a feature para *todos* de uma vez, você define um **percentual** (ex.: 25%). Apenas essa fração das mensagens usa Outbox; o restante continua no comportamento antigo.
- **Objetivo**: reduzir risco em produção. Você sobe 10%, observa; depois 25%, 50%, e por fim 100%, podendo reverter alterando só o registro no banco.

O console demonstra exatamente essa lógica: como a flag é lida, como o percentual é aplicado e como a decisão “Outbox sim/não” é tomada para cada `Id`.

---

## 2. Como funciona a decisão (passo a passo)

Para cada mensagem (ou processo), a aplicação chama algo como:

```csharp
var usarOutbox = VerificarFeatureFlagOutbox(id, featureFlag);
```

A decisão segue esta ordem:

| # | Condição | Resultado |
|---|----------|-----------|
| 1 | `featureFlag` é `null` | **Não** usa Outbox |
| 2 | `Ativo == false` | **Não** usa Outbox |
| 3 | `DataFim` existe e já passou (`DataFim < agora`) | **Não** usa Outbox |
| 4 | `PercentualVolume >= 100` | **Sim**, usa Outbox (100% do tráfego) |
| 5 | Caso contrário | Usa o **percentual**: calcula um “hash” do `Id` e decide com base em `PercentualVolume` |

Ou seja: primeiro verificamos se a feature está ligada e dentro do período; depois, se não for 100%, aplicamos o rollout por percentual.

---

## 3. O algoritmo do percentual (hash)

Quando o percentual é menor que 100%, precisamos decidir **quais** processos usam Outbox de forma **estável**: o mesmo `Id` deve **sempre** cair no mesmo lado (Outbox sim ou não), mesmo com várias chamadas ou reinícios da aplicação.

### Fórmula

```
hash         = |Id.GetHashCode()|   // inteiro não negativo
percentualHash = (hash % 10000) / 100       // valor entre 0.00 e 99.99
```

- `hash % 10000` gera um número de 0 a 9999.
- Dividir por 100 gera um “percentual simulado” entre **0.00** e **99.99**, específico daquele `Id`.

### Regra de decisão

```
Usa Outbox?  →  percentualHash < PercentualVolume
```

Exemplos:

- `PercentualVolume = 25.00`: só usam Outbox os processos cujo `percentualHash` é **menor que 25** (ex.: 0.00, 12.34, 24.99).
- `PercentualVolume = 50.00`: usam os com `percentualHash < 50`.
- `PercentualVolume = 100.00`: nem chegamos nessa conta; todos usam Outbox (ver passo 4 acima).

### Por que usar o `Id`?

- **Consistência**: o mesmo id sempre produz o mesmo `percentualHash`, então sempre recebe a mesma decisão — inclusive **entre pods** (Kubernetes, etc.): não há aleatório nem estado local. Ver [§ 9. Kubernetes / múltiplos pods](#9-kubernetes--múltiplos-pods).
- **Distribuição**: muitos `Id`s distintos tendem a gerar hashes bem espalhados entre 0 e 9999, e a proporção dos que ficam abaixo de `PercentualVolume` se aproxima do percentual configurado (ex.: ~25% quando `PercentualVolume = 25`).

### Resumo visual

```
Id  →  GetHashCode()  →  |hash|  →  % 10000  →  / 100  →  percentualHash (0.00–99.99)
                                                                         ↓
                                                    percentualHash < PercentualVolume?
                                                                         ↓
                                                              Sim → Outbox
                                                              Não → comportamento atual
```

---

## 4. Tabela no banco e colunas

A configuração fica em `[dbo].[OutboxFeatureFlag]`. Em geral usa-se **um único registro** (por exemplo o de `Id = 1`).

| Coluna | Tipo | Descrição |
|--------|------|-----------|
| `Id` | `int` | Chave primária. |
| `Ativo` | `bit` | `1` = feature ligada; `0` = desligada. Se `0`, ninguém usa Outbox por essa flag. |
| `PercentualVolume` | `decimal(5,2)` | Percentual do tráfego que usa Outbox (`0.00` a `100.00`). |
| `DataInicio` | `datetime2` | Opcional. Início do período de rollout. |
| `DataFim` | `datetime2` | Opcional. Se preenchido e **já passou**, a feature é considerada “expirada” e ninguém usa Outbox. |
| `Observacao` | `nvarchar(500)` | Texto livre (ex.: “Rollout 25% em produção”). |
| `DataUltimaAtualizacao` | `datetime2` | Última alteração. |
| `UsuarioAtualizacao` | `varchar(100)` | Quem alterou. |

O script completo está em `Scripts/OutboxFeatureFlag.sql`. Exemplo de insert inicial:

```sql
INSERT INTO [dbo].[OutboxFeatureFlag] ([Ativo], [PercentualVolume], [Observacao])
VALUES (0, 0.00, 'Feature flag inicial - Outbox Pattern');
```

---

## 5. Como rodar o demo

Requisito: **.NET 8**.

```bash
cd RolloutDemo
dotnet run
```

- **Modo interativo**: sem argumentos. O programa imprime todas as seções e, no final, pede um percentual (0–100). Você digita, vê o resultado da simulação e pode repetir. **Enter** vazio encerra.
- **Modo só demonstração** (sem input):  
  `dotnet run -- --no-input`

---

## 6. O que o demo mostra

O console executa, em sequência:

1. **Explicação do `percentualHash`**  
   Mostra a fórmula e um exemplo com um `Id` e o valor `0.00–99.99` correspondente.

2. **Simulação com 1000 `Id`s**  
   Para percentuais 0%, 10%, 25%, 50%, 75% e 100%, conta quantos “iriam para Outbox” e qual o percentual real obtido. Você verá que os números se aproximam dos percentuais configurados.

3. **Consistência**  
   Para o mesmo `Id`, chama a verificação duas vezes e mostra que o resultado é sempre o mesmo.

4. **Regras extras**  
   Demonstra que, com `Ativo = false` ou `DataFim` no passado, a decisão é sempre “não usa Outbox”.

5. **Amostra detalhada**  
   Lista alguns `Id`s com seu `percentualHash` e a decisão (Outbox sim/não) quando `PercentualVolume = 30%`.

6. **Interativo (se não usar `--no-input`)**  
   Você informa um percentual e o programa recalcula sobre os mesmos 1000 processos, mostrando quantos usariam Outbox.

Assim você entende na prática o fluxo da feature flag e do rollout por percentual.

---

## 7. Estrutura do projeto

| Item | Função |
|------|--------|
| `Models/OutboxFeatureFlag.cs` | Modelo que espelha a tabela `OutboxFeatureFlag`. |
| `Services/RolloutService.cs` | Contém `VerificarFeatureFlagOutbox` e `ObterPercentualHash` (lógica de rollout). |
| `Repositories/FeatureFlagRepository.cs` | Repositório em memória para o demo (em produção viria do banco). |
| `Program.cs` | Orquestra as simulações e o modo interativo. |
| `Scripts/OutboxFeatureFlag.sql` | Script de criação da tabela e insert inicial. |

---

## 8. Uso em produção

No seu serviço:

1. Buscar a feature flag do banco (ex.: `ObterFeatureFlagOutboxAsync`).
2. Para cada mensagem/processo, chamar `VerificarFeatureFlagOutbox(id, featureFlag)`.
3. Se retornar `true`, usar o fluxo **Outbox**; se `false`, manter o **comportamento atual**.

Para fazer rollout gradual, basta atualizar o registro no banco:

- `Ativo = 1`, `PercentualVolume = 10` → 10% em Outbox.
- Depois 25, 50, 75 e por fim 100 quando estiver estável.

Não é necessário deploy para mudar o percentual; apenas alterar `OutboxFeatureFlag` e a aplicação passa a usar os novos valores na próxima leitura da configuração.

---

## 9. Kubernetes / múltiplos pods

**Sim.** A abordagem funciona em Kubernetes com vários pods sem ajustes.

### Por quê?

A decisão é **determinística**: depende só de `Id` e da `featureFlag` (PercentualVolume, Ativo, etc.). Não há:

- uso de aleatório;
- identidade do pod (hostname, etc.);
- timestamp;
- estado em memória local.

O `percentualHash` vem de `Id.GetHashCode()` e operações matemáticas fixas. Em .NET, `Guid.GetHashCode()` é determinístico (baseado nos bytes do Guid).

### Consequência

O **mesmo `Id` sempre gera a mesma decisão**, em qualquer pod que processar a requisição:

| Requisição | Pod    | Id  | Decisão   |
|------------|--------|-----|-----------|
| 1          | Pod A  | X   | Outbox    |
| 2          | Pod B  | X   | Outbox    |
| 3          | Pod A  | Y   | atual     |

Não é necessário sticky session. O load balancer pode distribuir livremente; a consistência é garantida pelo `Id`.

### O que precisa ser igual em todos os pods

- **Feature flag**: todos os pods devem ler a mesma configuração (ex.: mesmo banco `OutboxFeatureFlag`). Em geral a flag já vem de um repositório compartilhado.
- **Id**: o identificador usado na decisão deve ser o mesmo para a mesma entidade (ex.: id do processo/mensagem), independente do pod.

Se isso estiver garantido, o rollout por percentual se comporta corretamente em múltiplos pods.
