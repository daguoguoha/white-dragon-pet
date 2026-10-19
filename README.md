# Codex娘

[下载原生素材替换包](https://github.com/daguoguoha/white-dragon-pet/releases/download/native-materials-v1/Codex-Girl-Native-Materials-v1.zip) · [下载独立桌宠 v1.3.4](https://github.com/daguoguoha/white-dragon-pet/releases/download/v1.3.4/Codex-Girl-v1.3.4.exe)

白发紫瞳、龙角、小翅膀与尾巴的大头 Q 版动画宠物。简化了花纹、流苏与配饰，让小尺寸下的表情更清楚。

![宠物待机](previews/idle.gif)

[五种食物动画预览](previews/foods.html) · [全部互动动画预览](previews/interactions.html)（打开 HTML 即可播放）

## 素材

- [精灵图](assets/spritesheet.png)：透明 PNG，支持 v2 布局的 Pets 上传接口使用。
- [高清主造型](assets/main-look.png)：角色参考图。
- [完整动画预览](previews/all-states.gif) · [MP4](previews/all-states.mp4)
- [跳跃衔接](previews/idle-jump-idle.gif) · [视线循环](previews/look-loop.gif)

## 动作

包含眨眼待机、左右移动、招手、跳跃、惊讶失落、托腮等待、手柄工作、读书检查九种状态，以及十六个视线方向。

精灵图为 **1536×2288**，8 列、11 行，单帧 **192×208**，共 73 个有效帧。每行帧数为 `6,8,8,4,5,8,6,6,6,8,8`，其余单元透明。具体布局见 [layout.json](layout.json)。

## 独立桌宠 EXE

源码当前版本 **1.3.4**。已发布安装包见 [Releases](https://github.com/daguoguoha/white-dragon-pet/releases)；本地构建输出 `dist/Codex-Girl-v1.3.4.exe`，内置素材，双击运行。需要 Windows 的 .NET Framework 4.8，无需 Python、Git 或管理员权限。

独立版直接使用九组动作的高清原画，避免先缩到原生 192×208 单元再放大。基础动作、互动和五种食物缓存均为 **384×416**，显示尺寸保持 192×208 DIP；保留原有对齐和跳跃落点。EXE 内嵌 Per-Monitor V2 DPI 声明，适配 Windows 高分屏缩放。原图和裁剪说明见 [assets/desktop](assets/desktop/README.md)。

交互逻辑参考 [GenhuiC-sudo/11](https://github.com/GenhuiC-sudo/11) 的 Python 源码，并用 C# / WPF 接入本项目。参考项目的 MIT 声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)，也内置在 EXE 的「关于」菜单。

| 操作 | 效果 |
| --- | --- |
| 按住头部小幅来回移动 | 摸头、加心情、飘爱心；拖远则移动窗口 |
| 点击身体 / 拖动身体 | 戳一戳 / 移动宠物 |
| 右键 → 互动 | 夸夸、逗逗、五种食物、跳跃、手柄游戏、读书 |
| 右键 → 活动模式 | 原地陪伴、自由散步、跟随鼠标 |
| 右键 → 动画流畅度 | 目标 60 FPS 流畅模式 / 30 FPS 省电模式，记住选择 |
| 双击宠物 | 显示 / 隐藏额度卡片，记住显示状态 |
| 右键 → 和她聊天 / AI 设置 | 配置兼容接口后聊天 |
| 右键 → 查看天气 / 系统状态 | 手动查询城市天气、CPU 和内存状态 |
| 右键 → 鼠标穿透 / 开机启动 | 按需手动启用；托盘可解除穿透、找回宠物 |
| 卡片底部 → 导入到 Codex Pet | 将动画素材安装到原生宠物列表 |

心情范围 0–100，连续三次喂同一种食物会嫌腻；闲置 90 秒打瞌睡，互动会唤醒。贴底边托腮，贴顶边有倒挂效果。带气泡、粒子和动作切换淡入。移动、呼吸与粒子统一跟随 WPF 绘制刷新，默认目标 60 FPS；实际帧率受屏幕刷新、系统负载和图形性能影响。原画仍为每动作 4–8 帧，播放保留各帧停留时间，短暂延迟按经过时间追赶，不会通过加速动作冒充新增原画。互动在下一次绘制时切换，隐藏到托盘后暂停逐帧绘制，恢复后继续。

聊天需要自行配置接口根地址、模型和密钥，可能产生该服务的 API 费用。只有点击发送才请求模型，最多携带本窗口最近 40 条消息，记录不落盘；API Key 用 Windows 当前账户加密保存在本机。天气只有手动查询才把城市名发送给 wttr.in，不读取定位。CPU 和内存读取本机数据，GPU 温度暂不可用。

额度仍通过官方 App Server 读取 **Codex CLI 当前登录账户** 的剩余百分比和重置倒计时，每分钟刷新；不是 API 余额。额度读取不发起模型请求，不处理令牌文件，和自行配置的 AI 聊天分开。详见 [安装说明](install/README.md)。

这是独立桌面程序。导入 Codex 原生 Pet 后仅显示动画，原生窗口不会获得这些新互动。保留 Codex娘的形象，没有直接使用参考项目的角色立绘；新增六组专属动画（每组四帧，共 24 帧）：摸头闭眼歪头、拿点心进食、开心笑眼、生气抱臂、嫌腻托脸、闭眼休眠。五种食物现已各有四帧专属动作：小鱼干双手咬嚼、蛋糕用叉子吃、棒棒糖含糖回味、三色团子串签进食、钻石抱着吸收光芒。换食物立即切换道具和动作；连续三次同款仍会嫌腻。散步和跟随继续使用原有左右移动动画。

原生动画支持 v2 自定义宠物格式。在 **设置 → 宠物 → 刷新** 选择 **Codex娘**，输入 `/pet` 显示。本地安装不自动同步到网页账户。

## 检查与已知差异

素材已通过布局、透明背景、必需帧、跳跃落点和视线方向检查。少数近轴向眼神较细微；左半视线帧约小 6%，外轮廓略浅，循环时可见轻微姿态变化。

角色与动作由 imagegen 根据用户提供的参考图重新生成，再完成精灵图合成与检查。

## 从源码构建

在 Windows PowerShell 5.1 中运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File release/Build-Windows.ps1` 编译 Windows 程序，再用 Python 运行 `release/build_release.py` 打包。额度计算测试为 `tests/Quota.Tests.ps1`，导入、回滚与卡片隐藏测试为 `tests/Import.Tests.ps1`，均需要通过 Windows PowerShell 5.1 执行。`tests/Animations.Tests.ps1` 验证专属动画接线、24 帧变化和透明边界，并生成互动动画预览。互动、密钥加密和托盘测试为 `tests/Desktop.Tests.ps1`；运行 `python tests/network_fixture.py` 使用本机模拟服务验证聊天请求、取消、响应限制和重定向拒绝，不消耗模型额度。

`tests/Smoothness.Tests.ps1` 验证 30/60 FPS 节拍、重复绘制回调、逐帧时序、休眠到喂食的即时切换、卡顿追赶、隐藏与关闭时解除回调，并记录本机实际更新帧率。

`tests/Clarity.Tests.ps1` 验证 57 帧基础动作的高清缓存、透明轮廓、无绿边和原始对齐，生成同尺寸的清晰度对比。互动动画测试也验证所有 44 帧保持双倍像素且显示大小不变。

`runtime/build-info.json` 记录程序和对应源码的哈希。修改 C# 源码后必须重新编译；打包器会拒绝过期的二进制。GitHub Actions 使用已提交且哈希一致的 Windows 程序打包，版本号更新后自动生成 Release。

## 互动动画素材

独立 EXE 内置 [互动图集](assets/interactions-v2.png)，布局见 [interactions-layout.json](assets/interactions-layout.json)，生成过程和完整提示词见 [interactions-prompts.json](assets/interactions-prompts.json)。素材由内置 image_gen 生成，WPF 按状态裁帧并统一大小、脚底基线，原生 v2 精灵图保持不变。

呼吸、走路起伏、拖拽抖动和状态交叉淡入的公式参考大肥鱼项目 `paintEvent`，保留 MIT 声明。托盘控制窗口显示/隐藏，本身不是角色动画。

## 五种食物动画

[小鱼干](assets/foods/fish-v1.png)、[蛋糕](assets/foods/cake-v1.png)、[棒棒糖](assets/foods/lollipop-v1.png)、[团子](assets/foods/dango-v1.png)、[钻石](assets/foods/diamond-v1.png)各有四帧，共 20 帧新增素材，均内置于独立 EXE。图片由内置 image_gen 生成，布局见 [foods/layout.json](assets/foods/layout.json)，完整提示词见 [foods/prompts.json](assets/foods/prompts.json)。原生 Codex Pet 的固定 v2 精灵图保持不变。

## 只替换原生 Pet 素材

[native/README.md](native/README.md) 提供五种可替换的原生 v2 素材，保留同一个 Codex娘条目。互动槽显示摸头反应，等待槽显示闭眼休息，工作槽显示所选食物动画；保留原来的待机、移动、跳跃、读书和十六向视线。原生状态仍由 Codex 控制，不新增喂食菜单或睡眠计时器。

通过 Windows PowerShell 5.1 运行 native/Build-Materials.ps1 构建，再用 Python 运行 native/validate_materials.py 校验、native/package_materials.py 打包，输出 dist/Codex-Girl-Native-Materials-v1.zip。解压后双击对应食物的 Use-*.cmd 切换。安装仅更新新素材文件和 pet.json 的素材路径，保留原始精灵图、独立 EXE 及用户设置，替换前自动备份。

[原生槽位预览](native/preview.html) · [GitHub 参考项目与状态映射](native/README.md)
