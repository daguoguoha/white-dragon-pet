# Codex娘 · 原生素材替换版

此包只替换 Codex 原生 Pet 的图片和素材路径，不修改客户端，不安装后台程序，不新增宠物条目。

## 使用

双击食物对应的 CMD，例如 `Use-fish.cmd`。到 Codex **设置 → 宠物 → 刷新**，重新选择 **Codex娘**。如果仍显示缓存中的旧图，可先切到其他宠物再切回。

五个版本共用同一个宠物 ID `codex-girl`，每次仅安装所选版本。默认版本为小鱼干。也可运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Materials.ps1 -Food cake
```

`Food` 支持 `fish`（小鱼干）、`cake`（蛋糕）、`lollipop`（棒棒糖）、`dango`（团子）、`diamond`（钻石）。指定 `-CodexDirectory` 可使用其他绝对路径；默认遵循 `CODEX_HOME`。

## 素材映射

| Codex 固定槽位 | 显示画面 |
| --- | --- |
| idle | 原来的眨眼待机；保留 v2 中性姿态 |
| running-right / running-left | 原来的左右移动 |
| waving | 闭眼歪头的摸头反应 |
| jumping | 原来的跳跃庆祝 |
| failed | 抱臂生气 |
| waiting | 闭眼休息 |
| running（工作中） | 当前食物版本的进食 / 吸收光芒画面 |
| review | 原来的读书检查 |
| 两行 look | 原来的十六向视线 |

这些是预设槽位的画面替换。具体触发仍由 Codex 控制：闭眼画面不等于新增了闲置计时器，摸头画面不等于新增了触摸检测，原生窗口没有新增食物选择菜单。切换食物需要运行对应 CMD 替换素材。钻石表现为抱着吸收光芒。

v2 格式为 1536×2288，8 列×11 行，单帧 192×208。固定行帧数不变，四帧素材按行的有效槽位重排，不在未用槽中塞入新状态。

## 安装与恢复

安装前备份原 `pet.json` 和已存在的 `spritesheet-native.png` 到 `pet-backups/native-materials-*`。新素材写入 `pets/codex-girl/spritesheet-native.png`，原始 `spritesheet.png`、独立 EXE、用户偏好及宠物 ID 保持原样。安装失败时恢复原素材。

恢复时从对应备份复制 `pet.json` 回原目录即可恢复原来的素材路径；之前也使用原生素材版时，同时恢复备份中的 `spritesheet-native.png`。再次导入独立 EXE 内置的基础素材，也会恢复原始动画映射。

## GitHub 参考

- [CosmicCoderDev/codex-pet-lab](https://github.com/CosmicCoderDev/codex-pet-lab)：实际读取了 `src/pet-contract.js`，核对 v2 尺寸、状态顺序、有效帧数和 `idle[6]` 中性帧槽位。未使用该项目的角色图片。
- [leanyu165-gif/mypet-codex-pet](https://github.com/leanyu165-gif/mypet-codex-pet)：将现成动画按状态映射后重建原生图集的实践。
- [312362115/codex-pet](https://github.com/312362115/codex-pet/blob/main/docs/specs/2026-07-13-native-pet-v2.md)：原生 v2 适配和验收规范。
- [himomohi/codex-pet-hud](https://github.com/himomohi/codex-pet-hud)：在真实原生宠物两侧显示额度的伴随 HUD，仍有额外程序，未安装或使用其令牌读取逻辑。
- [VectorPeak/Codex-Pet-Live](https://github.com/VectorPeak/Codex-Pet-Live)：独立桌宠运行时，可加载 Codex 素材，属于另一类方案。

在已检查的这些项目中，没有验证到通过仅替换图片给原生 Pet 增加完整喂食 / 心情事件系统的实现。
