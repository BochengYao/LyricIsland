# 共享文档注册表

| 文档 | 用途 | 维护权限 | 更新时机 |
| --- | --- | --- | --- |
| `coordination/00-04` | 线程治理、目录、边界与决策 | 项目统筹 | 范围或决策变化 |
| `coordination/05-V3-ROADMAP.md` | V3 版本基线、能力范围与阶段门禁 | 项目统筹 | V3 能力状态或发布口径变化 |
| `coordination/charters/*` | 单线程只读章程 | 项目统筹 | 模块职责变化 |
| `coordination/handoffs/*` | 单任务交接记录 | 任务线程 | 每次任务完成或移交 |
| `CHANGELOG.md` | 用户可见已发布变更 | 发布线程 | 实际发布验证后 |
| `api/WEBSITE_API.md` | 官网接口与鉴权边界 | 官网后台与数据 | API 契约变化 |
| `api/DESKTOP_CONTRACTS.md` | Desktop Core/App 兼容边界 | 核心与 UI 协作 | 持久化或接口变化 |
| `release/RELEASE_CHECKLIST.md` | 发布门禁 | 发布线程 | 流程或平台变化 |
| `testing/REGRESSION_BASELINE.md` | 命令、基线与已知失败 | 测试与集成 | 验证基线变化 |

共享文档不是聊天记录。所有动态工作状态写入独立交接文件，避免多线程修改同一页。
