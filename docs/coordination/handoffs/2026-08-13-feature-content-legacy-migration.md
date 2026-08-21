# 任务交接：新功能历史“早期更新”受控迁移

- 日期：2026-08-13
- 任务线程：官网管理后台 / API / 数据边界
- 基线提交：7fcb5da6cc2d8b635c5e44b61c62d94bf3431607
- 结果提交：以最终回复提供的单一提交 SHA 为准
- 允许修改范围：`website/app/api/incentives/admin/features`、`website/components/AdminIncentives.tsx`、`website/data/incentives-types.ts`、`website/lib/incentive-store.ts`、`website/esa/api.js`、ESA API 测试、`docs/api/WEBSITE_API.md` 与本交接记录

## 已完成

- 新增 `FeatureContentVersionOperation` 的 `migrate-legacy` 操作：目标版本必须是完整 semver，校验大小写不敏感但保留管理员输入值（例如 `V2.0.36`）。
- 管理端在“早期更新（历史未标版本）”组显示条目数量、目标版本输入（默认 `V2.0.36`）和明确确认的“迁移历史条目”按钮；迁移不是页面加载或公开读取时的自动写入。
- 服务端在已认证管理员 `PUT /api/incentives/admin/features` 会话中一次性处理所有 `release_version="早期更新"` 条目，并通过单次 `saveFeatureContent` 写回：目标版本已存在时复用并合并 `content.versions`，所有迁移条目由服务端派生 `major_version`（`V2`），无残留条目时兼容组不再出现。
- 没有历史条目时拒绝重复迁移；普通版本创建、重命名、删除路径保持不变；版本预告数据不参与候选或迁移。
- 更新 `WEBSITE_API.md`、ESA 镜像实现与错误映射，补充 9 条历史条目、目标已存在合并、大小写保留、未认证拒绝及重复迁移保护测试。

## 未修改 / 非目标

- 未执行生产数据迁移；上线后需管理员在后台明确输入并确认 `V2.0.36` 后再提交。
- 无数据库 schema 迁移；继续使用现有隐藏 `release_previews` JSON 中的 `content.versions` 与 sections `release_version`。
- 未修改公开页面视觉、公开 `/api/features` 契约、版本预告 CRUD、桌面端、资产或 ESA 发布环境；未触发 ESA。

## 验证

- 命令：`npm run typecheck`
- 结果：通过。
- 命令：`npm run test:esa-api`
- 结果：通过（包含历史 9 条目原子迁移与认证边界）。
- 命令：`npm run build:esa`
- 结果：通过。

## 风险与后续

- 已知限制：迁移动作会把当前兼容组的全部历史条目迁移到同一个目标版本；若只需迁移部分条目，应先由管理员在历史组编辑/删除并确认数量，再执行整体迁移。
- 交接目标：`05-quality-integration`；复核后台确认提示、目标已存在合并、V 大写保留、无残留“早期更新”、未认证拒绝及公开 API 的 `release_version`/`major_version` 兼容。
- 回滚点：回滚本提交即可恢复原有“早期更新”兼容行为；已经执行的生产迁移不会由代码回滚自动逆转，需管理员通过普通版本编辑/受控数据恢复处理。
