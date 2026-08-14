# Plano de Ação — HealthCheckPlus

Origem: `doc/healthcheckplus-audit.html` (auditoria de 14/08/2026).
Objetivo: executar as fases 0–3 do roadmap da auditoria como passos concretos, verificáveis e sequenciados por dependência real (não por facilidade). A Fase 4 (diferenciação) fica registrada em alto nível no final — só deve ser detalhada depois que as Fases 0–3 estiverem fechadas.

Este arquivo é o plano. O estado de execução (o que já foi feito, quando, em que commit, com que decisão tomada) vive em `doc/progresso-plano-acao.md`, no mesmo diretório. **Sempre atualize os dois juntos**: o plano descreve o que fazer, o progresso registra o que já foi feito e por quê.

## Convenção de referência

Os achados da auditoria citam `arquivo:linha`. Este plano cita **método/símbolo**, porque a Fase 0 já reestrutura o código citado (P0.3 consolida dois métodos em um) — números de linha ficariam obsoletos no meio da própria execução do plano. Quando um número de linha aparece aqui, é só para orientação inicial e vem marcado `(pré-Fase 0)`.

## Ordem de execução e dependências

```
Fase 0 (estabilização) ──┬─→ Fase 1 (estado por instância) ──┬─→ Fase 3 (suíte de integração real)
                          │                                    │        ↑ (P3.5 reaproveita P1.2)
                          └─→ Fase 2 (substituir ponte AddCheckLinkTo) ──┘
                                        │
                                        └─→ revisita P0.4 (P2.6)

Fase 4 (diferenciação) — só depois de Fases 0–3 fechadas.
```

Fase 1 e Fase 2 não dependem uma da outra e podem correr em paralelo depois que a Fase 0 fechar. A Fase 3 depende de ambas porque testa a integração final (DI + middleware) com o estado já escopado por instância (Fase 1) e a nova ponte de integração (Fase 2).

---

## Fase 0 — Estabilização (parar a sangria)

Meta: eliminar os defeitos confirmados pela auditoria com o menor redesenho possível, cada um amparado por um teste de regressão. Os testes desta fase usam o projeto de testes existente (`HealthCheckPlusTests`), que já tem `InternalsVisibleTo` para os assemblies `HealthCheckPlus`/`HealthCheckPlus.Abstractions` — não é preciso esperar pela infraestrutura de `WebApplicationFactory` da Fase 3 para testar `DefaultHealthCheckServicePlus` diretamente.

### P0.1 — Teste de regressão do achado crítico 1 (Degraded ignorado no caminho HTTP)
- **O quê**: escrever um teste que registra um check com `AddDegradedPolicy`, força o status para `Degraded`, invoca `CheckHealthPlusAsync` (caminho HTTP/foreground) e verifica que o período aplicado é o de Degraded, não o de Unhealthy.
- **Onde**: novo arquivo `src/HealthCheckPlusTests/DefaultHealthCheckServicePlusTests.cs`.
- **Estado esperado ao escrever**: o teste deve **falhar** contra o código atual — é a prova de que o bug existe antes de mexer em qualquer coisa.
- **Símbolo de referência**: `DefaultHealthCheckServicePlus.CheckHealthPlusAsync`, branch `case HealthStatus.Degraded` (pré-Fase 0: linha 98).

### P0.2 — Teste de regressão do achado crítico 2 (NRE com check sem política Healthy)
- **O quê**: registrar um check via `IHealthChecksBuilder` nativo (sem `AddCheckPlus`/`AddCheckLinkTo`) dentro de um `AddHealthChecksPlus`, e verificar o comportamento ao avaliar a saúde — hoje lança `NullReferenceException`.
- **Onde**: mesmo arquivo de P0.1.
- **Estado esperado ao escrever**: falha (exceção não tratada) contra o código atual.
- **Símbolo de referência**: `DefaultHealthCheckServicePlus.CheckHealthPlusAsync` e `BackGroudCheckHealthPlusAsync`, resolução da política `Healthy` via `_policies.Where(...).FirstOrDefault()!` (pré-Fase 0: linhas 76-79 e 232-236).
- **Depende de**: a decisão de P0.4 define qual deve ser o comportamento correto (o teste é escrito para o comportamento *desejado*, não apenas "não lança exceção").

