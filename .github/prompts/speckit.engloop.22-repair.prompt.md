---
agent: speckit.engloop.22-repair
description: "Route or close one rule-bound repair, including the incident's SEK scenario/model/Cord correction when relevant."
argument-hint: "--phase <route|close> --postmortem <path> --rpi <RPIxxx> --rules <RULE:id,...> --acceptance <.engloop/repairs/PMxxx-RPIxxx.<route|close>.json>"
---

Carry exact Rule IDs, SEK applicability/scenario/repair fields, and the executable verification requirement into immutable route/close acceptance. Relevant repairs must update and regenerate the named native .NET 10 SEK v0.1.3 model/Cord scenario before closure.
