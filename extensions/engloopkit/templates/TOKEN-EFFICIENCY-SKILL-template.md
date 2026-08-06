---
name: <lowercase-skill-name>
description: <Specific capability and when to use it. Include discovery keywords.>
metadata:
  author: <team-or-repository>
  version: "1.0"
---

# <Skill title>

## Purpose

<One concise statement of the repeated capability this skill makes fast and reliable.>

The directory name and `name` must match. Use 1–64 lowercase letters, digits, and
hyphens; do not start/end with a hyphen or use consecutive `--`. Add optional `license`
or `compatibility` frontmatter only when a real non-placeholder value is known.

## Prerequisites

- <Authoritative manifest/configuration/tool requirement.>
- <One cheap command that verifies the executable/path/version.>
- Stop with an actionable prerequisite result; do not guess a fallback.

## Procedure

1. <Read the minimum authoritative inputs.>
2. <Select one parameterized command/script path.>
3. <Write full output to an ignored evidence/log file.>
4. <Return a compact result: status, key metrics, evidence path, residual risk.>

## Validation

- <Focused deterministic check.>
- <Broader check only when the changed artifact's risk warrants it.>

## Edge cases

- <Missing local tool: record and stop.>
- <Conflicting manifest/lockfile: fail closed.>
- <Long-running monitor already exists: consume its final evidence; do not poll again.>

## Resources

- [Script](./scripts/<script-name>) — deterministic parameterized implementation.
- [Reference](./references/REFERENCE.md) — detailed material loaded only when needed.
- [Asset](./assets/<template-or-schema>) — optional static input/output template.

Keep this file concise. Move detailed material into directly linked `scripts/`, `references/`, or `assets/` resources so compatible agents load it progressively.
