# 官网后台接口与数据边界

本页是官网接口的协作契约，不替代路由实现。变更任一接口时，官网后台与数据线程须在同一提交更新本页、调用方和测试。

## 边界

- 路由位于 `website/app/api/`；公开展示与管理后台页面不得直接绕过其授权和数据边界。
- 密钥、管理员口令、会话值、第三方服务令牌和真实用户数据不得进入仓库、截图或交接记录。
- 数据结构以实现和迁移为准；新增字段必须声明兼容策略、默认值和回滚方式。

## 当前路由面

| 路由 | 方法 | 用途 | 权限边界 |
| --- | --- | --- | --- |
| `/api/access` | `POST` | 访问/登记类提交 | 公开输入，服务端校验 |
| `/api/features` | `GET` | 功能展示数据 | 公开只读 |
| `/api/incentives/public` | `GET` | 激励活动公开数据 | 公开只读 |
| `/api/incentives/submissions` | `POST` | 激励活动提交 | 公开输入，服务端校验 |
| `/api/incentives/likes` | `POST` | 点赞交互 | 公开输入，服务端限权 |
| `/api/incentives/admin/login` | `POST` | 建立管理员会话 | 管理员认证 |
| `/api/incentives/admin/logout` | `POST` | 结束管理员会话 | 已认证会话 |
| `/api/incentives/admin/access-logs` | `GET`、`PATCH` | 管理访问记录 | 管理员 |
| `/api/incentives/admin/features` | `GET`、`PUT` | 管理功能数据 | 管理员 |
| `/api/incentives/admin/previews` | `GET`、`POST`、`PATCH` | 管理预览内容 | 管理员 |
| `/api/incentives/admin/submissions` | `GET`、`PATCH`、`DELETE` | 审核提交 | 管理员 |
| `/api/incentives/admin/translate` | `POST` | 管理端翻译辅助 | 管理员，依赖服务端配置 |

## 本地化内容字段

`/api/features` 的 `content` 与 `/api/incentives/public` 的 `previews` 均返回简中、英文、繁中和日文内容。繁中字段以 `_zh_tw` 结尾，日文字段以 `_ja` 结尾；新功能内容使用 `label_*`、`title_*`、`body_*`、`items_*`，功能预告使用 `title_*`、`body_*`、`highlights_*`。管理端上传表单可编辑全部四种语言字段。每个公开预告还返回 `major_version`（如 `V2`、`V3`），供前台按大版本分组。

为兼容既有数据库记录，繁中缺失时服务端回退简中；日文缺失时依次回退英文、简中。管理端 `POST`/`PATCH /api/incentives/admin/previews` 可选接收 `body_zh_tw`、`body_ja`；未传字段不会在更新时被清空。`POST /api/incentives/admin/translate` 支持同一次请求指定 `en`、`zh-tw`、`ja` 目标语言，并按目标语言键分别返回翻译结果。公开预告接口支持游标分页：`preview_limit` 可选（默认 20，最大 50），`preview_cursor` 使用上一页返回的 `next_preview_cursor`；响应始终返回 `next_preview_cursor`（无下一页时为 `null`）。分页只作用于预告，建议数据保持原有返回方式。公开页面必须继续经上述接口读取，且由官网前台线程负责将 `zh-TW`、`ja` 路由映射到对应字段并在分页结果中按 `major_version` 分组。

## 鉴权与变更规则

管理端接口必须在每个请求路径验证管理员会话；登录、退出和会话 cookie 的具体实现以相邻路由和服务端工具函数为准。公开接口不等于无限制接口：应验证输入、限制写入权限，并避免泄露内部字段。

若接口有破坏性变更，先保留兼容读法或明确迁移窗口；更新调用者、`npm run test:esa-api` 覆盖和任务交接，再由发布线程验证真实生产路由。

## 验收

在 `website/` 执行：

```powershell
npm run typecheck
npm run build:esa
npm run test:esa-api
```

详见 [官网后台与数据章程](../coordination/charters/02-web-admin-data.md) 与 [发布清单](../release/RELEASE_CHECKLIST.md)。
