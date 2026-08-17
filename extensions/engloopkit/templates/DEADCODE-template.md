# DEADCODE<NNN>: <brief description>

- **Captured:** <UTC timestamp>
- **Status:** <PROPOSED | APPROVED | REJECTED | REMOVED | ROLLED-BACK | NO-CANDIDATE>
- **Confidence:** <HIGH | NOT-ELIGIBLE>
- **Scope:** <requested scan scope>
- **User decision:** <PENDING | APPROVED | REJECTED | NOT-ASKED>
- **Decision rationale:** <user rationale | declined without reason | NOT-APPLICABLE>

## Candidate

- **Symbol:** `<fully qualified symbol or branch identity>`
- **Path and lines:** `<root-relative path:start-end>`
- **Visibility/contract surface:** <private/internal/public/framework/generated/etc.>
- **Proposed deletion:** <exact concise deletion>

## Why this is the highest-certainty candidate

<Compare it with other inspected candidates and explain why this one has the strongest proof.>

## Liveness evidence

- **Symbol references:** <tool/check and result>
- **Repository references:** <search and result>
- **Build/generation reachability:** <evidence>
- **Runtime/dynamic-use checks:** <reflection/DI/config/serialization/interop/plugin/event/convention checks>
- **External/public contract checks:** <evidence>
- **History/architecture/direction/learnings:** <evidence>

## False-positive exclusions

- [ ] Not reflection- or convention-discovered
- [ ] Not serialized, configured, injected, generated, or dynamically invoked
- [ ] Not a platform/conditional/native/plugin/event surface
- [ ] Not a fixture, template, documentation contract, public API, or compatibility surface
- [ ] Not inferred dead solely from coverage, naming, or one workload

## Isolated deletion proof

- **Disposable copy/worktree:** `<path or safe identity>`
- **Deletion diff:** <exact deleted paths/lines>
- **Commands:**
  - `<command>` — exit `<code>` — <result>
- **Behavior/generation evidence:** <result>
- **Proof verdict:** <PASS | FAIL>

## User review

- **Question asked:** Proceed with removal of `DEADCODE<NNN>` exactly as recorded above?
- **Response:** <verbatim concise response | PENDING | NOT-ASKED>

## Current-source outcome

- **Change applied:** <yes/no>
- **Validation:** <commands/results | NOT-APPLICABLE>
- **Rollback:** <result | NOT-APPLICABLE>

## Next candidate

<`DEADCODE<NNN>` path after rejection, or `None` after accepted removal/no-candidate.>
