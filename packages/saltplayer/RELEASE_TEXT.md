椒盐音乐路径歌单与电脑本地歌曲接入 Chill With You 原生播放器。

- 导入 TXT、M3U/M3U8、单首歌曲和音乐文件夹，保留歌单顺序及重复歌曲。
- 手机路径失效时，可选择电脑音乐目录匹配；缺失及同名歧义会提示。
- 原生上一首、下一首、暂停、拖动进度和列表选歌已游戏实测。
- **F6** 打开面板，入口可隐藏；无需登录 Salt Player。
- 官方依赖已内置，支持离线安装；音乐仅在本机读取。

下载 `ChillSaltPlayerNative-1.0.0-OneClick.zip`，完整解压，保存并正常退出游戏后运行 `ChillSaltPlayerSetup.exe`。安装后从 Steam 启动游戏，按 F6 选择歌单及本地音乐。

歌单必须包含本地文件路径，音频需要先复制到电脑；云端 ID、数据库备份、网络音源与加密缓存不支持。允许 MP3/FLAC/WAV/M4A/AAC/WMA，实际编码支持取决于 Windows，当前实际音频测试为 WAV。PCM 上限 1 GB，首次解码需要临时磁盘空间。详细验证范围见 TESTING.md。

GPL-3.0-or-later。源码、构建脚本、开源来源和许可证随包提供，使用 BepInEx、NAudio 及此前原生播放器适配代码，参考 SaltPlayerConverter 的路径 TXT 格式。未包含 Salt Player 专有程序、账号、音乐或游戏存档。
