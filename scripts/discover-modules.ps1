[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modules = @(
    [ordered]@{ id = 'components.numbering'; path = 'components/EngLoopKit.Components.Numbering/EngLoopKit.Components.Numbering.csproj' },
    [ordered]@{ id = 'components.statemachine'; path = 'components/EngLoopKit.Components.StateMachine/EngLoopKit.Components.StateMachine.csproj' },
    [ordered]@{ id = 'components.documentvalidation'; path = 'components/EngLoopKit.Components.DocumentValidation/EngLoopKit.Components.DocumentValidation.csproj' },
    [ordered]@{ id = 'components.overlay'; path = 'components/EngLoopKit.Components.Overlay/EngLoopKit.Components.Overlay.csproj' },
    [ordered]@{ id = 'core'; path = 'src/EngLoopKit.Core/EngLoopKit.Core.csproj' },
    [ordered]@{ id = 'tool'; path = 'src/EngLoopKit.Tool/EngLoopKit.Tool.csproj' }
)

foreach ($module in $modules) {
    if (-not (Test-Path (Join-Path $root $module.path) -PathType Leaf)) {
        throw "Configured module path is missing: $($module.id)"
    }
}

$modules | ConvertTo-Json -Depth 4