### P0.3 — Consolidar a resolução de política em um método único
- **O quê**: extrair a lógica de "qual política/período aplicar dado o último status" de dentro de `CheckHealthPlusAsync` e `BackGroudCheckHealthPlusAsync` para um único método privado (ex.: `ResolveExecutionPolicy(HealthCheckRegistration item, ItemCacheHealth estadoAtual, ...)`), usado pelos dois caminhos. A correção do bug do Degraded (P0.1) é consequência desta consolidação, não um patch isolado — o objetivo é que a divergência entre os dois caminhos deixe de ser fisicamente possível.
- **Critério de aceite**: P0.1 passa a ficar verde; nenhuma outra regra de política muda de comportamento (cobrir com testes de caracterização dos casos Healthy/Unhealthy antes de mexer, se ainda não existirem).
- **Símbolo de referência**: `DefaultHealthCheckServicePlus.CheckHealthPlusAsync` (pré-Fase 0: linhas 60-194) e `BackGroudCheckHealthPlusAsync` (pré-Fase 0: linhas 196-298).

### P0.4 — Comportamento para check sem política Healthy registrada — **decisão pendente, bloqueia implementação**
Este passo não é um fix mecânico: é uma decisão de produto com duas alternativas reais, porque `AddHealthChecksPlus` substitui o `HealthCheckService` para **todo o processo** — qualquer check nativo (inclusive de bibliotecas que o consumidor não controla) passa a depender desta escolha.

- **Opção A — Falha explícita no startup.** Validar, dentro de `AddHealthChecksPlus` (ao lado de `ValidateRegistrations`), que todo check no `HealthCheckServiceOptions` final tem uma política `Healthy` correspondente; se não tiver, lançar uma exceção clara na inicialização, listando os nomes faltantes.
  - Risco: qualquer check registrado por uma biblioteca de terceiro que o time não controla vira um crash de startup até alguém adicionar o `AddCheckLinkTo` correspondente.
- **Opção B — Política padrão implícita com aviso.** Quando não houver política `Healthy` registrada, aplicar uma política padrão (ex.: os valores default de `HealthCheckPlusBackGroundOptions`) e logar um `Warning` identificando o check — o processo continua funcionando, mas de forma visível/auditável.
  - Risco: mascara configuração incompleta; o comportamento "funciona sozinho" pode esconder a existência do problema até alguém procurar nos logs.

**Recomendação para a Fase 0**: Opção B (padrão implícito + warning) — é a mudança mais segura para não quebrar consumidores existentes no meio de um ciclo de estabilização. **Revisitar esta decisão em P2.6**, depois que a Fase 2 redefinir como a adoção de checks de terceiros funciona — nesse ponto, a Opção A (fail-fast) pode fazer mais sentido, porque a nova API de adoção deve tornar o "esquecimento" menos provável.

- **Ação imediata**: registrar a decisão tomada (A ou B, e por quê) em `doc/progresso-plano-acao.md` antes de implementar P0.2/P0.4 — o teste de P0.2 depende de qual opção foi escolhida.

### P0.5 — Corrigir a chamada de Dispose por execução no WrapperBaseHealthCheckPlus
- **O quê**: remover a chamada `disposable.Dispose()` de dentro de `WrapperBaseHealthCheckPlus.CheckHealthAsync` — esta é a parte da correção que resolve o bug funcional (instância descartada sendo reutilizada) e deve entrar na Fase 0 porque é barata e não depende de mais nada.
- **O que NÃO faz parte deste passo**: garantir que a instância adotada seja descartada corretamente no encerramento do processo. Hoje ela vive em um dicionário `static` (`_externalCheck`) nunca enumerado para disposal — esse é um vazamento de recurso secundário (não uma corrupção funcional durante a operação) e sua correção própria depende de onde o dicionário passa a viver. **Este ponto fica formalmente adiado para P1.1**, que já move `_externalCheck` para dentro do container de DI — nesse momento, o dono do ciclo de vida (o container) passa a poder descartar as instâncias cacheadas no shutdown do host.
- **Símbolo de referência**: `WrapperBaseHealthCheckPlus.CheckHealthAsync` (pré-Fase 0: linhas 56-64).

### P0.6 — Trocar `DateTime.Now` por `DateTime.UtcNow`
- **O quê**: substituir todos os usos de `DateTime.Now` usados em comparação de agendamento por `DateTime.UtcNow`, em `CacheHealthCheckPlus` (propriedade `DateRegister`, método `SwithState`) e em `DefaultHealthCheckServicePlus` (todas as comparações `sta.DateRef.Add(...) < DateTime.Now` e `_cacheStatus.DateRegister.Add(...) < DateTime.Now`, nos dois caminhos).
- **Critério de aceite**: nenhuma ocorrência de `DateTime.Now` restante em código de agendamento/cache (`Grep` por `DateTime.Now` nos dois arquivos deve retornar vazio).
- **Observação**: fazer este passo **depois** de P0.3, já que a consolidação muda onde essas comparações vivem — se feito antes, retrabalho garantido.

