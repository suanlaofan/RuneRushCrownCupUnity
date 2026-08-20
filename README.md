# 符文疾驰：王冠杯（Rune Rush: Crown Cup）

西式奇幻第一人称道具竞速游戏，使用 Unity `6000.5.4f1`、URP `17.5.0` 开发，并已适配 PICO XR。默认比赛场景为“高岩王冠赛道”。

> PICO XR SDK 不随本仓库分发。请从 PICO 官方渠道取得与项目匹配的 SDK 版本，并安装到 `Packages/com.bytedance.pico.xr`；`Packages/manifest.json` 已保留本地包引用。该目录被有意忽略，避免分发 SDK 与许可材料。

## PICO XR 体验

- 固定驾驶舱镜头：镜头固定在赛车驾驶位，赛车会沿赛道整体前进；头显自然 6DoF 追踪用于自由观察，不会由比赛输入平移镜头。
- 右手食指扳机：加速。
- 右手 Grip／握把键：氮气（对应原 Unity 桌面版的 `Shift`）。
- 全部 UI（开始菜单、赛中 HUD、暂停、结算）采用统一的 World Space 画布，并整体放大为 2×。

最终 PICO APK 以 GitHub Release 附件提供，不提交到 Git 历史。

## 已实现

- 三圈赛事、玩家与 5 名 AI、侍从／骑士／领主三档难度。
- 漂移蓄能、氮气、尾流、护盾、伤害、计分、排名、圈速与结算。
- 6 类圣物：疾风药剂、守护结界、寂静符文、狮鹫之矢、诅咒石、牧师恩典。
- 12 个拾取物、18 个障碍、5 个增益板、动态障碍、中文 HUD／菜单／暂停／结算，以及桌面、手柄、触控和 XR 输入。
- 第一人称座舱、PICO XR 相机与控制器输入桥接；XR 使用固定驾驶舱层级与自然头部观察。
- 持久赛道控制点、永久场景环境根，以及会同步到运行时玩法的道具／障碍／增益板标记。

## 快速开始

1. 在 Unity Hub 打开本项目根目录。
2. 使用 Unity `6000.5.4f1` 打开项目，等待 Package Manager 完成解析。
3. 打开 [`Assets/_RuneRush/Scenes/RuneRushRace.unity`](Assets/_RuneRush/Scenes/RuneRushRace.unity)。
4. 若 Hierarchy 中尚未出现 `RACE LAYOUT - Editable Markers`，执行 `Rune Rush > Scene Editing > Create Default Editable Layout` 一次，然后按 `Cmd+S` 保存场景。
5. 展开 `AUTHORED ENVIRONMENT > BAKED SAMPLE ENVIRONMENT`，即可直接选择和编辑道路、城堡、树木、塔楼、山体与灯光；自己的对象放入 `CUSTOM CONTENT - Safe From Rebuild`。
6. 按 Play 可从赛事台开始比赛；完整的美术、场景、控制和 PICO 构建说明见 [`Assets/_RuneRush/Documentation/替换与部署指南.md`](Assets/_RuneRush/Documentation/替换与部署指南.md)。

## 在 Scene 窗口编辑场景

选中 `Track Authoring - Replace Control Points`，Inspector 中的 `Focus` 会框选整条赛道，`Refresh Preview` 会显示不参与构建的道路／路肩预览。直接拖动编号控制点即可改走线。

选中 `Scene Bindings - Replace Prefabs Here`，其子层级分为两类：

```text
Scene Bindings - Replace Prefabs Here
├─ AUTHORED ENVIRONMENT - Put Editable Models Here  ← 永久的地形、建筑、灯光、布景
├─ RACE LAYOUT - Editable Markers                   ← 可移动、复制、删除的玩法位置
├─ EnvironmentRoot                                  ← 运行时生成，勿手工放模型
├─ RaceObjectRoot                                   ← 运行时生成，勿手工放模型
└─ RacerRoot                                        ← 运行时生成，勿手工放模型
```

