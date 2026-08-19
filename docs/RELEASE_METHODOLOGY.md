# Release Methodology

This page describes how quality is verified before a release ships — for anyone deciding whether to adopt this library, and for future contributors who want to keep the same bar. It's not a changelog (see [`CHANGELOG.md`](../CHANGELOG.md) for what actually changed) and not an architecture reference (see [`ARCHITECTURE.md`](./ARCHITECTURE.md) for that).

## How v4.0.0 was hardened

Before this release, the codebase went through several independent review passes, each covering the full production surface from a different angle — concurrency and shared-state correctness, documentation-vs-code accuracy, and operational viability under real failure conditions (a broken logger, a throwing publisher, a disconnecting client, multiple hosts in one process). The passes combined manual review with multiple independent AI-assisted analysis rounds, each one deliberately kept blind to the previous round's own conclusions rather than building on trust in them.

A few rules shaped how findings were handled, not just what was found:

- **Reproduce before trusting.** Where a finding could be reproduced, a regression test was written against the bug first, confirmed failing, then confirmed passing after the fix — not just reasoned about.
- **A fix's own review counts too.** More than one fix introduced a new, narrower problem while closing the one it targeted; those got the same treatment as any other finding — reproduced, fixed, verified — rather than left as a footnote.
- **Fix the class, not the instance.** When a bug turned out to be one instance of a repeatable pattern (an unguarded external call, a stale cross-reference, an inconsistent name comparison), the rest of the codebase was searched for the same pattern in the same pass, so it closed once for the whole class.
- **Trade-offs go to the maintainer.** Where a fix carried a real trade-off — a breaking API change, a performance cost, a packaging decision — it was presented with the concrete cost spelled out rather than decided unilaterally. One proposed performance optimization was reverted after review surfaced a correctness risk it would have reopened.

## By the numbers

| | |
|---|---|
| Findings raised across the whole process | 134 |
| Fixed and verified (test-backed, or reviewed and corrected for documentation) | 122 |
| Investigated and discarded — not real bugs | 8 |
| Left unfixed by deliberate, documented trade-off | 4 |
| Left undecided | 0 |
| Tests passing | 188 / 188, across .NET 8, 9, and 10 |

## Documentation

Documentation was reorganized around one principle: objectivity and clarity at the top level, technical depth available on demand through navigation — not everything on one page.

- [`POINTS_OF_ATTENTION.md`](./POINTS_OF_ATTENTION.md) is new: a plain-language list of what to know before integrating the library, aimed at consumers, without implementation detail.
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) was restructured into a lean overview plus linked subpages under [`docs/architecture/`](./architecture/) for the topics with the most internal detail, aimed at maintainers and contributors.
- Every internal link across the hand-authored documentation was checked to resolve to a real file and a real heading — no broken links, and no link depending on a branch that doesn't hold the content it points to.

## Architecture decisions

Decisions load-bearing enough to affect how future contributors extend this library are now recorded as Architecture Decision Records under [`docs/adr/`](./adr/), each with its context, the alternatives considered, and the consequences accepted — not left implicit in the code alone.

## Where to go next

- [`CHANGELOG.md`](../CHANGELOG.md) — the itemized list of what actually changed.
- [`POINTS_OF_ATTENTION.md`](./POINTS_OF_ATTENTION.md) — what to know before you build on this library.
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — how the library is put together internally.
- [`RUNBOOK.md`](./RUNBOOK.md) — how to read a health check response and diagnose common problems in production.
- [`docs/adr/`](./adr/) — the architecture decisions this release made, and why.
