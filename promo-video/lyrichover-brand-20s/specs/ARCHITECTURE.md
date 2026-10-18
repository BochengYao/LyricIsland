# 第一阶段结构方案

状态：等待结构确认。此阶段不输出最终 MP4，也不把占位画面当作完成动画。

当前预览只包含结构级 seek 证明：全局世界平面在 20 秒内线性扫过七个区域，歌词岛有 120px 的测试位移，珊瑚路径持续绘制。这些测试 tween 用于证明时间轴可确定性寻址并通过 HyperFrames 检查，阶段二会被逐场景正式节奏替换。

## 1. 项目结构

```text
lyrichover-brand-20s/
├─ index.html                         # 1920×1080 根构图与持久对象
├─ fonts.css                          # 本地 WOFF2 品牌字体子集
├─ DESIGN.md                          # 品牌视觉和动效硬约束
├─ specs/ARCHITECTURE.md              # 本结构方案
├─ assets/
│  ├─ source-ppt/                     # 从原 PPT 原样提取的可追溯素材
│  │  ├─ SOURCE.md
│  │  ├─ manifest.json
│  │  ├─ vectors/                     # 15 个 SVG
│  │  └─ raster/                      # 27 个 PNG、1 个 JPEG
│  ├─ fonts/                          # 从 PPT 合法嵌入数据提取的品牌字体
│  └─ production/
│     ├─ icons/                       # 歌词岛原始 SVG 控件元素
│     ├─ scenes/                      # 现有桌面、窗口和任务栏场景
│     ├─ ui/                          # 歌词岛现有状态图
│     └─ vectors/                     # 全局线参考、椭圆和播放器轮廓
├─ scripts/
│  ├─ timeline-plan.js                # 20 秒时间轴和每幕职责
│  ├─ morph-contracts.js              # 同一产品对象的状态契约
│  ├─ phase1-audit.mjs                # 第一阶段七项要求的可重复审计
│  └─ render-final.ps1                 # 60fps MP4 的本地编码器解析与渲染包装
└─ styles/
   ├─ tokens.css                      # 品牌颜色、字体、尺寸 token
   └─ structure.css                   # 连续世界与持久层布局
```

## 2. 核心架构决定

全片只使用一个根构图 `main`。七个 Scene 是主时间轴上的逻辑阶段，不拆成七个彼此隔离的 HyperFrames 子构图。

原因：珊瑚橙路径与歌词岛必须保持同一 DOM 身份。如果把每幕做成独立子构图，产品会在边界被销毁和重建，无法证明 Scene 03、Scene 06 的 Morph 是同一个对象连续变化。

根构图分为五层：

1. 世界背景层 `#world`
2. 环境层 `#scene-environments`
3. 全局路径 `#global-coral-thread`
4. 持久产品层 `#product-plane > #lyric-island`
5. 文案层 `#copy-plane`

相机运动通过移动世界平面实现。歌词岛位于独立产品平面，因此可在镜头移动时保持稳定，也可在 Scene 06 单独向任务栏位置 Morph。

## 3. 20 秒时间轴

| Scene | 时间 | 时长 | 职责 | 主要连续动作 |
| --- | ---: | ---: | --- | --- |
| 01 | 0.0–2.0s | 2.0s | 建立品牌世界，0.5–1s 内看到产品 | 路径绘制、轻微相机跟随、歌词岛从顶部展开 |
| 02 | 2.0–5.0s | 3.0s | 展示鼠标靠近时的避让 | 世界平面沿路径推进；歌词岛同一对象降低并恢复透明度 |
| 03 | 5.0–9.0s | 4.0s | 展示紧凑、展开、重排和自动折叠 | wrapper 尺寸与内容遮罩连续 Morph，不切换整张截图 |
| 04 | 9.0–12.0s | 3.0s | 展示播放器切换与 SMTC | 产品位置稳定；只替换内容槽，平台名沿路径依次出现 |
| 05 | 12.0–16.0s | 4.0s | 逐字跟随高潮 | 相机推进歌词区；词语由灰到白；橙色节点与节拍同步 |
| 06 | 16.0–18.5s | 2.5s | 歌词岛进入任务栏并形成歌词坞 | 同一 wrapper 缩小、压扁、下移、吸附；镜头同时向下 |
| 07 | 18.5–20.0s | 1.5s | 品牌收束 | 最终场景才允许 UI 与背景退场；路径完成末段弧线 |

