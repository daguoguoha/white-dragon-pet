# Codex娘 · Windows 一键安装包

1. 将 ZIP **完整解压**到一个文件夹。
2. 双击 `Install.cmd`，等待显示安装成功。
3. 打开 ChatGPT / Codex 桌面端的 **设置 → 宠物 → 刷新**，选择 **Codex娘**。若没有出现，重启桌面端后再选择，输入 `/pet` 显示。

安装无需管理员权限、Python、Git 或联网。默认安装到 `%USERPROFILE%\.codex\pets\codex-girl`；若配置了 `CODEX_HOME`，则使用该目录的 `pets\codex-girl`。安装器只写入此宠物的两个文件，升级前自动备份已有文件到 `pet-backups`，重复安装相同版本不会重复备份。

支持 **v2 自定义宠物**的 Windows 桌面端。包含九种动画状态和十六个视线方向，透明 PNG 为 1536×2288。网页端和只支持九行动画的旧版 CLI 不使用此本地安装包；此安装也不会自动同步到网页账户。

安装会添加本地宠物，不会自动更改当前选择。已经通过 Pets 工具安装过的账户条目与此本地条目可能同时出现在列表中。

需要自定义目录或静默安装时：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -CodexDirectory "D:\MyCodex" -NoOpenFolder
```

卸载时先在设置中切换到其他宠物，再删除安装目录中的 `pet.json` 和 `spritesheet.png`，最后删除空的 `codex-girl` 文件夹。备份文件可自行保留。

格式依据：本机桌面端的 v2 清单解析与尺寸校验；使用说明参考 [OpenAI Pets 文档](https://learn.chatgpt.com/docs/pets)。
