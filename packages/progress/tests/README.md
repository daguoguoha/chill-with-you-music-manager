# 进度测试

`Run.ps1 -JsonAssembly <Newtonsoft.Json.dll>` 使用 .NET Framework C# 编译器编译生产源码和测试替身。提供 Newtonsoft.Json 13.x 的 net45 构建，不能使用 Unity 的 netstandard 版运行独立进程。依赖 DLL 只放入本地生成的 run 目录，不随测试源码分发。

`RuntimeStubs.cs` 是 Unity、游戏和 BepInEx 的测试替身。测试直接调用生产适配器的对应回调，覆盖秒数恢复、延迟音源、切歌取消、暂停、完成与循环、禁用配置等。它不验证真实 Harmony 注入、Unity 回调顺序或实际音频输出。

`ProgressStore.cs` 的存储测试使用真实文件和文件锁，包含持久化、原子替换失败重试、损坏文件恢复、未知格式保护和历史数量限制。`results.json` 保存通过的检查名称。run 目录中的生成物与测试记录均为合成数据。
