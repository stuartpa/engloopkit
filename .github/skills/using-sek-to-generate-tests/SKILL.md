---
name: using-sek-to-generate-tests
description: >
  How to consume SpecExplorerKit (the `sek` tool) as a downstream project to model a
  system-under-test (SUT), explore it, and generate a standalone xUnit test project. Use
  when building or debugging a SEK model/CORD project, wiring the config.json `binding`,
  running `sek explore`/`test`/`generate`, or fixing "binding"/SUT-reflection errors.
  Captures the exact project layout, config schema, and generated-test mechanics so they
  never have to be re-derived.
---

# Using SEK to model a SUT and generate xUnit tests

SpecExplorerKit (`sek`) is a model-based testing tool: you write a C# **model program**
+ a **CORD** script describing a system's behavior, `sek` explores it into a transition
graph, and `sek generate` emits a **standalone xUnit project** whose tests replay the
explored action sequences against your real implementation (the **SUT / binding**).

This skill is the consumer's cheat-sheet — everything below was verified against SEK
v0.1.1 and the in-repo samples.

> **Readiness is a gate, not a claim (PM001).** Generating tests for a component does **not** make
> a project "ready for incidents." A project is ready **iff** every module (each `components/*` and
> the vertical) is modelled + explored + covered ≥95% line/branch + conformant + green — the verdict
> of `/speckit.engloopkit.coverage`. Never narrate readiness from "I generated some tests" or "the
> pilot passed"; if the whole-product gate has not PASSed, the status is NOT READY.

## Consuming SEK (no new dependencies beyond what SEK uses)

- **The CLI** is a .NET global tool. Install from the release nupkg:
  `dotnet tool install -g SpecExplorerKit.Tool --version 0.1.1 --add-source <folder-with-nupkg>`.
  Or run the built DLL directly (the executable is **`sek.dll`**, not `Sek.Cli.dll`):
  `dotnet <SEK>/src/Sek.Cli/bin/Debug/sek.dll <command>`.
- **`Sek.Modeling` is NOT published as a NuGet package** — only the tool is. A model
  project references it by **`ProjectReference`** to `Sek.Modeling.csproj`. From a sibling
  repo, use a relative path, e.g. `..\..\..\SEK\src\Sek.Modeling\Sek.Modeling.csproj`.
- Everything is **net8.0 / xUnit** — the same platform as SEK itself.

## CLI commands

```
sek init [--project <dir>]        # scaffold .specexplorerkit/
sek validate [--project <dir>]    # check model program and CORD line up
sek explore <machine> [--out f]   # explore a machine -> .seexpl graph
sek view <file.seexpl> --format mermaid|dot|html [--out f]
sek test <machine>                # explore, then replay against the SUT (conformance)
sek generate <machine> [--out d] [--namespace ns] [--max n]   # emit an xUnit project
sek version
```

`test` and `generate` **require a `binding`** in config.json (they replay against the
SUT). `explore`/`view` do not.

## Project layout (a "SEK project" = a dir containing `.specexplorerkit/config.json`)

```
MyProject/
├── .specexplorerkit/
│   └── config.json
└── Model/
    ├── MyProject.Model.csproj     # references Sek.Modeling
    ├── Model.cs                   # the ModelProgram subclass
    └── Config.cord                # the CORD script(s)
```

### config.json

```json
{
  "model":   { "assembly": "Model/bin/Debug/Model.dll", "type": "MyNs.MyModel" },
  "cord":    "Model",
  "binding": { "assembly": "../Core/bin/Debug/MyCore.dll", "namespace": "MyNs.Core" },
  "out":     ".specexplorerkit/out"
}
```

- `model.assembly` / `model.type` — the built model DLL and the `ModelProgram` subclass.
- `cord` — the directory (relative to the project) holding the `*.cord` files.
- `binding` — the **SUT**: the assembly + namespace that generated tests reflect into.
  **Omit it and `generate`/`test` fail with** `test generation needs a 'binding' in
  .specexplorerkit/config.json`.

### The model project (`.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>MyNs</RootNamespace>
    <AssemblyName>Model</AssemblyName>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\SEK\src\Sek.Modeling\Sek.Modeling.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="*.cord" />
  </ItemGroup>
</Project>
```

### The model (`Model.cs`)

```csharp
using System.Collections.Generic;
using Sek.Modeling;

