# Card: Verification follows artifact class

- **Recall cue:** Are we forcing one test technique onto fundamentally different code?
- **Apply when:** Classifying modules, selecting unit/property versus SEK verification,
  or designing a whole-product readiness inventory.
- **Status:** Living
- **Last condensed:** 2026-07-10

## Compressed principle

Keep one quality bar but choose the verification method from the artifact's nature.
Generic, domain-free components earn evidence through unit/property tests. The
stateful domain vertical earns behavioral evidence through SEK. Generic code left in
the vertical is a boundary failure, not permission to evade either method.

## Decision checks

1. Does the module pass the domain-free component litmus test?
2. If yes, is it extracted and directly unit/property tested rather than given a
   tautological model?
3. If no, does the residual vertical receive real behavioral model/exploration proof?
4. Did changing the method accidentally lower line/branch, architecture, or green-test
   requirements? The bar must remain common.

## Source learnings

- [PM002/LRN001](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — verification method follows artifact class.
- [PM002/LRN002](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — extracting generics is a precondition for honest self-validation.
- [PM002/LRN003](../../postmortems/PM002_gate-verification-method-by-class.md#learnings)
  — refine how evidence is measured without lowering what is required.