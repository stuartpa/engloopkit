# Contract: Learnings Pyramid Validation and Retrieval

- **Feature:** SPEC001
- **Stage:** 31
- **Compression graph:** `PMxxx/LEARNxxx → subject cards → root LEARNINGS.md → on-demand instruction`

## Inputs

Given an explicitly selected repository root whose physical layout has passed the
single-root gate and whose exact config is `.engloop/config.json`, the validator
consumes:

- `.engloop/postmortems/PM[0-9][0-9][0-9]_*.md`;
- `.engloop/learnings/cards/*.md`;
- root `LEARNINGS.md`;
- `.github/instructions/project-learnings.instructions.md`;
- committed `.engloop/learnings/retrieval-cases.json` and a fresh result file below
  ignored `.engloop/out/`.

Paths outside the root, symlinks escaping the root, duplicate canonical paths, malformed
UTF-8, or ambiguous IDs fail validation.

The validator MUST reject a missing/duplicate `.engloop/`, any current `engloop/`
compatibility tree, or any `.engloopkit/` directory before reading learning sources.
It does not merge card/source sets or retry discovery against another root.

## Accepted source extraction

1. Sort post-mortems by numeric PM identity, then canonical path.
2. Require one unambiguous `## Learnings` section in each accepted PM.
3. Within that section, accepted source bullets have exact bold identity `LEARNnnn`.
4. Form the globally unique source key `PMnnn/LEARNnnn` from file identity plus bullet.
5. Reject duplicate LEARN identity within a PM, duplicate PM identity across files, a
   learning outside the Learnings section presented as provenance, or a link whose
  displayed PM/LEARN pair disagrees with its target.
6. Never edit, delete, or normalize source PM content during condensation.

Acceptance status follows the PM's authoritative status/approval contract. If that
contract is missing or ambiguous, the validator fails rather than guessing whether a
learning belongs to the source set.

## Subject-card schema

Every current card is a Markdown file directly under the cards directory and MUST
contain:

- one subject title and one terse recall cue;
- applicability/“apply when” guidance;
- a compressed plain-language principle;
- operational/decision checks;
- a non-empty source-learning list with resolving links labeled `PMxxx/LEARNxxx`;
- a conflicts/supersessions/unresolved-tension section, explicitly saying “none known”
  when empty;
- living status.

A card must not copy an entire PM. Card slugs and canonical paths are unique. One source
may support multiple cards and one card may cite multiple sources.

## Static graph gate

The deterministic PASS predicate is:

```text
acceptedSources != empty
AND every accepted source has >= 1 incoming card citation
AND every card has >= 1 valid accepted source citation
AND every current card has exactly 1 index target
AND every index target resolves to a current card
AND every page→card and card→source link resolves
AND every displayed source ID equals target PM/LEARN content
AND all required card sections exist
AND every conflict/supersession state is explicit
AND indexWords <= 500
AND indexNonblankLines <= 60
AND instruction contract passes
AND retrieval contract passes
```

The report lists total/covered/missing/extra IDs and every broken or mismatched edge.
There is no partial PASS.

## Link resolution

- Resolve relative links from the containing file after percent-decoding path
  components once.
- Reject absolute filesystem paths, network links used as internal provenance, root
  escape, missing files, duplicate canonical targets, and fragments that do not identify
  the claimed PM Learnings content.
- An index cue may link only to a current card.
- A card provenance link must resolve to the PM file whose filename identity matches
  the displayed PM and whose Learnings section contains the displayed LEARN identity.
- “File exists” alone is insufficient; claimed content must exist at the target.

## One-page counting algorithm

Normalize CRLF/CR to LF. For the root index only:

- `nonblankLines` is the count of lines whose Unicode-whitespace-trimmed content is not
  empty;
- remove Markdown link destinations while retaining visible labels;
- remove Markdown punctuation/control markers but retain visible text, headings, list
  labels, inline code text, and fenced-code text;
- `words` are Unicode letter/number token runs that may contain internal apostrophe,
  right-apostrophe, underscore, or hyphen only when surrounded by token characters;
- frontmatter, if introduced, counts because it consumes the page; HTML comments count
  unless the format explicitly forbids them (the v2 template forbids them).

PASS requires both `words <= 500` and `nonblankLines <= 60`. The report prints both
actual values.

## Instruction contract

The exact path is
`.github/instructions/project-learnings.instructions.md`. It MUST:

- have a keyword-rich discovery description covering architecture, scaffold, refactor,
  model, exploration, validation, unit test, readiness, incident, repair, and evolution;
- direct progressive retrieval: root index → relevant card(s) → cited PM/LEARN source;
- tell the agent not to invent consensus when no cue fits or sources conflict;
- stay concise and not duplicate cards/sources;
- contain no `applyTo: "**"`, equivalent wildcard, command registration, custom-agent
  registration, or picker entry.

## Clean-context retrieval contract

### Case manifest

Each case has a stable ID, a question requiring one or more learning principles,
expected card slugs, expected `PMxxx/LEARNxxx` source IDs, and coverage tags. The case set
MUST cover:

- every current card at least once;
- at least one source from every PM in the accepted source set;
- at least one conflict/tension case when any card declares one;
- a no-cue case that must report the gap rather than fabricate provenance.

### Isolation protocol

For each case, start a fresh agent/context with access to the repository and give it
only:

1. the question;
2. the on-demand instruction path as the entry point.

Do not preload `LEARNINGS.md`, cards, PMs, expected IDs, or prior case output. Record the
card/source IDs the evaluator actually used.

### Deterministic result comparison

The validator compares canonical expected and actual sets:

- missing expected card/source: FAIL;
- extra/false-provenance card/source: FAIL;
- unresolved ID or wrong PM/LEARN pairing: FAIL;
- prior-case/context leakage: invalidate and rerun in a clean context;
- exact match for all cases: PASS.

The agent interaction itself is an acceptance exercise; set comparison, coverage, and
final status are deterministic. Results carry input digests so an old retrieval run
cannot validate changed cards/index/sources.

## Stage 31 state effect

On PASS for matching source/card/index/instruction/retrieval digests, clear the pending
learning-refresh obligation. On any failure, keep it pending and repeat
condense/recreate/retrieve. Stage 31 never rewrites source PM/LEARN content, closes a
repair item, or changes product readiness.
