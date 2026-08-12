# 任务交接：版本预告主版本分组与游标分页 API

- 日期：2026-08-12
- 任务线程：官网后台与数据
- 基线提交：`d760bf8b4c4109d2e41c5a6e87cf2633108bd235`
- 结果提交：未提交（共享工作树；保留其他线程已有改动）
- 允许修改范围：`website/app/api/incentives/public/route.ts`、`website/lib/incentive-store.ts`、`website/esa/api.js`、`website/data/incentives-types.ts`、`website/scripts/test-esa-api.mjs`、`docs/api/WEBSITE_API.md`

## 已完成

- `/api/incentives/public` 的每个已发布预告新增 `major_version`：`v2.1`、`V2 Beta` 归为 `V2`，`v3.0` 归为 `V3`；无法识别数字主版本的记录归为 `OTHER`。
- 公开预告读取改为游标分页：`preview_limit` 默认 20、最大 50；首屏和后续页均返回 `next_preview_cursor`，无更多数据时为 `null`。
- 游标使用 `published_at` 与 `id` 的稳定排序键，按 `published_at.desc,id.desc` 继续读取，不依赖可变页码；非法游标返回 HTTP 400。
- 保留兼容字段 `previews`；未配置或首屏没有数据库预告时仍返回原有 fallback，带游标的空页返回空数组。
- 同步更新 ESA 运行时镜像、类型定义、接口文档与 API 测试。

## 未修改 / 非目标

- 未修改公开页面组件、视觉样式、路由或语言文案；前台版本选择器仍由官网前台线程负责。
- 未修改数据库结构、管理员鉴权、Microsoft Store 集成或任何密钥配置。
- 未写入密钥、令牌、真实用户数据或环境变量。

## 前台接入契约

1. 请求 `/api/incentives/public?preview_limit=50`，拿到 `previews` 与 `next_preview_cursor`。
2. 只要 `next_preview_cursor` 非 `null`，继续请求 `/api/incentives/public?preview_limit=50&preview_cursor=<encodeURIComponent(cursor)>`，直到为空；不要只读取首屏，否则同一 `V2` 可能跨页而不完整。
3. 将所有页的 `previews` 合并后按 `major_version` 分组；组内按现有版本排序。分组标签直接使用接口返回的 `V2`、`V3` 等值。
4. 保留四语字段选择：简中 `_zh`、英文 `_en`、繁中 `_zh_tw`、日文 `_ja`；服务端已提供兼容回退。

## 验证

- 命令：`git diff --check`
- 结果：通过。
- 命令：`npm run test:esa-api`
- 结果：通过；覆盖 `V3`/`V2` 主版本标记、两页游标读取、末页 `null` 游标和非法游标 400。
- 命令：`npm run typecheck`
- 结果：通过。
- 命令：`npm run build:esa`
- 结果：通过，ESA 静态构建完成。

## 风险与后续

- 已知限制：本地测试使用模拟 Supabase；尚未以真实生产数据验证跨页排序和真实前台下拉交互。
- 交接目标：官网前台线程（交接 `2026-08-12-web-frontend-updates-version-selector.md`）接入分页并按 `major_version` 分组；发布线程再验证生产 API 与四个语言路由。
- 回滚点：回退本交接列出的服务端/接口文档/测试改动即可恢复固定 20 条读取；数据库数据不会被删除。
