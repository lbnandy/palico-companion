param(
    [Parameter(Mandatory=$true)][string]$TmlInstallRoot,
    [string]$SaveDirectory,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$modRoot = Split-Path $PSScriptRoot -Parent
$tmlRoot = (Resolve-Path -LiteralPath $TmlInstallRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $tmlRoot 'tModLoader.dll'))) {
    throw 'TmlInstallRoot must contain tModLoader.dll.'
}
if ((Split-Path $modRoot -Leaf) -ne 'PalicoCompanion') {
    throw 'The source folder must be named PalicoCompanion (the internal mod ID).'
}
$buildArguments = @('tModLoader.dll', '-server', '-build', $modRoot, '-nosteam')
if ($SaveDirectory) {
    $saveRoot = [IO.Path]::GetFullPath($SaveDirectory)
    $buildArguments += @('-tmlsavedirectory', $saveRoot)
}
Push-Location $tmlRoot
try {
    & $Dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "tModLoader build failed (exit $LASTEXITCODE)." }
} finally {
    Pop-Location
}