namespace MyNs
{
    public sealed class MyModel : ModelProgram
    {
        // model state as fields/props
        [Rule("Sut.DoThing")]                 // "Sut" must match the SUT class name
        public void DoThing(int x)
        {
            Require(x >= 0, "guard message"); // precondition prunes the exploration
            // mutate model state
        }

        [AcceptingCondition]
        public bool Accepting() => true;       // which states may end a test
    }
}
```

Rule names are `"<SutClass>.<Method>"`. Return types may be `Set<T>`/`Sequence<T>`/`Map<>`
from `Sek.Modeling`. `Require(...)` is the precondition/guard.

### The CORD (`Config.cord`)

```
config Main
{
    action all Sut;                 // bind actions to the SUT surface
    switch StateBound = 12800;
    switch StepBound = 12800;
    switch PathDepthBound = 12800;
}

// pin parameter domains (drives Z3 parameter generation)
config Params : Main
{
    action static void Sut.DoThing(int x) where {. Condition.In(x, 0, 1, 2); .};
}

machine ModelProgram() : Main { construct model program from Params }

// slice with the behavior algebra: ; sequence, | choice, * star, || product
machine Sliced() : Main { (Setup; DoThing*; Teardown) || ModelProgram }

// test-case machine
machine TestSuite() : Main where TestEnabled = true { construct test cases for Sliced }
```

## What `sek generate` emits (and how tests find the SUT)

- A **standalone net8.0 xUnit project** (`Microsoft.NET.Test.Sdk` 17.11.1, `xunit`
  2.9.2, `xunit.runner.visualstudio` 2.8.2). **No reference to SEK** — it runs on its own.
- Each `[Fact]` replays one explored path by calling a private `Sut` harness.
- The harness **loads the binding assembly by path via reflection**
  (`AssemblyLoadContext.LoadFromAssemblyPath`), then for each action label
  `"<Class>.<Method>"` resolves `bindingNamespace + "." + Class`, finds `Method`
  (public/nonpublic, static/instance), coerces string args (`string` as-is, enums via
  `Enum.Parse`, else `Convert.ChangeType`), and invokes it. Instance methods use **one SUT
  instance per test path** (the harness caches one instance per SUT type and reuses it across the
  steps of a `[Fact]`; xUnit news a fresh test class per fact, so paths stay isolated). This means
  a **stateful** SUT is driven correctly across a path — e.g. `Coin(); Push()` acts on the same
  turnstile. (Fixed in SEK after the first binding sample; earlier builds new-ed a fresh instance
  per step and could not drive stateful SUTs.)
- The binding DLL **path is baked in** as `DefaultBinding`; override at runtime with the
  **`SEK_BINDING`** environment variable.
- `call` actions → `Step` (driven); `event` actions → `Observe` (asserted).

**Implication for the SUT:** it must expose a class named exactly as in `[Rule("Class.Method")]`
under the binding namespace, with methods whose names/arities match. The generated tests
"pass" when those calls don't throw (and, for a live harness, when observations hold).

## Gotchas (learned the hard way)

- `generate`/`test` need a `binding`; `explore` does not.
- The CLI DLL is `sek.dll`. Invoke via `dotnet <path>/sek.dll`.
- **Reference binding sample:** `SEK/samples/Turnstile` is the first in-repo sample that wires a
  `binding` (a stateful SUT + model + cord). Copy its layout: `Sut/` (the SUT class-library),
  `Model/` (`ModelProgram` + `.cord`), and `.specexplorerkit/config.json` with a `binding`. Its
  generated tests pass and run in CI, so it is the known-good template.
- Object-valued arguments and object-valued *return* state still cannot be threaded by the
  generated harness (it coerces only string/enum/primitive args). Model such SUTs with
  primitive-keyed actions, or use `sek test` / hand-written tests.
- Get the SUT class/method names to match the model rule names exactly (`[Rule("Class.Method")]`
  binds to `bindingNamespace + "." + Class` and method `Method`), or the reflection throws at run
  time (surfaced as failing asserts).
- Build the model project **before** `explore`/`generate` (the tool loads `Model.dll`),
  and build the SUT before running the generated tests (they load the binding DLL).
- Don't pipe interactive tool commands through `Select-Object` — it hides prompts.
