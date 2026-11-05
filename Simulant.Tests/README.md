# 时间轴回归测试

独立 .NET Framework 4.8 / C# 7.3 控制台程序，直接链接三个时间轴核心源文件及真实 `SimSession`，不引用主插件、ACT、游戏进程或 NuGet 包。会话测试用最小替身隔离宿主、逻辑和实体依赖。需要 Windows、.NET Framework 4.8 开发包及 Visual Studio MSBuild。

在仓库根目录的 PowerShell 中执行：

```powershell
$msbuildPath = & "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe
& $msbuildPath Simulant.Tests/Simulant.Tests.csproj /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw '测试项目编译失败' }
& ./Simulant.Tests/bin/Release/Simulant.Tests.exe
if ($LASTEXITCODE -ne 0) { throw '时间轴测试失败' }
```

程序逐项输出 PASS / FAIL，全部通过时退出码为 0。覆盖绝对与相对时间、重复节点、子时间轴与运行快照、同刻顺序、参数验证、较早节点唤醒、堆内取消、错误传播与取消、正在执行节点的取消语义、外部和回调内 Dispose、多线程入队与取消，以及 50,000 节点只由一个非线程池工作线程执行。

会话测试覆盖选项准备失败回滚、初始化节点异常只清理一次、停止时等待初始化退出、停止后重开及重复释放。

同步测试使用有超时的事件屏障，不要求精确毫秒调度。耗时和线程池可用线程差值仅用于观察，不作为跨机器性能门槛。这里验证调度核心和会话协调；ACT UI、实际实体释放、游戏注入和画面效果仍需在实际插件环境验证。
