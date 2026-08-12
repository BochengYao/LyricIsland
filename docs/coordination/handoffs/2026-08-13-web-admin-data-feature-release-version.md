# 任务交接：后台新功能完整发布版本与 API 契约

- 日期：2026-08-13
- 任务线程：官网管理后台、API、鉴权和数据边界（`02-web-admin-data`）
- 基线提交：`56e6f873327d39972773353dee69ded6b3aa5301`
- 结果提交：当前工作树实现（待项目统筹安排提交/合并）
- 允许修改范围：官网数据模型、管理员表单与接口、公开 API、ESA 镜像、API 测试和 `docs/api/WEBSITE_API.md`；未修改公开页面视觉。

## 已完成

- `FeatureContentSection` 新增 `release_version`，后台新建条目从空值开始，保存前必须填写完整版本号（规范 `vX.Y.Z`，例如 `v2.1.8`）。
- `/api/features` 与管理员 GET/PUT 的内容清洗会返回 `release_version`，并从该字段派生只读 `major_version`（`V2`、`V3` 等）；没有第二个可编辑的大版本来源。
- 历史无版本条目读取兼容为 `release_version: "早期更新"`、`major_version: "OTHER"`，不会因新增字段导致旧数据无法读取或保存。
- 管理员 PUT 在原始请求缺少/清空版本字段时返回 400；非法非完整版本值也会被拒绝。四语言标题、描述与翻译流程保持原边界。
- ESA `website/esa/api.js` 与源实现同步，未改数据库列；版本信息继续存放在现有功能内容 JSON 中。
- `WEBSITE_API.md` 已补充字段来源、派生规则、兼容回退及前台调用契约。

## 给官网前台线程的调用契约

- 从 `GET /api/features` 读取 `content.sections[]`，按每条的 `release_version` 显示完整版本号。
- 用同一条记录返回的 `major_version` 做 V2/V3 分组；不要自行从标题、排序或另一个字段推断，也不要把 `major_version` 写回管理数据。
- `release_version === "早期更新"`、`major_version === "OTHER"` 是历史兼容记录的合法回退；展示时不得因该值过滤整条内容。
- 继续使用 `title_*`、`body_*`、`items_*` 的四语字段与既有缺失语言回退；本交接不包含公开页面布局或视觉改动。

## 验证

- `cd website && npm run typecheck`：通过。
- `cd website && npm run build:esa`：通过。
- `cd website && npm run test:esa-api`：通过；覆盖旧记录回退、缺版本 PUT 返回 400、`v2.1.8` 输出及 `V2` 派生。

## 风险与后续

- 当前实现尚未部署 ESA 生产环境；发布线程需在合并后按 `RELEASE_CHECKLIST.md` 验证实际生产 `/api/features` 与管理员 PUT。
- 不需要新增 Supabase 列或提交密钥、真实用户数据、环境变量。回滚应回到基线提交或保留旧 JSON 记录，不删除历史内容。
