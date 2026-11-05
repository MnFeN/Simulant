# 时间轴与实体回归测试

独立 .NET Framework 4.8 / C# 7.3 控制台程序，直接链接时间轴核心、`SimSession`、`EntityRegistry`、`EntityIdentity`、`SimActor`、`SimMovementController`、`SimCastController` 和 `SimOmenRenderer` 的真实源文件，不引用主插件、ACT、游戏进程或 NuGet 包。会话测试用最小替身隔离宿主和逻辑；实体测试以可控对象表观察、内存中的角色替身、CSV 数据和外部 PictoACT 回调替身隔离游戏环境。需要 Windows、.NET Framework 4.8 开发包及 Visual Studio MSBuild。

在仓库根目录的 PowerShell 中执行：

```powershell
$msbuildPath = & "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe
& $msbuildPath Simulant.Tests/Simulant.Tests.csproj /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw '测试项目编译失败' }
& ./Simulant.Tests/bin/Release/Simulant.Tests.exe
if ($LASTEXITCODE -ne 0) { throw '回归测试失败' }
```

程序逐项输出 PASS / FAIL，全部通过时退出码为 0。覆盖绝对与相对时间、重复节点、子时间轴与运行快照、同刻顺序、参数验证、较早节点唤醒、堆内取消、错误传播与取消、正在执行节点的取消语义、外部和回调内 Dispose、多线程入队与取消，以及 50,000 节点只由一个非线程池工作线程执行。

会话测试覆盖选项准备失败回滚、初始化节点异常只清理一次、停止时等待初始化退出、停止后重开及重复释放。管理器测试链接真实 `SimEntityManager`，仅替换原生生成器，验证自有实体回收、借用实体只解除关联、停止后拒绝创建，以及回收或日志异常不妨碍其余清理。

实体测试覆盖同一生命周期的包装复用、并发查询、消失与槽位复用、地址指纹变化、显式失效后不复活、同值重新创建、世界代次切换、拥有相同游戏 ID 的本地对象，以及原生清理失败后的重试只能匹配原生命周期。

移动测试通过真实调度器验证多个角色共享一个待执行更新节点、所有插值写入都在调度线程、运行中更换目标、停止与释放后的取消、失效实体不再写入和无效参数不覆盖原动作。另验证长帧不会越过目标，以及 `SimActor` 在读条清理失败时仍停止移动、重复释放只清理一次、解除关联不销毁底层实体。

读条与预兆测试覆盖替换读条取消旧节点、取消与释放停止后续工作、每次读条使用独立预兆标签、外部不同技能读条与失效实体保护，以及外部回调同步重入后旧逻辑不得清除新读条。延迟预兆在出现时采样位置并使用不受区域设置影响的数字格式；一个清理回调失败后仍清理其他角色。

同步测试使用有超时的事件屏障，不要求精确毫秒调度。耗时和线程池可用线程差值仅用于观察，不作为跨机器性能门槛。这里验证调度、实体身份和模拟行为的协调；原生字段偏移、坐标转换、实际实体释放、读条特效、ACT UI 和游戏画面效果仍需在实际插件环境验证。
