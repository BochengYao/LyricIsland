# 线程总目录

| 章程 | 线程 | 主要范围 | 何时启用 | 交接给 |
| --- | --- | --- | --- | --- |
| `00-project-coordination.md` | 项目统筹与架构 | 边界、决策、共享文档、任务分流 | 跨模块或新任务 | 对应模块线程 |
| `01-web-frontend.md` | 官网前台 | 公开页面、国际化、SEO、视觉 | 官网公开体验变更 | 测试/发布 |
| `02-web-admin-data.md` | 官网后台与数据 | 管理页、API、Supabase、鉴权 | 内容与数据流程变更 | 测试/发布 |
| `03-desktop-lyrics-player-core.md` | 歌词与播放器核心 | `LyricHover.Core`、媒体会话、缓存 | 歌词或播放器问题 | 测试/桌面 UI |
| `04-desktop-island-settings-ui.md` | 岛屿、设置与交互 | `LyricHover.App`、布局、教程 | 桌面视觉或交互变更 | 测试/发布 |
| `05-quality-integration.md` | 测试与集成 | 回归基线、跨模块验收 | 高风险变更或发版 | 原功能线程/发布 |
| `06-release-github-store.md` | 发布与平台 | 版本、MSIX、GitHub、Store | 发布、文档同步、平台动作 | 项目统筹 |
| `07-visual-assets-brand.md` | 视觉资产与品牌 | 宣传图、图标、3D 徽章、品牌素材 | 资产或品牌素材变更 | 前台/桌面 UI |
| `08-legal-evidence.md` | 法务、软著与证据 | 软著、专利、证据保全 | 申报或证据任务 | 项目统筹/发布 |

线程是职责入口，不是长期占用的聊天或分支。一个模块可在不同时间创建多个短期任务线程；一个任务若涉及多个模块，由项目统筹拆分并定义交接顺序。
