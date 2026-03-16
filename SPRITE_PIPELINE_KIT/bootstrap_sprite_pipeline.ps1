param(
    [Parameter(Mandatory = $true)]
    [string]$TargetRoot,
    [string]$ProjectName = "",
    [ValidateSet("generic", "character", "creature", "prop", "vfx", "ui_mascot")]
    [string]$Preset = "generic",
    [string[]]$Entities = @(),
    [string]$EntityLabelSingular = "",
    [string]$EntityLabelPlural = "",
    [string[]]$VariantAxes = @(),
    [string]$SourceRoot = "incoming_sprites",
    [string]$RuntimeRoot = "sprites_runtime",
    [string]$AuthoredRoot = "sprites_authored_verified"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $repoRoot "tools\bootstrap_sprite_pipeline.py"

$argsList = @(
    $scriptPath,
    "--target-root", $TargetRoot,
    "--preset", $Preset,
    "--source-root", $SourceRoot,
    "--runtime-root", $RuntimeRoot,
    "--authored-root", $AuthoredRoot
)

if (-not [string]::IsNullOrWhiteSpace($ProjectName)) {
    $argsList += @("--project-name", $ProjectName)
}
if ($Entities.Count -gt 0) {
    $argsList += @("--entities")
    $argsList += $Entities
}
if (-not [string]::IsNullOrWhiteSpace($EntityLabelSingular)) {
    $argsList += @("--entity-label-singular", $EntityLabelSingular)
}
if (-not [string]::IsNullOrWhiteSpace($EntityLabelPlural)) {
    $argsList += @("--entity-label-plural", $EntityLabelPlural)
}
if ($VariantAxes.Count -gt 0) {
    $argsList += @("--variant-axes")
    $argsList += $VariantAxes
}

python @argsList
