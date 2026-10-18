# 任务交接：版本预告按版本分层、功能完全平级

- 日期：2026-09-21
- 集成基线：`61aaaabd0eccadc355ae08a65bf6f6dceee364bd`
- 范围：版本预告公开展示、后台版本编辑、批量导入、兼容映射、样式、测试与 API 文档

## 结果

- 公开页以父级预告版本和 feature 的 `target_version` 解析所属版本，不再用 `display_group` 区分功能等级。
- 当前版本只保留“新功能与改进”一个功能列表标题；所有条目的字号、间距、描述和进度圆环结构一致。
- 后续版本独立显示为“接下来 · Vx.x”，条目平级且不展示未确定的进度圆环。
- 版本号和预计推出时间改为上下排列；删除可见的 Development Note 标签，保留说明正文。
- 功能区、版本元信息和说明正文限制为 660px，避免宽屏下标题与圆环失去关联。
- 后台用“所属版本”替代“展示位置”，批量导入用“导入到版本”；新增单项始终默认进入正在编辑的当前版本，之后可通过“所属版本”调整。
- 上移/下移只在同一版本内排序。

## 数据兼容

- 不新增数据库表或列，不执行数据迁移。
- 当前版本继续由父级 `release_previews.version` 表示；其他版本复用 `target_version`。
- `display_group` 保留为旧 schema/API 兼容字段：当前版本保存为 `featured`，其他版本保存为 `future`；历史 `improvement` 可继续读取和保存，但前台不依赖它决定样式或分组。
- 旧 `content_*`、旧数组和 schema v2/v3 信封继续走现有 fallback。

## 本地验证

- `npm run typecheck`：通过。
- `npm run test:esa-api`：通过。
- `npm run build:esa`：通过，生成 17 个静态页面且 API 源码恢复。
- `python -m py_compile tests/smoke.py`：通过。
- 当前 Python 环境未安装 Playwright；发布后使用本机 Chrome CDP 对生产页面做桌面与移动端结构、尺寸和溢出核验，并明确记录该限制。

## 发布边界

- 不写入生产预告数据，不修改版本预告之外的页面或桌面端代码。
