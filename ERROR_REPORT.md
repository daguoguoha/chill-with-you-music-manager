# 错误报告导出

管理器2.0新增“导出错误报告”。扫描游戏目录后点击该按钮，选择游戏目录之外的ZIP文件名。无需先关闭游戏；导出只读游戏文件，不修改账号或插件。已有同名报告不会覆盖，请换一个名称。报告不会自动上传。

ZIP中包含report.json和说明.txt。report.json记录随包版本、已安装程序集版本、启停状态、目标文件SHA256与完整性、Windows/CLR/媒体组件是否存在、代理环境变量是否配置，以及最近错误的固定分类、异常类型和可识别阶段/状态码。哈希不匹配也可能是安装了其他版本，应结合版本判断。

日志按最多8MB/最近5000行读取，只保留最多200条结构化错误。缺失或正在被独占的日志仍能导出部分报告；LogState和LogTruncated说明是否缺失、不可读或截断。无法读取的插件文件单独标记unavailable。报告不包含原始日志、账号/配置内容、Cookie、二维码/密钥、歌曲信息、完整网址、本机路径和代理地址。

CLI示例：`powershell -NoProfile -ExecutionPolicy Bypass -File Manager.ps1 -Action Export -GameDirectory "D:\Games\Chill with You Lo-Fi Story" -OutputPath "D:\Reports\ChillMusic.zip"`。

测试入口为tests/DiagnosticsTests.ps1，覆盖敏感信息哨兵、共享读、日志缺失、边界截断、错误分类、文件损坏、停用状态、无覆盖/无修改及真实界面渲染。
