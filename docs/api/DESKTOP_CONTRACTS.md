# 桌面端核心契约

本页描述 `LyricHover.Core`、`LyricHover.App`、设置持久化和播放器连接的稳定边界。涉及这些边界的改动必须与调用方和测试一起审查。

## 分层

- `LyricHover.Core/`：歌词解析、时间线、缓存、播放器相关的领域逻辑；不得依赖桌面页面或 XAML。
- `LyricHover.App/`：WPF 岛屿、设置、引导和用户交互；通过 Core 契约消费数据，不复制核心逻辑。
- `LyricHover.Tests/`：可执行的回归约束；测试失败应先区分新回归、既有基线和环境问题。

## 兼容性边界

- 产品当前数据目录为 `LyricHover`，并保留从 `LyricsIsland` 与更早 `AppleMusicDesktopLyrics` 的迁移路径。不得删除或静默改写这条迁移兼容层。
- 商店包身份 `70643607.LyricIsland`、应用标识 `LyricsIsland` 和单实例名称 `LyricsIsland.DesktopLyrics.SingleInstance` 是发布兼容标识；重命名展示品牌时不得随意更改。
- 用户设置需有默认值、升级行为和缺失值处理。修改设置键或语义时必须说明迁移、回滚与手动验收步骤。

## 播放器与歌词

播放器连接、曲目身份、歌词时间线和缓存属于 Core 行为。UI 可以决定展示与交互，但不得在视图层引入另一套缓存或解析规则。任何歌词定位、空标记、抖动或缓存策略改动都必须附回归案例。

## 验收

```powershell
dotnet build LyricHover.sln -c Release
dotnet run --project LyricHover.Tests -c Release --no-build
```

若 SDK 环境需要变量，使用 [回归基线](../testing/REGRESSION_BASELINE.md) 中的命令。参见 [核心章程](../coordination/charters/03-desktop-lyrics-player-core.md) 和 [岛屿与设置章程](../coordination/charters/04-desktop-island-settings-ui.md)。