- 将手工导入的地形、道路、城堡、树木、灯光和静态装饰放在 `AUTHORED ENVIRONMENT` 下；这些对象会保存进场景，比赛运行时不会清除它们。
- 场景已包含 `BAKED SAMPLE ENVIRONMENT - Edit or Replace`。其中 `GENERATED SAMPLE GEOMETRY - Rebuildable` 是可直接选中的西式奇幻样例道路与环境，`CUSTOM CONTENT - Safe From Rebuild` 专门存放自己的模型。
- 选中 `Scene Bindings` 后可用 `Bake Editable Sample Environment` 重建样例实体、`Frame Baked Sample` 框选全景。重建只替换生成组，不会删除 `CUSTOM CONTENT`。
- 在 `RACE LAYOUT - Editable Markers` 下移动、复制或删除标记根对象，即可调整拾取物、障碍和增益板的位置与数量。每个标记都有彩色默认子模型 `EDITOR VISUAL - Replace With Your Model`，可删除并替换为自己的参考模型。
- 选择标记根对象后，Inspector 可设置 `Kind`，以及障碍种类／增益板种类。位置会在移动、手动同步或保存场景时投影回赛道可行驶范围。
- 按 `Cmd+S` 会自动将所有标记同步为赛道相对坐标。运行时读取的是该已保存布局，因此调整位置后务必保存再测试。

`Rune Rush > Scene Editing > Create Default Editable Layout` 会重建默认的 `12` 个拾取物、`18` 个障碍和 `5` 个增益板标记，并覆盖当前所有赛事标记；只应在初始创建或明确要恢复默认布局时使用。常规编辑请使用 Inspector 的添加、同步、框选和清理按钮。

## 模型替换原则

- **静态场景模型**：直接放在 `AUTHORED ENVIRONMENT` 下，便于在 Scene 窗口增删、定位和调灯。
- **玩法位置参考模型**：放在对应赛事标记根对象下；根对象上的 `RuneRushRaceLayoutMarker` 组件必须保留。
- **比赛运行时模型**：在 [`RuneRushArtProfile.asset`](Assets/_RuneRush/Settings/RuneRushArtProfile.asset) 的 `Race Objects`、`Vehicles` 和 `Scene` 槽位中指定。标记的子模型决定 Scene 编辑时看见什么；Art Profile 决定运行时动态生成物使用什么 Prefab。
- 自制环境已经包含道路／地形时，可在 Art Profile 关闭 `Generate Procedural Track`／`Generate Procedural Environment`；使用自己的灯光时关闭 `Override Scene Lighting`。

## 目录

| 位置 | 用途 |
| --- | --- |
| `Assets/_RuneRush/Scenes/RuneRushRace.unity` | 唯一比赛场景，已加入 Build Settings。 |
| `Assets/_RuneRush/Settings/RuneRushArtProfile.asset` | 车辆、座舱、动态比赛物件、环境 Prefab 与材质的替换入口。 |
| `Assets/_RuneRush/Scripts/Core`、`Simulation`、`Track` | 与美术解耦的赛事规则、可保存布局和闭合赛道数据。 |
| `Assets/_RuneRush/Scripts/Runtime/Presentation` | 驾驶呈现、场景绑定、赛道控制点和布局标记组件。 |
| `Assets/_RuneRush/Editor` | Scene 编辑工具、赛道预览，以及 PICO Android 配置／构建菜单。 |
| `Assets/_RuneRush/Documentation/替换与部署指南.md` | 面向后续美术与关卡制作的完整操作指南。 |
| `BuildArtifacts/Logs` | 编译、测试、PICO 配置和构建日志。 |

## 保存与构建安全性

- `Rune Rush > Project > Create or Refresh Race Scene` 是**重新生成工具**：它会新建并覆盖 `RuneRushRace.unity`。在已开始手工摆放场景后不要执行它；如确需重置，先复制场景备份。
- `Rune Rush > PICO > Configure Android Project` 与 `Rune Rush > PICO > Build Android APK` 仅在比赛场景文件不存在时才创建示例场景；对已经保存的 `RuneRushRace.unity` 不会重建或覆盖编辑内容。
- PICO 构建会读取当前已保存的场景。因此编辑赛道、布局标记或环境后，先 `Cmd+S`，再构建即可。

## 当前验证状态

- PICO 最终构建已成功：`Build Finished, Result: Success.` 与 `RUNE_RUSH_PICO_BUILD_OK` 记录于本地构建日志（构建日志不随仓库发布）。
- 最终 APK 的包名为 `com.runerush.crowncup`，启动 Activity 为 `com.unity3d.player.UnityPlayerActivity`。
- 已完成 PICO 模拟器运行期与用户视觉验收：固定驾驶舱、自然转头、分区 HUD、2× UI 与赛道高度同步均已复核。

## 注意事项

- 赛道规则以控制点与弧长为基准；保留至少 4 个有序控制点并保持 `Closed` 启用。
- 赛事标记会限定在约 `±9m` 的逻辑赛道横向可行驶范围内。改变赛道走线后，使用 `Synchronize Markers → Layout` 或保存场景，让标记重新投影到新赛道。
- 本仓库忽略所有 keystore、证书和私钥。请在本地 Player Settings 中配置自己的签名材料，绝不要提交密码或私钥。
