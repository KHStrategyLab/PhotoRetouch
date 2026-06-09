param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [object]$SelfContained = $true,
    [string]$OutputRoot = ".\publish\v1"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "PhotoRetouch.csproj"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$publishDirectory = Join-Path $outputRootPath "KRetouchPro-$Runtime"
$zipPath = Join-Path $outputRootPath "KRetouchPro-$Runtime.zip"

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null
if (Test-Path $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

$selfContainedText = if ([System.Convert]::ToBoolean($SelfContained)) { "true" } else { "false" }

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedText `
    -p:Platform=x64 `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDirectory

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -Force

Write-Host "Published: $publishDirectory"
Write-Host "Package:   $zipPath"
