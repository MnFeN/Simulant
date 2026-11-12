<#
.SYNOPSIS
构建插件，并用真实 DLL 验证本地创建清理、实体 ID 分配、插件卸载、模拟小队与技能演出。
.DESCRIPTION
仅替换 PostNamazu 的原生读写与调用边界，不会启动或连接 ACT、游戏。
各组入口分别运行，避免静态状态互相影响；任一构建、编译或验证失败均返回非零退出码。
.PARAMETER MSBuildPath
可选的 MSBuild.exe 路径；省略时通过 Visual Studio 的 vswhere 自动定位。
.PARAMETER Configuration
插件构建配置，默认 Release。
#>
[CmdletBinding()]
param(
    [string]$MSBuildPath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    $repositoryPath = Split-Path -Parent $PSScriptRoot
    $projectPath = Join-Path $repositoryPath 'Simulant\Simulant.csproj'
    $outputPath = Join-Path $repositoryPath ('Simulant\bin\' + $Configuration)

    if ([string]::IsNullOrWhiteSpace($MSBuildPath)) {
        $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path -LiteralPath $vswherePath -PathType Leaf)) {
            throw '未找到 vswhere，请用 -MSBuildPath 指定 MSBuild.exe。'
        }
        $discoveredPaths = @(& $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
        if ($LASTEXITCODE -ne 0 -or $discoveredPaths.Count -eq 0) {
            throw '无法定位 MSBuild，请安装 Visual Studio 的 .NET 桌面开发组件，或指定 -MSBuildPath。'
        }
        $MSBuildPath = $discoveredPaths[0]
    }
    if (-not (Test-Path -LiteralPath $MSBuildPath -PathType Leaf)) {
        throw ('指定的 MSBuild 文件不存在：' + $MSBuildPath)
    }
    $resolvedMSBuildPath = (Resolve-Path -LiteralPath $MSBuildPath).ProviderPath
    $msbuildDirectory = Split-Path -Parent $resolvedMSBuildPath
    $compilerCandidates = @(
        (Join-Path $msbuildDirectory 'Roslyn\csc.exe'),
        (Join-Path (Split-Path -Parent $msbuildDirectory) 'Roslyn\csc.exe')
    )
    $compilerPath = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $compilerPath) {
        throw ('未在所选 MSBuild 的安装目录找到 Roslyn 编译器：' + $resolvedMSBuildPath)
    }

    Write-Host ('构建插件：' + $Configuration)
    & $resolvedMSBuildPath $projectPath /restore /nologo /verbosity:minimal "/p:Configuration=$Configuration" /p:Platform=AnyCPU
    if ($LASTEXITCODE -ne 0) { throw ('插件构建失败，退出码：' + $LASTEXITCODE) }

    $pluginPath = Join-Path $outputPath 'Simulant.dll'
    if (-not (Test-Path -LiteralPath $pluginPath -PathType Leaf)) {
        throw ('构建未生成预期插件：' + $pluginPath)
    }
    $localTestsPath = Join-Path $PSScriptRoot 'Native\LocalCreationTests.cs'
    $idTestsPath = Join-Path $PSScriptRoot 'Native\EntityIdAllocationTests.cs'
    $unloadTestsPath = Join-Path $PSScriptRoot 'Native\PluginUnloadTests.cs'
    $partyTestsPath = Join-Path $PSScriptRoot 'Native\PartyServiceTests.cs'
    $nativeCallsTestsPath = Join-Path $PSScriptRoot 'Native\NativeCallsTests.cs'
    $entries = @(
        @{ Name = 'LocalCreationTests'; Label = '本地创建与失败清理'; Sources = @($localTestsPath) },
        @{ Name = 'EntityIdAllocationTests'; Label = '实体 ID 分配'; Sources = @($localTestsPath, $idTestsPath) },
        @{ Name = 'PluginUnloadTests'; Label = '退出模拟并卸载插件'; Sources = @($localTestsPath, $unloadTestsPath) },
        @{ Name = 'PartyServiceTests'; Label = '模拟小队与仇恨恢复'; Sources = @($partyTestsPath) },
        @{ Name = 'NativeCallsTests'; Label = '技能演出参数与远程内存释放'; Sources = @($partyTestsPath, $nativeCallsTestsPath) }
    )

    foreach ($entry in $entries) {
        $executablePath = Join-Path $outputPath ('Simulant.NativeTests.' + $entry.Name + '.exe')
        $compilerArguments = @(
            '/nologo', '/utf8output', '/codepage:65001', '/langversion:7.3', '/target:exe', '/platform:x64',
            ('/main:Simulant.Tests.Native.' + $entry.Name),
            ('/out:' + $executablePath),
            ('/reference:' + $pluginPath),
            '/reference:System.Core.dll', '/reference:Microsoft.CSharp.dll', '/reference:System.Numerics.dll',
            '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
            ('/reference:' + (Join-Path $outputPath 'Advanced Combat Tracker.dll'))
        ) + $entry.Sources
        Write-Host ('编译故障注入入口：' + $entry.Label)
        & $compilerPath @compilerArguments
        if ($LASTEXITCODE -ne 0) { throw ($entry.Label + '编译失败，退出码：' + $LASTEXITCODE) }

        Write-Host ('运行故障注入入口：' + $entry.Label)
        & $executablePath
        if ($LASTEXITCODE -ne 0) { throw ($entry.Label + '验证失败，退出码：' + $LASTEXITCODE) }
    }
    Write-Host '五组真实 DLL 故障注入验证全部通过。'
    exit 0
}
catch {
    Write-Error -Message ('原生边界验证未完成：' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
