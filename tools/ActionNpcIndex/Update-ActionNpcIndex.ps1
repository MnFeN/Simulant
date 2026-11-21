param(
    [Parameter(Mandatory = $true)][string] $GamePath,
    [Parameter(Mandatory = $true)][string[]] $Logs,
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\..\Simulant\Resources\NpcIndexes\ActionCasters.csv'),
    [string] $PackageSource
)

$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = Join-Path $installation 'MSBuild\Current\Bin\amd64\MSBuild.exe'
$project = Join-Path $PSScriptRoot 'ActionNpcIndex.csproj'
$restoreOptions = @()
if ($PackageSource) {
    $restoreOptions += "/p:RestoreSources=$PackageSource"
}
& $msbuild $project /restore /p:Configuration=Release /p:NuGetAudit=false /v:minimal /nologo @restoreOptions
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$tool = Join-Path $PSScriptRoot 'bin\Release\ActionNpcIndex.exe'
& $tool $GamePath $OutputPath @Logs
exit $LASTEXITCODE
