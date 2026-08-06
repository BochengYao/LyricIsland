# 桌面端歌词与播放器核心章程

## 使命

维护歌词解析、时间线、播放器连接、缓存与 Core 层稳定性。

## 允许范围

- `LyricHover.Core/` 中的领域逻辑、缓存、解析和平台无关服务。
- 与核心契约直接相关的测试及文档更新请求。

## 禁止范围

- 不直接改变 XAML 视觉、岛屿布局或设置页交互。
- 不破坏既有用户数据迁移、单实例或商店身份兼容性。

## 必读文档

阅读 [桌面契约](../../api/DESKTOP_CONTRACTS.md)、[既定决策](../04-DECISIONS.md) 和 [回归基线](../../testing/REGRESSION_BASELINE.md)。

## 验收与交接

运行 `dotnet build LyricHover.sln -c Release` 和桌面测试命令；交接中说明歌词/缓存兼容性、已知失败是否变化以及需要 UI 线程验证的行为。
