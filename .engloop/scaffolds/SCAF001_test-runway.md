# SCAF001 Test Runway

- Captured: 2026-07-13T16:37:43.3360446Z
- SDK: 8.0.422
- Framework: xUnit 2.9.2 / Microsoft.NET.Test.Sdk 17.11.1
- Terse command: ``dotnet test tests/EngLoopKit.Tests/EngLoopKit.Tests.csproj -c Debug --nologo --logger console;verbosity=detailed --filter FullyQualifiedName~EngLoopKit.Tests.RunwayBoundaryTests``
- Boundary test: ``EngLoopKit.Tests.RunwayBoundaryTests.RunwayBoundaryTest``
- Generated destination: ``tests/EngLoopKit.Loop.Generated/``
- Evidence digest: ``ac294c081cc2ba0ac13d042beadd245af71543592bc5cf94f740b3ed58992919``

## Observations
- baseline-pass: exit=0, boundary=True, controlledFailure=False
- controlled-failure: exit=1, boundary=True, controlledFailure=True
- restored-pass: exit=0, boundary=True, controlledFailure=False
