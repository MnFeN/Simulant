# Third-party components

- **Lumina 7.7.2**, NotAdam and contributors. Source snapshot: `8802d66a3cb3032be2fcf998bb10e864221bdb13`, <https://github.com/NotAdam/Lumina>. License: WTFPL v2, reproduced in `LICENSE.Lumina.txt`. Resource readers were modified for C# 7.3 / .NET Framework 4.8.1. Source comments identifying incorporated algorithms remain in their files.
- **Lumina.Excel generator and generated definitions**, WorkingRobot, NotAdam and contributors. Source snapshot: `f79754d7659243cbc2b2a0d9ec55c66319aa42a9`, <https://github.com/NotAdam/Lumina.Excel>. License: WTFPL v2, reproduced in `Licenses/Lumina.Excel.WTFPL.txt`. Generated syntax and row factories were adapted to C# 7.3. Schema inputs: <https://github.com/xivdev/EXDSchema> snapshots `75f674655bb89d6172effa3a5a2d93bcfc7deb51` and `a1e24a0c72058353f30f23beb996792cdb926868`. YamlDotNet 16.3.0 and SDK Roslyn are used only by the optional development generator and are not shipped.
- **BCnEncoder.Net 2.3.0**, Nominom and contributors, <https://github.com/Nominom/BCnEncoder.NET>. License: MIT OR Unlicense. Used for compressed texture decoding.
- **CommunityToolkit.HighPerformance 8.4.0**, .NET Foundation and contributors, <https://github.com/CommunityToolkit/dotnet>. License: MIT. Transitive dependency of BCnEncoder.Net.
- **Microsoft .NET compatibility libraries** (`System.Memory` 4.6.0, `System.Buffers` 4.6.0, `System.Numerics.Vectors` 4.6.0, `System.Runtime.CompilerServices.Unsafe` 6.1.0, `System.Threading.Tasks.Extensions` 4.6.0, `Microsoft.Bcl.HashCode` 6.0.0, `Microsoft.Bcl.Numerics` 10.0.3). Copyright (c) .NET Foundation and Contributors. License: MIT, <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>.
- **ILRepack.Lib.MSBuild.Task 2.0.48**, RBSoft, <https://github.com/ravibpatel/ILRepack.Lib.MSBuild.Task>。MIT 许可，仅用于构建时合并依赖，工具本身不随插件分发。

The ACT SDK is referenced for compilation and is not included in the plugin distribution. FFXIV client files are not included in source or distribution.

## MIT license

Copyright (c) the respective component authors and contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
