$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$candidatePaths = New-Object System.Collections.Generic.List[string]

$tempBuildRoot = Join-Path $repoRoot "artifacts"
if (Test-Path $tempBuildRoot) {
    Get-ChildItem -Path $tempBuildRoot -Directory -Filter "temp-build-*" |
        Sort-Object LastWriteTimeUtc -Descending |
        ForEach-Object {
            $exePath = Join-Path $_.FullName "SpriteWorkflow.App.exe"
            if (Test-Path $exePath) {
                [void]$candidatePaths.Add($exePath)
            }
        }
}

$debugExe = Join-Path $repoRoot "src\SpriteWorkflow.App\bin\Debug\net8.0\SpriteWorkflow.App.exe"
if (Test-Path $debugExe) {
    [void]$candidatePaths.Add($debugExe)
}

$releaseExe = Join-Path $repoRoot "src\SpriteWorkflow.App\bin\Release\net8.0\SpriteWorkflow.App.exe"
if (Test-Path $releaseExe) {
    [void]$candidatePaths.Add($releaseExe)
}

$appExe = $candidatePaths | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($appExe)) {
    throw "Could not find SpriteWorkflow.App.exe. Build the app first."
}

Start-Process -FilePath $appExe | Out-Null
