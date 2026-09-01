# TODO: Toyopuc Computerlink .NET

Current active TODOs only.

## Current Status

Three high-level API consistency candidates are currently tracked. Library implementation has not started.

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

## CL-API-003: Explicit relay bit-in-word operations

### Target State Candidate

Add explicit relay-route counterparts to the existing .NET bit-in-word read-modify-write helper:

- `RelayWriteBitInWord`
- `RelayWriteBitInWordAsync`

Python already exposes equivalent synchronous and asynchronous device-client operations. The .NET implementation can reuse the existing bit-in-word core; this candidate does not add a protocol command or change wire behavior. Exact signatures and transition requirements must be approved before implementation.

### Acceptance Criteria

1. Exact public signatures are approved before implementation.
2. The caller supplies the relay hops explicitly, and the complete read-modify-write sequence stays on that fixed route.
3. Each call performs one read followed by one write, sequentially, under one client FIFO turn and one absolute deadline.
4. Device, bit-index, and Boolean-value validation matches the existing `WriteBitInWord` contract and completes before transport.
5. PLC non-atomicity and outcome-unknown behavior after a possibly started write remain identical to the existing helper.
6. Synchronous and asynchronous behavior has the same functional scope as Python `relay_write_bit_in_word`.

- [ ] Target contract approved.
- [ ] Implementation completed.
- [ ] Tests added or updated for every acceptance criterion.
- [ ] Relevant static, unit, package, and documentation checks passed.
- [ ] Codex self-review completed.
- [ ] Documentation, migration notes, changelog, and API reference agree.
- [ ] Required live-PLC checks passed or given an explicit disposition.
- [ ] Final acceptance criteria verified.