最终品牌画面至少保持 0.8 秒。

## 4. Scene 职责边界

### Scene 01

- 只建立世界、产品和主标题。
- 标题与歌词岛同步入场；产品最迟在 1.0 秒完全可辨。
- 不加入纯标题前奏。

### Scene 02

- 引入现有桌面或软件窗口环境。
- 鼠标仅作为交互触发提示，不成为主视觉。
- 文案只保留“靠近时，让开一点。”

### Scene 03

- 负责产品外壳宽度、圆角、内容排列和遮罩变化。
- 现有 `island-layout-states.png` 作为三个布局状态的来源，阶段二从同一源图裁出状态片段或使用原 SVG 控件重排。
- 不使用三张完整截图硬切。

### Scene 04

- 产品外壳位置和尺寸尽量稳定。
- 专辑封面、歌曲名、播放状态在 `#lyric-island-content-slot` 内更新。
- Apple Music、QQ 音乐、网易云音乐、Spotify 与 `SMTC` 是辅助信息。

### Scene 05

- 逐字跟随是视觉高潮，保留 4 秒。
- 歌词文本固定为 `Follow the quiet and let your own light rise`。
- 高亮采用每词遮罩和颜色过渡；仅当前词允许极轻微亮度扩散。

### Scene 06

- 只允许变换现有 `#lyric-island`，不得创建第二个歌词坞替身。
- 从顶部状态连续变成底部任务栏状态，任务栏素材在吸附接近完成时由遮罩揭示。

### Scene 07

- 只保留品牌、收束文案与官网。
- 允许最终淡出，但保持约 0.8–1.0 秒可读。

## 5. 跨 Scene 持续存在的元素

| 元素 | 存续时间 | 不变量 | 可变化属性 |
| --- | --- | --- | --- |
| `#global-coral-thread` | 0–20s | 同一条 SVG path | `stroke-dashoffset`、整体 `x/y`、节点透明度与小幅缩放 |
| `#lyric-island` | 0–18.5s | 同一 DOM wrapper、同一产品身份 | `x/y`、`scaleX/scaleY`、圆角、遮罩、透明度 |
| `#lyric-island-content-slot` | 0–18.5s | 固定在产品 wrapper 内 | 内容片段的遮罩、颜色和局部位置 |
| 世界平面 `#world` | 0–20s | 单一连续空间 | `x/y` 与轻微 `scale` |

场景文案与局部环境不要求跨幕持续，但它们的离场由世界移动或场景遮罩承担，不能先独立淡空再转场。

## 6. SVG 与位图使用

### 使用 SVG

- 全局珊瑚橙路径与节奏节点：内联 SVG，便于 `stroke-dashoffset`。
- 歌词岛内的 13 个原始控件/图标：来自 PPT 中的原 SVG。
- 播放器名称胶囊轮廓：`player-pill-outline.svg`。
- 珊瑚橙椭圆装饰：`coral-oval.svg`。

### 保留为位图

- 真实桌面、Windows 任务栏和软件窗口背景。
- 蓝、青、紫、橙抽象界面图形。
- 现有歌词岛状态图，作为内容和比例的权威来源。

位图只做 wrapper transform、裁切、遮罩和轻微视差，不重新绘制其中的产品界面。

## 7. 动画技术分配

| 内容 | 技术 |
| --- | --- |
| 主标题 | mask reveal + opacity，300–600ms |
| 珊瑚橙路径 | SVG `stroke-dasharray/stroke-dashoffset` |
| 相机跟随 | `#world` transform，`power3.inOut` |
| 歌词岛展开和任务栏吸附 | wrapper transform + clip-path + border-radius Morph |
| 布局重排 | transform + mask，核心段 700–1000ms |
| 鼠标避让 | opacity 1 → 0.34 → 1，产品节点保持存在 |
| 播放器内容切换 | 内容槽内短遮罩交接，不替换产品外壳 |
| 逐字跟随 | 每词颜色与遮罩过渡，节拍点来自预计算时间表 |
| 背景层 | 轻微 parallax 与 scale，避免过量 blur |
| 最终收束 | 仅 Scene 07 使用 opacity exit |

