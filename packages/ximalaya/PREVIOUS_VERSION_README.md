> 以下内容属于历史版本，不能视作当前候选版已完成相同实测。当前验证见 TESTING.md。

# Chill With You 喜马拉雅原生播放器 v1.0.0

把喜马拉雅专辑、播客、有声书和单集声音加入游戏原生曲目列表，使用底部播放器播放、暂停、切换节目、拖动进度和调整音量。

## 一键安装

1. 下载 Release 中的 `ChillXimalayaNative-1.0.0-OneClick.zip`，完整解压，保留 offline-dependencies 文件夹。
2. 保存并正常退出游戏，运行 `ChillXimalayaSetup.exe` 或 `双击安装.cmd`，选择包含 Chill With You.exe 的游戏目录，点击安装 / 更新。
3. 从 Steam 启动游戏，按 **F7** 打开面板。免费内容可直接导入；用自己的喜马拉雅手机 App 扫码登录后，可读取“我的订阅”和“喜欢的声音”。
4. 选择专辑或声音后，在游戏原生列表的下拉菜单选择“喜马拉雅 · 专辑名”，点击节目即可播放。

面板底部可隐藏游戏内入口，隐藏后仍可按 F7 打开，设置会保存。快捷键避开网易云 F8、QQ F9、酷狗 F10、B站 F11 和 Steam F12 截图；可与这些独立插件共用 BepInEx。完整 ChillPatcher 需要先停用，以免重复接管播放器。

## 导入内容

点击“导入专辑 / 声音”，支持以下输入：

- 专辑 ID，例如 `2677356`，或 `album:2677356`。
- 专辑链接，例如 `https://www.ximalaya.com/album/2677356`。
- 声音链接，例如 `https://www.ximalaya.com/sound/25202`，或 `track:25202`。
- 喜马拉雅 share/album、share/sound 和旧版分类/专辑/声音链接；xima.tv 与站内 short 分享链接按受限域名跳转解析。如果平台返回网页而非跳转，请复制完整专辑或声音链接。

订阅、喜欢的声音和专辑均分页读取，最多 10000 项；接口未返回、失效或视频条目不加入列表，实际数量可能少于页面总数。导入不会改变手机端的订阅或喜欢列表，也不会上报收听历史。

登录仅使用当前账号原有播放权限。付费和会员声音是否可播由喜马拉雅服务端判定，本插件不提供解锁功能。只解析普通音频地址，不解密受保护的音频文件。账号无权限、音源未返回或内容已下架时会提示或跳过。

## 较长节目

点播时先加载并解码该集音频，短节目使用内存，较长节目使用本地临时 PCM 文件按需读取。首次载入长节目需要时间和临时磁盘空间，载入期间可以选择其他节目取消本次加载。

单段编码音频上限 64 MB，解码 PCM 上限 1 GB，48 kHz 立体声约 90 分钟。实际可支持时长也取决于音质与声道，超限时请选择较短节目。最多缓存 4 集，其中长节目最多 2 集；释放缓存或正常退出时清理临时文件。异常退出可能遗留以 ChillXimalaya- 开头的临时 PCM 文件，可在关闭游戏后通过系统临时文件清理移除。

## 账号、更新与停用

登录信息使用当前 Windows 用户 DPAPI 加密，保存在 `BepInEx/plugins/ChillXimalayaPlaylist/account/session.dat`。换电脑或 Windows 用户后需重新扫码。安装包不含账号、音频或游戏存档；更新与停用保留这些数据。请勿分享自己运行后生成的 account 文件夹。

退出游戏后运行 `停用插件.cmd`，只停用本插件，保留共享 BepInEx 框架。

## 离线安装与构建

官方 BepInEx 5.4.23.5、NAudio.Core / Wasapi 2.2.1 已内置并校验固定 SHA256，安装无需联网。登录、导入和播放仍需联网。二维码由喜马拉雅官方接口直接返回，无第三方二维码服务。

Windows x64，需要系统媒体功能。源码在 source/，安装器源码在 installer-source/，使用本地游戏程序集构建：

```powershell
.\Build.ps1 -GameDirectory '游戏目录' -DependencyDirectory '含 NAudio.Core.dll 和 NAudio.Wasapi.dll 的目录'
.\BuildLauncher.ps1
.\UpdatePackageHashes.ps1
```

游戏更新或喜马拉雅接口变化可能需要适配。界面会显示超时、DNS、代理、TLS、HTTP 状态或接口错误码，不在日志中记录登录凭据与音频签名 URL。

本项目为非官方社区适配，与游戏或喜马拉雅没有官方合作关系。许可证 GPL-3.0-or-later；开源来源、版权与原始许可见 THIRD_PARTY_NOTICES.md 和 licenses/。
