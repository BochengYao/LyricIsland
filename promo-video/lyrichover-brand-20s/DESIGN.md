# Lyric Hover 歌词岛品牌片视觉规范

## Style Prompt

沿用现有歌词岛平面宣传系列的编辑式品牌语言：米白与近黑构成主要画布，超大粗体中文负责叙事，珊瑚橙细线在留白中连续穿行，真实歌词岛界面与蓝、青、紫、橙抽象界面图形承担产品展示。画面应克制、精确、连续且以产品为中心，空间运动来自同一条路径、同一个产品对象和轻微镜头位移，不创造另一套视觉身份。

## Colors

| Token | Hex | Role |
| --- | --- | --- |
| `ivory` | `#FCFBFA` | 主米白背景 |
| `ink` | `#141413` | 主文字、黑色场景和歌词岛 |
| `coral` | `#F38F61` | 全片连续引导线与节奏节点 |
| `deep-purple` | `#1C172E` | 现有界面中的紫色暗部 |
| `warm-muted` | `#E8E2DA` | 黑底辅助文字与弱化英文 |
| `white` | `#FFFFFF` | 高亮歌词与黑底正文 |

蓝、青、紫、橙抽象图形只从现有 PPT 素材中取用，不为视频另造新的渐变体系。

## Typography

- 中文大标题：PPT 内嵌的 `字由点字典黑 Bold`，项目字体族名为 `LyricHover Zidianhei`、字重 900。
- 中文正文：PPT 内嵌的 `字由点字典黑`，项目字体族名为 `LyricHover Zidianhei`、字重 400。
- 英文辅助标题：PPT 内嵌的 `阿里巴巴普惠体 Bold`，项目字体族名为 `LyricHover Puhuiti`、字重 700。
- 标题使用 900 视觉重量，字距约 `-0.04em`；正文使用 350 到 400 视觉重量。
- 横版标题不小于 88px，产品内歌词不小于 30px，辅助标签不小于 20px。
- PPT 的 EOT 容器声明 `fsType=0x0000`，允许安装式嵌入。三套实际使用的字体已提取为项目本地 TTF/OTF；运行时通过覆盖全片文案字形的 WOFF2 子集和 `@font-face` 固定加载，不依赖当前 Windows 字体注册表，也不触发大文件回退。

## Layout

- 画布：1920×1080，60fps，20 秒。
- 标题锚定左上或左中，不使用每幕都居中的网页式堆叠。
- 每幕至少包含背景层、产品层和珊瑚橙引导层。
- 画面保留大面积留白；产品界面最多占画面约 65%，视觉高潮除外。
- 9:16 适配通过场景锚点和产品状态表重排，不直接裁切横版成片。

## Persistent Visual Objects

1. `#global-coral-thread`：同一条 SVG 路径贯穿 0–20 秒，镜头与节点沿其推进。
2. `#lyric-island`：同一个 DOM 对象贯穿 0–18.5 秒，通过 wrapper transform、遮罩、内容槽和圆角变化展示功能；禁止按场景替换整个产品对象。

## Motion Character

- 关键词：smooth、restrained、editorial、spatial、continuous、product-focused。
- 入场采用 300–700ms 的 `power3.out`、`expo.out`、`sine.out` 等自然减速。
- 位置变化采用 600–1000ms 的 `power3.inOut` 或 `sine.inOut`。
- spring 只用于歌词岛布局完成后的极轻微 1% 到 2% 回弹，禁止 bounce 或 exaggerated elastic。
- 转场由世界平面移动、产品 Morph、遮罩揭示或焦点推进完成。除最终场景外，不单独把旧场景元素先淡出。

## What NOT to Do

- 不使用科技蓝主视觉、霓虹、粒子、HUD、赛博朋克或扫描线。
- 不大量使用发光、镜头光晕、玻璃反射、模糊或运动模糊。
- 不重新绘制产品 UI，不虚构按钮、播放器状态或 Windows 界面。
- 不把七张海报按页轮播，不使用“大标题 + 截图”重复模板。
- 不让播放器 Logo 占满画面，不把平台名称变成主角。
- 不使用 glitch、粒子爆炸、夸张 3D 旋转或大量快速硬切。

## Visible Naming

- 品牌：`Lyric Hover 歌词岛`
- 功能：`歌词坞`、`逐字跟随`
- 官网：`lyric-island.top`
