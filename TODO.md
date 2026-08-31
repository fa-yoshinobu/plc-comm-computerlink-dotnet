# TODO: Toyopuc Computerlink .NET

Current active TODOs only.

## Current Status

Two high-level API consistency candidates are currently tracked. Library implementation has not started.

## CL-NAME-001: DWord single-request API names

### Target State

The one-request DWord extension methods use names that identify their one-request contract and do not collide with the broader instance methods:

- `ReadDWordsSingleRequestAsync`
- `WriteDWordsSingleRequestAsync`

The existing `ReadDWordsAsync` and `WriteDWordsAsync` extension names remain available only for an agreed transition period. Wire behavior does not change.

### Acceptance Criteria

1. The two canonical names above are public and execute exactly one protocol request.
2. Ordinary extension-method syntax resolves to the intended methods without a static-call workaround.
3. The previous extension names follow the separately approved transition and removal policy.
4. Tests distinguish the single-request extensions from the broader instance APIs.
5. User documentation and examples use only the canonical names.

- [ ] Implementation completed.
- [ ] Tests added or updated for every acceptance criterion.
- [ ] Relevant static, unit, package, and documentation checks passed.
- [ ] Codex self-review completed.
- [ ] Documentation, migration notes, changelog, and API reference agree.
- [ ] Final acceptance criteria verified.

## CL-API-002: Named read and polling cardinality

### Target State Candidate

Align the .NET and Python high-level contracts. Currently `ReadNamedAsync(IEnumerable<string>)` and `PollAsync(IEnumerable<string>)` accept collection-shaped arguments but reject every count except one, while Python supports multiple named addresses. The preferred candidate is multiple-address aggregation in .NET; changing the .NET API to a singular argument is the alternative.

The final cardinality, ordering, duplicate handling, request splitting, and non-atomic behavior require a separate specification decision before implementation.

### Acceptance Criteria

1. One target contract is approved and recorded before implementation.
2. Method names, parameter shapes, and return types accurately express the approved cardinality.
3. .NET and Python expose equivalent high-level behavior, with language-idiomatic signatures.
4. Tests cover one address, multiple addresses, duplicates, invalid input, request splitting, and failure behavior required by the approved contract.
5. Documentation states whether a multiple-address operation may use multiple non-atomic protocol requests.

- [ ] Target contract approved.
- [ ] Implementation completed.
- [ ] Tests added or updated for every acceptance criterion.
- [ ] Relevant static, unit, package, and documentation checks passed.
- [ ] Codex self-review completed.
- [ ] Documentation, migration notes, changelog, and API reference agree.
- [ ] Required live-PLC checks passed or given an explicit disposition.
- [ ] Final acceptance criteria verified.
