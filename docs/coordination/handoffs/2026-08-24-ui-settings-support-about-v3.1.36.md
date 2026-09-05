# UI 线程交接：v3.1.36 支持页与关于页

- 日期：2026-08-24
- 主线提交：`4decc8e`（`fix(app): refine support and about pages`）
- 已发布本地包：`publish/current`，`v3.1.36 Beta`
- 写入主文件：`LyricHover.App/PlacementSettingsWindow.xaml`

## 本次完成内容

### 支持开发者页

- 将原先横向四列、信息容易被压缩的行动卡片改为 2×2 系统设置式分组行。
- 每个项目使用一致的 72px 行高、22px 图标、14.5px 标题、12.5px 说明和单一系统蓝链接；保留“评价、分享、GitHub、反馈”的原点击处理器。
- Pro 区域四边内边距统一为 20px，收紧标题、权益和按钮的层级，去除表情与重复营销文字。
- 当前视觉原则：分组列表、低对比度分隔线、克制留白、单一蓝色交互强调；不应重新引入横向挤压的营销卡片。

### 关于页

- Logo 统一为 48px，并移除厚重投影。
- 建立稳定字号阶梯：产品名 22px、副标题 14.5px、元数据/正文 12.5px；官网、GitHub、教学模式操作行统一为 60px。
- 用简洁的产品说明替换过期的“v2.0 更新内容”清单，保留版本、官网、GitHub 和教学入口。
- 页面内外保持既有全局圆角与卡片安全距离规则；不要以单独页面的局部圆角覆盖全局资源。

## 关键回归点

- `LyricHover.Tests/Program.cs` 已覆盖支持页的 2×2 行列结构、4 个点击处理器、文本层级和 Pro 区域；也覆盖关于页的 48px Logo、无阴影、60px 操作行与新说明文案。
- 不要删除 `SupportStoreReviewButton_Click`、`SupportShareButton_Click`、`OpenGitHubAboutRow_Click`、`SupportFeedbackButton_Click`，也不要改变官网/GitHub/教学行行为。
- 150% DPI 实图已检查：
  - `C:\Users\14731\AppData\Local\Temp\settings-support-apple-after-150.png`
  - `C:\Users\14731\AppData\Local\Temp\settings-about-apple-after-150.png`

## 验证命令

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
$env:NUGET_PACKAGES='C:\Users\14731\.nuget\packages'
dotnet build LyricHover.sln -c Release --no-restore
dotnet run --project LyricHover.Tests -c Release --no-build
```

构建会报告测试工程既有的 `CS0436` 类型冲突警告；本次验证为 157 条警告、0 错误，测试完整通过。
