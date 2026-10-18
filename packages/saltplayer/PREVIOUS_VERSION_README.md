# Chill With You 椒盐歌单本地导入版 v1.0.1

把 Salt Player（椒盐音乐）导出的路径歌单和电脑本地歌曲加入游戏原生列表，使用底部播放器切歌、暂停、拖动进度和调整音量。本版按用户需求实现本地导入，无需登录或安装 Salt Player，也不接管 Salt Player 程序。

## 一键安装

1. 下载 Release 中的 `ChillSaltPlayerNative-1.0.1-OneClick.zip` 并完整解压。
2. 保存并正常退出 Chill With You，运行 `ChillSaltPlayerSetup.exe` 或 `双击安装.cmd`，选择游戏目录后安装。
3. 从 Steam 启动游戏，按 **F6** 打开面板。选择歌单文件、单首歌曲或音乐文件夹，再点击“开始导入”。
4. 点击面板中的歌曲播放，或从游戏原生列表上方下拉菜单选择“椒盐音乐 · 歌单名”。

面板底部可隐藏入口，隐藏后仍可按 F6 打开，设置会保存。与此前网易云 F8、QQ F9、酷狗 F10、B站 F11、喜马拉雅 F7 版使用不同快捷键及曲目标签。共用 BepInEx；完整 ChillPatcher 需要先停用以免重复接管播放器。

## 导入歌单或文件夹

- 椒盐 TXT：每行一个音频文件路径，支持绝对路径；歌单与歌曲在同一目录时也可用相对路径。纯曲名清单、云端歌曲 ID 和数据库备份不属于本版支持的路径歌单。
- M3U / M3U8：支持相对、绝对路径、EXTINF 歌名/歌手/时长，保留原顺序和有意重复的歌曲。
- 音乐文件夹：递归导入支持的音频，按路径排序；跳过子目录的符号链接与目录联接。添加歌曲后重新导入同一文件夹更新。
- 单首歌曲：可以单独导入成一个歌单。

相同来源重新导入会更新已有歌单；导入失败保留原索引。歌单最多 10000 首，最多保存 1000 个歌单；匹配目录最多扫描 100000 个支持的音频文件及 10000 个目录，超限时会提示，请缩小目录范围。

TXT / M3U 默认读取 UTF-8，可识别 UTF-16 BOM；无效 UTF-8 会尝试 GB18030。路径可以含空格、中文及常用特殊字符。无 EXTINF 信息时，标题使用文件名，并将“歌手 - 歌名”形式拆开；本版不读取全部内嵌标签。

## 手机歌单路径如何匹配

先把自己的音频文件复制到电脑。导入手机 TXT 时，在“路径失效时的电脑音乐目录”选择对应目录。例如：

```text
手机歌单里的路径
/storage/emulated/0/Music/Artist/Album/song.flac

电脑上对应的歌曲
D:\Music\Artist\Album\song.flac
```

插件先检查原路径；不存在时按文件名在所选目录匹配，并比较目录后缀区分重名。同名仍无法确定时不会猜选，缺失、重名和不支持条目会计数提示，导入页可查看前 200 条详情。只把成功匹配的歌曲加入列表。

不支持 Android content:// 设备地址、UNC 共享路径、HTTP 音源和 HLS 播放列表。歌单只是索引，音频必须已保存在本机；它不会从歌单中下载音乐。

## 音频格式与长音频

允许 MP3、FLAC、WAV、M4A、AAC、WMA；实际解码取决于 Windows 媒体组件和文件编码，不包含椒盐播放器完整的解码引擎。APE、DSD、OPUS、加密缓存及受 DRM 保护的音乐不在本版支持范围。

直接从本地文件解码，不受此前云端插件 64 MB 下载限制。PCM 最大 1 GB，48 kHz 立体声约 90 分钟，高采样率支持时长更短。较长歌曲通过临时 PCM 文件读取；首次加载需要时间和临时磁盘空间。最多缓存 4 首，其中长音频最多 2 首，退出或释放缓存时清理。异常退出可能遗留以 ChillSaltPlayer- 开头的临时 PCM 文件，可在游戏关闭后通过系统临时文件清理移除。

只读原音乐，解码不会修改或删除源文件。移动音乐文件后请重新导入，必要时选择新的匹配目录。

## 保存、停用与构建

歌单索引保存在 `BepInEx/plugins/ChillSaltPlayerPlaylist/library/library.json`，上一次索引备份为 library.bak，包含本地路径和曲目名，请勿随安装包分享。更新及停用保留索引和原音乐。

退出游戏后运行 `停用插件.cmd` 可停用此插件。安装器内置官方 BepInEx 5.4.23.5 和 NAudio.Core / Wasapi 2.2.1 并校验固定 SHA256，安装和本地播放无需联网。独立文件选择器源码在 installer-source/FilePickerApp.cs，随安装复制到插件目录；没有后台网络服务。

使用 Windows .NET Framework 4.x 和本地游戏程序集构建：

```powershell
.\Build.ps1 -GameDirectory '游戏目录' -DependencyDirectory '含 NAudio.Core.dll 和 NAudio.Wasapi.dll 的目录'
.\BuildLauncher.ps1
.\UpdatePackageHashes.ps1
```

本项目为非官方社区适配，不包含 Salt Player 程序或其专有源码。许可证 GPL-3.0-or-later，开源来源及许可见 THIRD_PARTY_NOTICES.md、LICENSE 和 licenses/。


## 管理器 v1.2.0 随包界面更新

本包更新为 1.0.1，使用与游戏协调的深靛色半透明面板、浅紫文字、圆形入口和青色 / 洋红描边。F 键、面板位置拖动、隐藏入口、原生歌单和播放器逻辑保留。此版本更新界面样式，不包含对平台 HTTP 412 或酷狗 20028 限制的修复。

本次主题版随管理器 v1.2.0 分发，请从 https://github.com/daguoguoha/chill-with-you-music-manager/releases/latest 获取完整包；原独立仓库的旧包不包含本次主题更新。
