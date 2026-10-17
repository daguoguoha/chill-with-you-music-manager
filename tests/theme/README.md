# 游戏风格界面预览与构建验证

`PreviewUnity.cs` 提供 GDI 绘图适配器，`PreviewPlugin.cs.template` 提供合成账号、歌单和空回调。`RenderPanels.ps1 -Package <完整解压的管理器目录>` 编译并调用生产 `PlayerUI.cs`、`GameTheme.cs`、`Models.cs`，椒盐另外包含生产 `LocalImport.cs`。在 Windows .NET Framework 下执行，不需要游戏程序集或联网。

每个平台生成列表、登录、长提示和导入布局的 PNG；椒盐没有登录场景，合计 23 个视图。预览不接触用户账号，不发起请求，不启动游戏，不播放音频。预览中的账号已连接等状态只是合成测试条件。

这是布局和配色检查，不能代替 Unity 的实际渲染、透明叠加、键盘和鼠标输入验证。字体度量与抗锯齿也可能与 Unity 有差异。预览适配器是测试代码，不会被编译进正式插件。`render-results.json` 记录所生成的场景。

`GameTheme.cs` 是各平台内相同的原创程序绘图代码，生成圆角纹理、细描边、圆形青色 / 洋红色装饰。未复制或分发游戏的美术、字体与精灵资源。
