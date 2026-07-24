---
title: "<paper title>"
author:
  - "<author>"
date: "<date>"
abstract: |
  <Problem/constraint, system/technique, principal contribution, evaluation basis, and strongest evidenced result.>
bibliography: "<PAPxxx_title.bib>"
link-citations: true
---

# Introduction

<Establish the real system pressure, limitations of prior approaches, design challenge, contributions, and paper roadmap.>

Contributions:

1. <Evidence-backed design or system contribution.>
2. <Evidence-backed mechanism or integration contribution.>
3. <Evidence-backed evaluation or operational contribution.>

# Background and State of the Art

<Explain only the prior architecture/features/concepts required to understand the contribution. Cite primary sources accurately and distinguish prior assumptions from this work.>

# Design Goals and Considerations

| Goal / constraint | Target or invariant | Evidence / authority |
|---|---|---|
| | | |

<State architectural principles and explicit tradeoffs before presenting the solution.>

# High-Level Architecture

![High-level architecture](./<paper-assets>/architecture.svg){#fig:architecture}

<Explain components, control/data paths, ownership, integration, trust/failure boundaries, and why this architecture satisfies the goals.>

# Mechanism and Subsystem Design

## <Mechanism 1>

<Algorithm/data structure/protocol/compilation/storage/transaction behavior with sourced figures or worked examples.>

## <Mechanism 2>

<Mechanism detail and interaction with surrounding system.>

# System at Work

<Walk through representative legal, rejection, failure, recovery, or lifecycle paths and tie behavior to mechanisms.>

# Evaluation Methodology

Research questions:

- **RQ1:** <Question answered by evidence.>
- **RQ2:** <Question answered by evidence.>

| Dimension | Recorded value |
|---|---|
| Hardware/service environment | |
| Software/build versions | |
| Configuration | |
| Dataset/workload | |
| Baselines | |
| Metrics and units | |
| Warmup/repetitions/statistics | |
| Reproduction commands/artifacts | |

# Experimental Results

## RQ1: <question>

![Result plot](./<paper-assets>/result-rq1.svg){#fig:rq1}

<Report absolute values, comparison, uncertainty/variance, and the narrow supported conclusion.>

## RQ2: <question>

<Results and interpretation tied to evidence. Include negative results.>

# Discussion

## Tradeoffs and operational implications

<Cost, scalability, deployment, failure modes, operability, and when the design should not be used.>

## Limitations and threats to validity

<Scope limits, external/internal validity threats, missing experiments, confounders, and mitigations.>

# Related Work

<Compare contributions, assumptions, and tradeoffs precisely with resolved citations.>

# Conclusion

<Concise restatement of problem, contribution, and evidenced outcome; no new claims.>

# References

::: {#refs}
:::

# Appendix

<Extended algorithms, proofs, additional results, or operational detail as appropriate.>
