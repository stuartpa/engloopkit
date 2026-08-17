---
name: speckit.engloop.06-explore
description: Explore model scenarios, regenerate deterministic suites, and capture CORD evidence.
argument-hint: "[exploration scenario set]"
target: vscode
user-invocable: true
disable-model-invocation: true
tools: [read, search, edit, execute]
agents: []
hooks:
  SessionStart:
    - type: command
      command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.06-explore --root .
      timeout: 30
handoffs:
  - label: Repair model deficiency
    agent: speckit.engloop.05-model
    prompt: Revise the behavioral model to address the exploration deficiency identified above before regenerating.
    send: false
  - label: Validate generated suite
    agent: speckit.engloop.07-validate
    prompt: Run the freshly generated suite above against the real SUT and publish functional reachability.
    send: false
---

## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** current accepted model exists.
- **Goal:** bounded exploration and generated suite refresh.
- **Actions:** load the installed SEK product skills, explicitly register generated
  destinations, run SEK exploration/generation, and persist CORD evidence.
- **Verification:** generated suites are fresh, exploration is bounded, and ELK's required
  legal and model-derived rejection evidence is present.
- **Memory:** `.engloop/cord/` and generated test destination.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.06-explore --root .`

## SEK product dependency

SEK owns its language, binding, and generation guidance. Load the installed
`.specify/extensions/sek/skills/sek-cord-authoring/SKILL.md` and
`.specify/extensions/sek/skills/using-sek-to-generate-tests/SKILL.md`; if either is absent, stop and require the SEK
extension. Do not copy SEK documentation into ELK.
Use the root-local `sek` v0.1.3 tool directly; it and generated test projects are
native `net10.0` dependencies.

## Overlay ownership

When `.engloop/config.json` has `overlayMode: true`, register every generated destination
outside `.engloop/` **before generation**:

`dotnet tool run engloopkit -- overlay register --root . --file <generated-file>`

Use `--directory <generated-directory>` when the generator owns a whole output tree.
Registration is explicit and must precede generation; do not guess ownership from CORD
field names or application layout.

## Done when

- [ ] Installed SEK product skills governed exploration and generation
- [ ] Exploration evidence is fresh and bounded
- [ ] Required legal and model-derived rejection evidence is verified
- [ ] Every overlay-local generated destination is explicitly registered and ignored
- [ ] Generated suite is regenerated deterministically
