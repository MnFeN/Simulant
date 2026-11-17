<#
.SYNOPSIS
验证正式 DLL 加载，并用独立测试构建验证实体创建、插件卸载、小队与技能演出。
.DESCRIPTION
仅在测试构建中替换游戏连接的原生读写与调用边界。测试无需启动或连接 ACT、游戏。
各组入口分别运行，避免静态状态互相影响；任一构建、编译或验证失败均返回非零退出码。
.PARAMETER MSBuildPath
可选的 MSBuild.exe 路径；省略时通过 Visual Studio 的 vswhere 自动定位。
.PARAMETER Configuration
插件构建配置，默认 Release。
.PARAMETER GamePath
客户端根目录、game 或 sqpack 目录；只读加载游戏表，不连接游戏进程。
#>
[CmdletBinding()]
param(
    [string]$MSBuildPath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [Parameter(Mandatory = $true)]
    [string]$GamePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    $repositoryPath = Split-Path -Parent $PSScriptRoot
    $projectPath = Join-Path $repositoryPath 'Simulant\Simulant.csproj'
    $buildOutputPath = Join-Path $repositoryPath ('Simulant\bin\' + $Configuration)
    $outputPath = Join-Path $repositoryPath ('Simulant\obj\native-tests\' + [Guid]::NewGuid().ToString('N'))

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
    & $resolvedMSBuildPath $projectPath /restore /nologo /verbosity:minimal "/p:Configuration=$Configuration" /p:Platform=AnyCPU /p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw ('插件构建失败，退出码：' + $LASTEXITCODE) }

    $builtPluginPath = Join-Path $buildOutputPath 'Simulant.dll'
    if (-not (Test-Path -LiteralPath $builtPluginPath -PathType Leaf)) {
        throw ('构建未生成预期插件：' + $builtPluginPath)
    }
    # 隔离目录只放插件本体及 ACT 已有的宿主组件，防止构建目录中的依赖掩盖单 DLL 加载失败。
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    Copy-Item -LiteralPath $builtPluginPath -Destination $outputPath
    $hostAssemblies = Get-ChildItem -LiteralPath $buildOutputPath -File | Where-Object {
        $_.Name -eq 'Advanced Combat Tracker.dll' -or $_.Name -like 'FFXIV_ACT_Plugin*.dll' -or $_.Name -like 'Machina*.dll'
    }
    $hostAssemblies | Copy-Item -Destination $outputPath

    Write-Host ('构建原生边界测试目标：' + $Configuration)
    & $resolvedMSBuildPath $projectPath /nologo /verbosity:minimal "/p:Configuration=$Configuration" /p:Platform=AnyCPU /p:UseSharedCompilation=false /p:NativeTestBuild=true
    if ($LASTEXITCODE -ne 0) { throw ('测试目标构建失败，退出码：' + $LASTEXITCODE) }

    $testDllName = 'Simulant.NativeTestTarget.dll'
    $testBuildPath = Join-Path $repositoryPath ('Simulant\bin\NativeTests\' + $Configuration)
    $testOutputPath = Join-Path $outputPath 'fault-injection'
    New-Item -ItemType Directory -Path $testOutputPath -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $testBuildPath $testDllName) -Destination $testOutputPath
    $hostAssemblies | Copy-Item -Destination $testOutputPath

    $localTestsPath = Join-Path $PSScriptRoot 'Native\LocalCreationTests.cs'
    $idTestsPath = Join-Path $PSScriptRoot 'Native\EntityIdAllocationTests.cs'
    $unloadTestsPath = Join-Path $PSScriptRoot 'Native\PluginUnloadTests.cs'
    $partyTestsPath = Join-Path $PSScriptRoot 'Native\PartyServiceTests.cs'
    $nativeCallsTestsPath = Join-Path $PSScriptRoot 'Native\NativeCallsTests.cs'
    $nameTestsPath = Join-Path $PSScriptRoot 'Native\NameTests.cs'
    $dataTestsPath = Join-Path $PSScriptRoot 'Native\GameDataTests.cs'
    $entries = @(
        @{ Name = 'GameDataTests'; Label = '客户端强类型数据读取'; Sources = @($dataTestsPath) },
        @{ Name = 'ActionResourceTests'; Label = '技能检索、复制与窗体异步读取'; Sources = @(
            (Join-Path $PSScriptRoot 'Native\ActionResourceTests.cs'),
            (Join-Path $repositoryPath 'Simulant\Game\Data\ActionResource.cs')) },
        @{ Name = 'ExdResourceTests'; Label = '通用游戏表、关联名称与异步预览'; Sources = @(
            (Join-Path $PSScriptRoot 'Native\ExdResourceTests.cs'),
            (Join-Path $repositoryPath 'Simulant\Game\Data\ExdResource.cs')) },
        @{ Name = 'LocalCreationTests'; Label = '本地创建与失败清理'; Sources = @($localTestsPath) },
        @{ Name = 'DeveloperModeTests'; Label = '开发者模式与旅馆类型限制'; Sources = @($localTestsPath,
            (Join-Path $PSScriptRoot 'Native\DeveloperModeTests.cs')) },
        @{ Name = 'EntityIdAllocationTests'; Label = '实体 ID 分配'; Sources = @($localTestsPath, $idTestsPath) },
        @{ Name = 'PluginUnloadTests'; Label = '退出模拟并卸载插件'; Sources = @($localTestsPath, $unloadTestsPath) },
        @{ Name = 'PartyServiceTests'; Label = '模拟小队与仇恨恢复'; Sources = @($partyTestsPath) },
        @{ Name = 'NativeCallsTests'; Label = '技能演出参数与远程内存释放'; Sources = @($partyTestsPath, $nativeCallsTestsPath) },
        @{ Name = 'PresentationTests'; Label = '本地提示、标点和特效'; Sources = @($partyTestsPath, (Join-Path $PSScriptRoot 'Native\PresentationTests.cs')) },
        @{ Name = 'NameTests'; Label = '玩家临时改名与原名恢复'; Sources = @($partyTestsPath, $nameTestsPath) }
    )

    foreach ($entry in $entries) {
        $sources = $entry.Sources
        if ($entry.Name -in @('GameDataTests', 'ActionResourceTests', 'ExdResourceTests')) {
            $entryOutputPath = $outputPath
            $pluginPath = Join-Path $entryOutputPath 'Simulant.dll'
        }
        else {
            $entryOutputPath = $testOutputPath
            $pluginPath = Join-Path $entryOutputPath $testDllName
            $sources += Join-Path $PSScriptRoot 'Native\NativeTestContext.cs'
        }
        $executablePath = Join-Path $entryOutputPath ('Simulant.NativeTests.' + $entry.Name + '.exe')
        $compilerArguments = @(
            '/nologo', '/utf8output', '/codepage:65001', '/langversion:7.3', '/target:exe', '/platform:x64',
            ('/main:Simulant.Tests.Native.' + $entry.Name),
            ('/out:' + $executablePath),
            ('/reference:' + $pluginPath),
            '/reference:System.Core.dll', '/reference:System.Numerics.dll',
            '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Xml.Linq.dll',
            ('/reference:' + (Join-Path $entryOutputPath 'Advanced Combat Tracker.dll'))
        ) + $sources
        Write-Host ('编译故障注入入口：' + $entry.Label)
        & $compilerPath @compilerArguments
        if ($LASTEXITCODE -ne 0) { throw ($entry.Label + '编译失败，退出码：' + $LASTEXITCODE) }

        Write-Host ('运行故障注入入口：' + $entry.Label)
        & $executablePath $GamePath
        if ($LASTEXITCODE -ne 0) { throw ($entry.Label + '验证失败，退出码：' + $LASTEXITCODE) }
    }
    Write-Host '正式 DLL 加载、客户端数据及原生边界故障注入验证全部通过。'
    exit 0
}
catch {
    Write-Error -Message ('原生边界验证未完成：' + $_.Exception.Message) -ErrorAction Continue
    exit 1
}