### P0.7 — Corrigir mensagem de erro malformada
- **O quê**: `HealthChecksPlusAppExtension.UseHealthChecksCore` monta a mensagem `string.Format("Unable Find {0})", nameof(HealthCheckServiceCollectionExtensions.AddHealthChecks))` — sobra um `)` e falta clareza. Reescrever como algo como `$"Unable to find required services. Call '{nameof(HealthCheckServiceCollectionExtensions.AddHealthChecks)}' before this method."`.
- **Símbolo de referência**: `HealthChecksPlusAppExtension.UseHealthChecksCore` (pré-Fase 0: linha 136).

### P0.8 — Atualizar `SECURITY.md`
- **O quê**: corrigir a tabela de versões suportadas (hoje diz "2.x", o pacote publicado é 3.0.1) para refletir a série suportada real.

### P0.9 — Gate de fechamento da Fase 0
- **Critério de aceite explícito**:
  1. `dotnet build ./HealthCheckPlus.sln` limpo (0 erros, 0 warnings) nos três TFMs (net8.0, net9.0, net10.0).
  2. `dotnet test` verde com **31 testes pré-existentes + N novos** (contar e registrar N em `doc/progresso-plano-acao.md`) — nenhum teste antigo pode ter sido removido ou marcado `Skip` para "resolver" uma falha.
  3. P0.1 e P0.2 (que nasceram falhando) agora passam.
  4. `Grep` por `DateTime.Now` em `src/HealthCheckPlus/` retorna vazio.
  5. Decisão de P0.4 registrada em `doc/progresso-plano-acao.md` com justificativa.

---

## Fase 1 — Escopo de estado por instância (remover estado estático global)

### P1.1 — Mover estado estático para o container de DI
- **O quê**: mover `_addedHealthChecksPlus` (bool) e `_externalCheck` (dicionário) de campos `static` de `HealthChecksPlusExtension` para um serviço registrado como singleton no `IServiceCollection` (ex.: incorporar ao `CacheHealthCheckPlus` já existente, ou criar uma nova classe `HealthChecksPlusRegistrationState`).
- **Inclui o item adiado de P0.5**: se a nova classe de estado implementar `IDisposable`/`IAsyncDisposable`, ela pode descartar todas as instâncias `WrapperBaseHealthCheckPlus` cacheadas quando o container for descartado no shutdown do host — resolvendo o vazamento de recurso secundário identificado em P0.5.
- **Símbolo de referência**: `HealthChecksPlusExtension` (pré-Fase 0: linhas 25-26 e todo uso de `_addedHealthChecksPlus`/`_externalCheck`).

### P1.2 — Teste com dois hosts no mesmo processo
- **O quê**: escrever um teste que sobe dois `IServiceCollection`/`IHost` distintos no mesmo processo de teste, cada um chamando `AddHealthChecksPlus` + `AddCheckLinkTo` com o mesmo nome de check, e comprova que as instâncias adotadas **não** são compartilhadas entre os dois hosts. Este teste também serve para confirmar (ou refutar) o achado "Inferido" da auditoria — deve ser promovido para "Verificado" depois deste teste, seja qual for o resultado.
- **Reaproveitado por**: P3.5 (a suíte de integração real da Fase 3 pode incorporar este teste em vez de duplicá-lo).

### P1.3 — Gate de fechamento da Fase 1
- `dotnet build` + `dotnet test` verdes (31 + N pré-existentes da Fase 0 + os novos desta fase).
- P1.2 passa, comprovando isolamento entre hosts.

---

## Fase 2 — Substituir a ponte reflectiva do `AddCheckLinkTo`

Prioridade arquitetural nº 1 do produto (ver seção 3 da auditoria) — é o caminho pelo qual passam os checks de infraestrutura reais (Redis, SQL etc.), e hoje depende de nomes de tipo internos não contratuais do ASP.NET Core.

### P2.1 — Desenhar a nova API de adoção
- **O quê**: definir a assinatura pública que substitui a varredura reflectiva de `AddCheckLinkTo`. Caminho recomendado pela auditoria: o consumidor passa explicitamente a fábrica (`Func<IServiceProvider, IHealthCheck>`) ou os metadados do check de terceiro (nome, failure status, tags, timeout) para uma nova sobrecarga, em vez de o HealthCheckPlus reconstruir esse registro varrendo o `IServiceCollection`.
- **Entregável**: um rascunho de assinatura (pode ser um comentário/protótipo no próprio código ou uma nota curta neste plano) revisado antes de implementar — mudança de API pública não deve ser feita "no improviso".

