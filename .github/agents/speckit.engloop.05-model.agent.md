---
name: speckit.engloop.05-model
description: Author and validate the independent behavior model including rejection
  semantics.
argument-hint: '[model scope or behavior delta]'
target: vscode
user-invocable: true
disable-model-invocation: true
tools:
- read
- search
- edit
- execute
- agent
agents:
- Explore
hooks:
  SessionStart:
  - type: command
    command: dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.05-model
      --root .
    timeout: 30
handoffs:
- label: Explore the model
  agent: speckit.engloop.06-explore
  prompt: Explore the accepted model above and generate the functional suite into
    the proven test runway.
  send: false
---


<!-- Extension: engloop -->
<!-- Config: .specify/extensions/engloop/ -->
## User Input

```text
$ARGUMENTS
```

## Artifact root

Use exactly `.engloop/` with config at `.engloop/config.json`.

## Loop definition

- **Trigger:** implementation and architecture are current.
- **Goal:** independent rich behavioral model updates.
- **Actions:** explicitly register overlay-local model output paths, update model
  state/guards/invariants, and validate model build.
- **Verification:** model compiles and captures required negative semantics.
- **Memory:** `.engloop/models/` and `model/EngLoopKit.Model/`.

Run before any action:

`dotnet tool run engloopkit validate agent-entry --stage speckit.engloop.05-model --root .`

## Overlay ownership

When `.engloop/config.json` has `overlayMode: true`, register every model project
directory outside `.engloop/` **before creating or updating it**:

`dotnet tool run engloopkit -- overlay register --root . --directory <model-project-directory>`

Registration is explicit; do not infer ownership from a module name or repository layout.
The command atomically updates the local overlay manifest and `.git/info/exclude`, and
fails closed if the path is already tracked, staged, or present in history since the
overlay baseline.

## Done when

- [ ] Model captures legal and rejection behavior requirements
- [ ] Every overlay-local model output path is explicitly registered and ignored
- [ ] Model build/validation passes