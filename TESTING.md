# v1.2.0 验证记录

Windows x64、Windows PowerShell 5.1、.NET Framework。此次更新管理器外观和六个平台面板的主题，播放器及网络桥接源码保持不变，进度保存插件沿用 v1.0.0。

## 已执行

- 使用真实游戏及 BepInEx 引用程序集重新编译六个平台插件，编译成功。与此前一样，部分游戏 netstandard / Newtonsoft.Json 引用有统一版本警告。
- 生成六个平台共 23 个列表、登录、导入和长提示视图。直接执行生产 PlayerUI、GameTheme、Models；椒盐另包含生产 LocalImport，使用 GDI 测试适配器和合成数据。不是实际游戏画面。
- 重新执行管理器 21 组安装回归，包括七插件共存、目标文件 SHA256、停用、启用、撤回、修复、运行保护、冲突、拒绝降级、文件锁、链接目录和真实 WinForms 界面渲染。
- 重新执行安装中途写入失败恢复检查，原文件恢复，失败得到报告。
- 管理器真实渲染检查确认七行插件完整显示，并保留安装、停用与撤回按钮的条件判断。
- 使用 QQ 音乐从 v1.1.0 随包版升级到主题版，再撤回到旧版，核对账号测试标记、配置和进度标记保持不变。
- 检查 UI 修改只涉及 PlayerUI、GameTheme、插件声明版本和初始面板高度；Bridge、NativeIntegration、AudioDecoder、Models、RuntimeHost、LocalImport 等业务源码与 v1.1.0 随包版一致。

回归发现并修复椒盐文件选择器重编译后安装器固定校验值需要同步的问题。所有安装器与新目录清单均重新计算校验值。更新、停用和撤回继续保留账号、配置、进度记录与游戏存档。

本次没有启动或修改用户真实游戏，没有重新扫码或播放六个平台音频。实际 Unity 透明叠加、输入焦点、各分辨率和 DPI 下显示仍需游戏内验收。此前用户反馈的 B站 HTTP 412、酷狗 20028 不是此次主题更新的修复范围。

机器可读结果位于 `tests/test-results.json`、`tests/recovery-result.json` 与 `tests/theme/render-results.json`。`packages/progress/tests/` 中的进度测试记录保留自 v1.1.0，不能视为本次重新运行。
