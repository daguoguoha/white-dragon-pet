# Codex娘 · Quota 额度悬浮版

1. 将 ZIP **完整解压**到一个文件夹。
2. 双击 **Install.cmd**，安装后会自动打开额度宠物。
3. 她旁边会显示短期 / 每周等额度窗口的 **剩余百分比** 和 **重置倒计时**。数据来自本机 Codex CLI 登录的账户，每分钟刷新一次。

拖动宠物或卡片可以移动窗口；单击宠物招手；双击或点击 ↻ 刷新。右键调整大小、切换置顶或退出。手动刷新间隔至少十秒。断网或读取失败时会保留并标记旧数据；缺失数据不会当成 0%。

## 登录与数据

需要 Windows、桌面端附带的 codex.exe（或 PATH 上的 Codex CLI）和已登录的 ChatGPT 账户。若显示读取失败，先检查网络，并在终端运行 codex login 登录对应账户。CLI 和桌面端可能使用不同账户，本工具显示的是 **CLI 当前登录账户** 的 Codex 额度，并非 API 余额。

若找不到 CLI，可设置环境变量 CODEX_GIRL_CODEX_EXE 为 codex.exe 的绝对路径，再重新打开额度宠物。本工具不自动下载 CLI，不处理 API Key，不读取或复制令牌文件，不保存额度响应、邮箱或账户 ID。只通过官方 App Server 发送初始化和 account/rateLimits/read 请求，不发起模型对话。App Server 自身会使用 Codex 正常的登录和运行状态目录。

本机不联网也能完成安装；实时额度读取需要网络。无需管理员权限、Python 或 Git。

## 与原生宠物的关系

额度版是使用相同动画素材的**独立悬浮窗口**。原生宠物素材清单没有实时文字插槽，额度版不会接管原生聊天入口、任务状态或鼠标视线。它不会自动隐藏原生宠物；若出现两只，可从原生宠物菜单隐藏原生窗口。

原生动画也会一并安装。需要纯原生宠物时，关闭额度窗口，再到桌面端 **设置 → 宠物 → 刷新** 选择 **Codex娘**；必要时重启桌面端，输入 /pet 显示。需要支持 v2 自定义宠物的桌面端。本地安装不会自动同步到网页账户。

## 安装、升级与卸载

默认安装到 %USERPROFILE%/.codex/pets/codex-girl；配置了 CODEX_HOME 时使用该目录的 pets/codex-girl。只更新此宠物的清单、精灵图、额度程序与启动器。升级前自动备份已有文件到 pet-backups，重复安装同一版本不重复备份。升级前请退出已运行的额度窗口，再运行新版安装器。

下次启动可双击解压目录或安装目录中的 Start-Quota.cmd，也可直接运行 quota/CodexGirlQuota.exe。需要 Windows 的 .NET Framework 4.8。

位置、大小和置顶偏好保存在 %LOCALAPPDATA%/CodexGirlQuota/settings.json。不设置开机自启。解压后直接双击 Start-Quota.cmd 也可运行，不必安装。

需要只安装、不启动或使用自定义目录时，可运行 powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -CodexDirectory "D:\MyCodex"。直接运行 Install.ps1 只复制文件；Install.cmd 会在成功后启动额度宠物。

卸载时退出额度窗口并在设置中切换到其他宠物，然后删除 codex-girl 安装文件夹。备份及偏好设置可自行保留。

接口说明见 [官方 App Server 文档](https://learn.chatgpt.com/docs/app-server)，原生宠物说明见 [OpenAI Pets 文档](https://learn.chatgpt.com/docs/pets)。
