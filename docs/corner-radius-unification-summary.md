# 偏好设置页圆角统一改造总结

> 日期：2026-08-23
> 范围：LyricHover 偏好设置页（`PlacementSettingsWindow.xaml`）及相关跨窗口元素
> 目标：参考 iOS / macOS Settings 视觉语言，将整页圆角收敛为统一 Design Token，消除混用

---

## 一、任务目标

- 建立全局 CornerRadius Design Token，禁止 XAML 中散落硬编码圆角
- 按层级统一：大卡片 18 / 选择卡片 14 / 控件 10 / 胶囊 999
- 清理全项目 `CornerRadius=` 硬编码，逐个核查归类
- Hover / Selected / Focus 状态不得改变圆角
- 深色 / 浅色主题共用同一套圆角体系

## 二、代码改动

### 1. 新增 Design Token（`LyricHover.App/PlacementSettingsWindow.xaml` 资源顶部）

| Token | 值 | 适用层级 |
|---|---|---|
| `RadiusLarge` | 18 | 窗口底衬、侧边导航卡、页面大卡片（通用/缓存/模块布局/歌词外观等） |
| `RadiusMedium` | 14 | 水平积木/自动折叠选择卡片、分段控件容器、实时预览场景 |
| `RadiusSmall` | 10 | 按钮、输入框、下拉框及弹层、分段按钮、Slider 滑块、内嵌小卡片 |
| `RadiusPill` | 999 | 完成按钮、ToggleSwitch 轨道与旋钮、侧边导航项、胶囊形预览条 |

### 2. 关键替换（约 40 处硬编码收敛）

| 元素 | 原值 | 新值 |
|---|---|---|
| `CardStyle`（卡片基础样式） | 14 | `RadiusLarge` |
| `SettingsGlassCardStyle`（页面大卡片） | 12 | `RadiusLarge` |
| Sidebar 导航卡 | 20 | `RadiusLarge`（不再比右侧卡片更圆） |
| 窗口底衬 `RootChrome` | 8 | `RadiusLarge` |
| 完成按钮 `SettingsDoneButtonStyle` | 17 | `RadiusPill`（iOS 式蓝色胶囊） |
| 侧边导航项 `SidebarButtonStyle` | 16 | `RadiusPill` |
| ToggleSwitch 轨道 / 旋钮 | 12 / 10 | `RadiusPill` / `RadiusPill` |
| 下拉框主体 / 弹层 / 列表项 | 10 / 10 / 7 | `RadiusSmall` |
| 输入框 / 各按钮样式 / 复选框 | 10 / 10 / 6 | `RadiusSmall` |
| 水平积木、自动折叠选择卡片 | 12 | `RadiusMedium` |
| 分段控件容器（行数/对齐/主题切换） | 10 / 12 | `RadiusMedium` |
| 分段按钮 / 分段滑块 | 8 | `RadiusSmall` |
| Slider 拇指 | 10 | `RadiusPill`（圆形） |
| 支持页容器、关于页 Logo、徽章署名输入框 | 9 / 10 / 10 | `RadiusSmall` |
| 教学高亮框 | 16 | `RadiusLarge` |

### 3. 跨窗口处理

- `LayoutEditing/ModuleToolboxCard.xaml`：9 → `DynamicResource RadiusSmall`（在设置窗口内通过元素树解析资源）
- `LayoutEditing/ModuleDragGhostWindow.cs`：拖拽幽灵窗口独立复制资源，补充注册 `RadiusSmall`，保证卡片圆角正常解析

### 4. 有意保留的元素（逐个核查后决定不动）

- 布局选择卡内的**微型插画缩略图**（2–7px 圆角）：歌词岛的按比例缩小示意图，套用真实尺寸会破坏插画
- 滚动条拇指、Slider 轨道条（5–6px 高、半径 3）：本身已是视觉胶囊，半径大于高度会变形
- 预览中的歌词岛模型（18/20）：模拟真实岛的胶囊形态
- `ContentCard` 的 `CornerRadius="0"`：透明内容容器，结构性保留

## 三、验证过程

1. **编译验证**：`dotnet build -c Release` 0 警告 0 错误
2. **实机视觉验证**（深色 + 浅色双主题截图核对）：
   - 通用页、模块布局页、歌词外观页、下拉框展开态
   - 确认：侧边导航、页面卡片、选择卡片、输入框、下拉框、Toggle、完成按钮圆角全部一致
   - 确认：Hover/Selected 触发器只改背景与描边，不改圆角
   - 确认：深浅色主题共用同一套资源，表现一致

### 验证期间的环境处理

- 旧版发布实例（PID 133128）占用单实例互斥锁且沙箱禁止杀进程，遂在 `App.xaml.cs` 临时给互斥名加环境变量后缀使新构建并行运行，验证完成后**已完整还原**并重新编译确认
- 通过托盘菜单「退出」优雅关闭验证实例；退出前将测试中切换的浅色主题恢复为「跟随系统」，未改变用户设置
- 验证产生的临时脚本与截图已全部清理

## 四、验收清单

| 检查项 | 结果 |
|---|---|
| 左侧导航圆角一致 | ✅ |
| 页面 Card 圆角一致 | ✅ |
| 布局选择卡片一致 | ✅ |
| 输入框一致 | ✅ |
| 下拉框一致 | ✅ |
| Toggle 胶囊一致 | ✅ |
| 按钮一致（完成按钮为胶囊） | ✅ |
| Hover 不改变圆角 | ✅ |
| 深色/浅色主题一致 | ✅ |

## 五、遗留提醒

- 系统中存在一个前一日残留的 LyricHover 僵尸进程（当时 PID 38596，无窗口、托盘图标无响应），沙箱内无法结束，需用户重启时留意
- 后续新增设置页 XAML 时，禁止硬编码圆角数值，必须引用上述 Token（该规范已写入长期记忆）

## 六、涉及文件

- `LyricHover.App/PlacementSettingsWindow.xaml`（主要改动）
- `LyricHover.App/LayoutEditing/ModuleToolboxCard.xaml`
- `LyricHover.App/LayoutEditing/ModuleDragGhostWindow.cs`
- `LyricHover.App/App.xaml.cs`（仅验证期临时改动，已还原）