## 8. 场景连续过渡

- 01→02：相机沿同一橙线向右推进，产品保持顶部位置；桌面环境进入视野。
- 02→03：鼠标离开时歌词岛透明度恢复，恢复动作直接延续为第一次宽度 Morph。
- 03→04：最后一次展开停在播放器切换所需宽度，内容槽开始切歌；不重置产品位置。
- 04→05：内容槽的歌曲名成为歌词高亮的视觉入口，相机只推进歌词区域。
- 05→06：最后一个高亮词触发橙色节点向下，镜头跟随节点移动到任务栏；产品同步压扁下移。
- 06→07：歌词坞吸附完成后，世界背景退去，橙线完成最后弧线并引出品牌终帧。

全片的主要转场是连续空间移动和产品 Morph。辅助转场只使用 mask reveal 与温和 focus shift，不采用 glitch、shader 炫技或硬切。

## 9. 9:16 适配策略

- 保留 20 秒节奏、Scene 边界和同一产品对象契约。
- 为世界锚点和 Morph 状态增加 portrait 数值表，不缩放整张 16:9 画面。
- 标题由横向宽幅改为两行窄列，产品宽度限制在 900px 内。
- 路径改为纵向 S 曲线，但节点顺序与时间不变。
- Scene 06 的任务栏改为竖屏底部安全区，不改变“顶部岛进入底部任务栏”的叙事。

## 10. 进入第二阶段前的确认点

1. 接受“单根构图 + 逻辑 Scene”而不是七个独立子构图。
2. 接受“同一歌词岛 wrapper + 内容槽更新”作为连续 Morph 的实现模型。
3. 接受从原海报曲线提炼一条跨 13440px 世界空间的连续 SVG 路径。
4. 确认阶段二可从 `island-layout-states.png` 裁出状态片段，但不重新绘制 UI。
5. 如需音乐节拍同步，请在阶段二开始前提供音乐，或授权先使用无版权占位节拍并在最终渲染前替换。

## 11. 已关闭的结构风险

- 字体确定性：已从 PPT 的 `fsType=0x0000` EOT 容器提取三套品牌 TTF/OTF；完整文件保留用于追溯，运行时通过覆盖全片文案字形的 WOFF2 子集和本地 `@font-face` 加载。
- 2 MB 媒体内联限制：`desktop-blue.png` 已做无损 PNG 重压缩，从 2,106,142 字节降至 1,945,832 字节，逐像素差异为零。

## 12. 第一阶段验证

- `npm run audit:phase1`：逐项断言画布、帧率、七幕连续时间、核心文案、唯一持久对象、Morph 状态、素材清单、转场说明和“未提前生成 MP4”。
- `npm run check`：执行 HyperFrames 的 Lint、Runtime、Layout、Motion 与 Contrast 门禁。
- `artifacts/phase1/`：保存 0s、10s、19s 三个确定性 seek 帧和 Studio 外壳截图；浏览器实测根构图为 1920×1080，主时间轴已注册且直接构图页无运行时错误。
- Studio 当前后台进程的缩略图接口不能启动 Chrome，只影响左侧缩略图；直接预览、官方检查和取样帧不受影响，详见 `artifacts/phase1/README.md`。

## 13. 后续导出门槛

- HyperFrames `doctor` 0.8.63 因 PATH 不可见而报告缺少 FFmpeg/FFprobe；只读侦察已确认 WinGet 中实际安装了 Gyan.FFmpeg 8.1.1 full build，并包含 `psnr`、`libx264` 与 `h264_nvenc`。
- `scripts/render-final.ps1` 动态解析现有 WinGet 安装，通过 HyperFrames 官方的 `HYPERFRAMES_FFMPEG_PATH`、`HYPERFRAMES_FFPROBE_PATH` 环境变量传入编码器，不修改系统 PATH。
- `npm run render:preflight` 只验证路径和能力；`npm run render:final` 固定输出 1920×1080、60fps、高质量 MP4，并启用 `--strict-all`。GPU 编码只在显式传入 `-Gpu` 时启用。
- 本阶段只运行预检，不生成草稿或最终视频；完整动画确认后才允许执行正式渲染。
