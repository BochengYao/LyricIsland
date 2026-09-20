# 任务交接：版本预告 Note、结构化功能项与圆环进度

- 日期：2026-09-16
- 任务线程：官网前台 + 官网后台与数据
- 基线：当前共享工作树；已有桌面端与视觉资产 WIP 均未纳入本任务
- 允许修改范围：版本预告公开组件、维护者后台、预告 API/存储兼容层、ESA 运行时镜像、官网测试、接口文档与本交接

## 实现边界

- 沿用 `release_previews` 表与现有 `target_date`、四语字段，不新增 Supabase 列，不执行真实数据写入或不可逆 migration。
- Note 使用既有 `body_*`；结构化 Features 写入 `highlights_zh` JSONB 的兼容信封数组：首项为 schema v2 元数据对象，后续项为中文功能文案。公开响应保留旧 `body_*` / `highlights_*` 兼容字段。
- 旧 `body + highlights` 安全适配为 Note + Features；未知 progress 保持 `null`。无法判断 Note 的旧长文本不自动挪动，只有需求指定的 V3.2 中文首条总说明采用精确匹配迁移。
- 后台保留批量中文粘贴解析，并提供稳定 ID、四语逐项内容、排序、删除、新增、range + number 进度输入；不引入拖拽库。
- 翻译使用 `note` / `feature.<id>` 键并严格校验完整键集合；提供“翻译全部”和“仅翻译缺失内容”。
- 公开页保持原双栏与留白，左侧增加 Note，右侧编号替换为 16px SVG 圆环；不显示百分比、状态标签或横向进度条。`null` 使用弱化空心圆并提供辅助技术文本。

## 数据回滚

- 代码回滚后，旧运行时会忽略信封首项对象并继续显示后续中文字符串；现有历史记录未被批量重写或删除。
- 若回滚后再用旧后台编辑这些记录，旧代码不会保留结构化进度元数据；正式回滚前如已用新后台保存记录，应先导出对应行以便恢复。
- 新 API 在混合部署期间会拒绝旧客户端对 schema v2 记录的正文 PATCH（`409`，状态切换仍可用），避免静默降级回写。
- 本任务不连接真实 Supabase，不执行生产迁移、发布或删除。

## 验证

- `node node_modules/typescript/bin/tsc --noEmit`：通过
- `node scripts/test-esa-api.mjs`：通过，覆盖结构化新增、刷新读取、排序、`100/85/60/30/null`、非法输入、旧数据与旧客户端降级写入保护
- `node scripts/build-esa-static.mjs`：完整通过一次，17 个静态页面生成完成；最后补充保留 ID 前缀校验后，`tsc`、ESA API 测试与语法检查再次通过。再次重复构建时 Windows 返回“内存资源不足”，不是代码编译错误。
- `node --check esa/api.js`、`python -m py_compile tests/smoke.py`、`git diff --check`：通过

本机 Python 环境未安装 Playwright，Codex 内置浏览器运行时又存在版本缺失，因此本轮无法执行 `tests/smoke.py` 的真实 Chrome 视觉流程；没有把静态构建冒充为 Desktop/Mobile 视觉验收。生产 ESA、真实管理员会话、真实 AI 服务和四语页面的最终人工验收仍由质量/发布流程完成。