### P2.2 — Implementar a nova API
- Implementar a nova sobrecarga de adoção; manter o comportamento de política idêntico ao atual (delay/period aplicados do mesmo jeito).

### P2.3 — Atualizar `Samples/` e `README.md`
- Os dois exemplos em `Samples/HealthCheckPlusDemo` e `Samples/HealthCheckPlusDemoBackgroudService` usam `AddCheckLinkTo` com `AddRedis` — atualizar para a nova API. Atualizar a seção de uso do `README.md` correspondente.

### P2.4 — Testes de integração da nova adoção
- Cobrir a adoção de um `IHealthCheck` de teste (fake) via o novo mecanismo, incluindo o caso de política aplicada corretamente.

### P2.5 — Remover o código reflectivo antigo
- Remover a varredura de `ServiceDescriptor`/`ImplementationInstance`/cast para `ConfigureNamedOptions<HealthCheckServiceOptions>` em `HealthChecksPlusExtension.AddCheckLinkTo`, e a comparação de string `"DefaultHealthCheckService"` em `AddHealthChecksPlus`, substituindo por comparação de tipo (`typeof(...)`) onde a remoção do serviço nativo ainda for necessária.

### P2.6 — Revisitar a decisão de P0.4
- Com a nova API de adoção em vigor, reavaliar se a Opção A (fail-fast no startup) passa a ser viável sem quebrar consumidores — registrar a decisão final (mantida ou trocada) em `doc/progresso-plano-acao.md`.

### P2.7 — Gate de fechamento da Fase 2
- `dotnet build` + `dotnet test` verdes.
- Nenhuma referência restante a nomes de tipo internos do ASP.NET Core por string/reflection em `HealthChecksPlusExtension`.
- `Samples/` compilam e demonstram a nova API.

---

## Fase 3 — Suíte de testes de integração real

### P3.1 — Infraestrutura de teste com `WebApplicationFactory`
- Adicionar a referência necessária (`Microsoft.AspNetCore.Mvc.Testing`) ao projeto de testes e montar um host mínimo de teste reutilizável entre os casos desta fase.

### P3.2 — Cobertura de `DefaultHealthCheckServicePlus` fim a fim
- Repetir os cenários de política (Healthy/Degraded/Unhealthy) já cobertos in-process na Fase 0, agora através de requisição HTTP real ao endpoint de health check, nos dois caminhos (com e sem `AddBackgroundPolicy`).

### P3.3 — Cobertura do serviço de background e filtros de publicação
- Testar `HealthCheckPlusBackGroundService`: `PublishingOptions.AfterIdleCount`, `WhenReportChange`, e a execução periódica em si.

### P3.4 — Cobertura do middleware fim a fim
- Testar `HealthCheckMiddlewarePlus`: mapeamento de `ResultStatusCodes`, os `ResponseWriter` prontos (`WriteShortDetails`, `WriteDetailsWithException` etc.), headers de cache.

### P3.5 — Cobertura das extensões de DI + cenário de dois hosts
- Reaproveitar P1.2 neste ponto, migrando-o (se fizer sentido) para o mesmo formato de teste desta fase.

### P3.6 — Medir cobertura e definir meta
- Rodar `coverlet` sobre a suíte completa e registrar o número resultante em `doc/progresso-plano-acao.md`. **Não inventar uma meta antes de medir** — a meta mínima de cobertura para o assembly `HealthCheckPlus` deve ser definida depois da primeira medição real. O gate qualitativo, esse sim, vale desde já: nenhum ponto de entrada público de `HealthChecksPlusExtension`, `HealthCheckMiddlewarePlus` ou `HealthCheckPlusBackGroundService` pode ficar sem pelo menos um teste de integração.

### P3.7 — Gate de fechamento da Fase 3
- `dotnet build` + `dotnet test` verdes, incluindo toda a suíte de integração.
- Gate qualitativo de P3.6 atendido.

---

## Fase 4 — Diferenciação deliberada (backlog, não detalhado)

Só entra em planejamento passo a passo depois que as Fases 0–3 estiverem fechadas e o gate de cada uma tiver sido cumprido. Itens candidatos (da auditoria, seção 7):
- Documentação de arquitetura e runbook operacional.
- Suporte opcional a cache compartilhado (ex.: Redis) para múltiplas réplicas.
- Métricas/OpenTelemetry a partir do `HealthReport`.
- Avaliar API fortemente tipada (enum/source generator) como alternativa opcional ao registro por string em `AddHealthChecksPlus`.
