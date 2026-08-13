# 任务交接：新功能内容独立版本 CRUD

- 日期：2026-08-13
- 任务线程：官网管理后台、API、鉴权和数据边界（`02-web-admin-data`）
- 基线提交：`87a5406d148a8b21be58e05ebe5f64426364aaf7`
- 结果提交：本交接所在的单一独立提交
- 允许修改范围：新功能后台组件、FeatureContent 类型/清理与存储、ESA API 镜像、接口文档、API 测试和交接记录；未修改根树 WIP、桌面、公开页面视觉或 ESA 发布环境。

## 数据结构与兼容

- `FeatureContent` 新增 `versions: string[]`，只保存新功能内容自身的规范完整版本号（`vX.Y.Z`）；允许存在没有 sections 的空版本。
- `sections[].release_version` 仍是每条功能的唯一持久化版本来源，`major_version` 仍由服务端派生。
- 历史 JSON 没有 `versions` 时，清理逻辑从已有 sections 恢复规范版本；无版本历史 sections 继续归入 `早期更新`，不会编造或迁移 9 条历史内容。
- `早期更新` 不是 `versions` 元数据项，只作为兼容组可浏览、编辑、保存，禁止新增、重命名和删除。

## 管理 API 与行为

- `PUT /api/incentives/admin/features` 继续接受完整 `content` 保存；版本条目必须属于 `content.versions`，历史 `早期更新` 例外。
- 同一 PUT 支持版本原子操作：
  - `{ type: "create", release_version }`：新建空版本。
  - `{ type: "rename", from, to }`：服务端一次写入版本元数据并级联所有 child sections。
  - `{ type: "delete", release_version, delete_sections? }`：空版本可删除；非空版本没有 `delete_sections: true` 时拒绝，显式确认后才级联删除版本及其条目。
- 后台候选仅来自 `content.versions` 和 `早期更新`，完全不依赖预告数据；版本预告继续独立管理未来预告 CRUD/status。

## 已完成

- 版本父级卡片提供“新建版本”、当前版本重命名和删除操作。
- 当前版本组只显示该组条目；黄色序号从 `01` 开始，排序不跨版本。
- 新建版本后可立即新增条目；历史兼容组不能新增。
- API 文档已删除预告作为新功能版本来源的旧约束，并记录元数据恢复和回滚策略。

## 验证

- `cd website && npm run typecheck`：通过。
- `cd website && npm run test:esa-api`：通过；覆盖旧数据恢复、独立空版本创建/删除、原子重命名、非空删除保护与显式级联删除、多版本派生。
- `cd website && npm run build:esa`：通过，生成四语静态路由。
- `git diff --check`：通过。

## 给质量线程的重点复核

- 创建空版本后刷新后台仍可选择并新增条目；版本预告增删不会影响新功能候选。
- 重命名后所有原 child sections 一次性进入新版本，不能出现部分级联。
- 非空删除默认被 API 拒绝，确认级联后条目与版本一并消失；空版本可直接删除。
- `早期更新` 可保存四语内容但不可新增、重命名、删除；公开 `/api/features` 继续返回 sections 的 `release_version` / `major_version`。

## 风险与回滚

- 无数据库列迁移；版本元数据与既有功能 JSON 一起存储在现有 feature-content 行的 `highlights_zh` 中。
- 未触发 ESA；发布线程需另行部署并核验实际后台权限。
- 回滚点：`87a5406d148a8b21be58e05ebe5f64426364aaf7`；回滚不删除既有 JSON 数据。
