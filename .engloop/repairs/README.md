# Repair learning acceptance

Stage 22 stores one route/close record per selected PM repair item:

`.engloop/repairs/PM<NNN>-RPI<NNN>.route.json`

`.engloop/repairs/PM<NNN>-RPI<NNN>.close.json`

The record binds:

- source PM path and SHA-256;
- exact `RPIxxx` and `RULE:<card-slug>` identities;
- current `NORTHSTAR.md` and `LEARNINGS.md` SHA-256 values;
- the PM's exact executable gate and what it proves;
- `ROUTED` state before the Stage 04 handoff; or
- immutable close state referencing the route SHA-256 and a tool-produced gate receipt;
- `CLOSED` only after that gate process exits zero with hashed stdout/stderr and
  Stage 08 readiness is current.

Do not edit Rule IDs/gates to fit implementation output. If direction, pyramid, PM, or
gate changes, re-run Stage 21/22 validation.