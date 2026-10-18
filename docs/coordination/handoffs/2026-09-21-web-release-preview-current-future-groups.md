# 任务交接：版本预告仅区分本版本与稍后推出

- 日期：2026-09-21
- 集成基线：`cd4e413c806655047690d8978d04955cbdeda2c6`
- 范围：版本预告公开分组、后台分组选项、样式、浏览器断言与 API 文档

## 结果

- 公开页不再区分“核心功能”和“其他改进”；所有非 `future` 功能按原 `sort_order` 合并显示为“新功能与改进”。
- `future` 继续按 `target_version` 独立显示为“接下来 · Vx.x”，不显示进度圆环。
- 后台“默认展示分组”和单项“展示位置”只显示“本版本 / 稍后推出”。历史 `improvement` 值在控件中视作“本版本”。
- API 与 schema 继续接受和原样保存 `improvement`，不批量改写数据库；新建本版本功能使用 `featured`，保证旧数据和旧客户端兼容。

## 修改文件

- `website/components/ReleasePreviewArticle.tsx`
- `website/components/AdminIncentives.tsx`
- `website/app/globals.css`
- `website/tests/smoke.py`
- `docs/api/WEBSITE_API.md`

## 本地验证

- `npm run typecheck`：通过。
- `npm run test:esa-api`：通过。
- `npm run build:esa`：通过，生成 17 个静态页面且 API 源码恢复。
- `python -m py_compile tests/smoke.py`：通过。
- Python 环境未安装 Playwright，因此完整 smoke 浏览器流程未执行；发布后使用本机 Chrome CDP 对生产页面做桌面和移动端核验。

## 发布边界

- 不修改数据库、不写入生产预告数据。
- 不修改版本预告之外的页面或后台模块。
- 主工作目录中的桌面提交、宣传图和其他 WIP 不进入发布提交。
