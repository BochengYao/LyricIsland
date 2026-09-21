# 任务交接：版本预告产品预告式重构

- 日期：2026-09-21
- 任务线程：版本预告前台、后台、接口与兼容模型同步重构
- 交接目标：Website Backend / Public Experience / Quality & Release
- 集成基线：`695ff43dd8c871a964edb9dce1e99c150c0a06d1`
- 集成方式：从最新 `origin/main` 创建隔离发布工作树，仅带入下列版本预告白名单文件；主工作目录中的桌面提交、宣传图和其他 WIP 不进入发布提交

## 结果

- 公开页面改为真正两栏的产品预告布局，右栏按“核心功能 / 其他改进 / 接下来 · 目标版本”展示；删除开发状态图例与可见百分比。
- 核心功能保留右侧 16px 语义化进度圆环；其他改进降低层级；future 独立分组且不绘制进度圆环。
- 后台每项可编辑四语标题、四语描述、展示分组、future 目标版本、既有十档进度/状态及排序；版本级 Note 独立编辑。
- 批量导入支持 `标题 | 描述`、`标题｜描述` 和旧版整句格式，并可指定默认分组与 future 目标版本。
- AI 翻译按稳定 Feature ID 分别翻译 title、description 与 developmentNote；版本与结构字段不送翻译。预计发布时间继续复用 `target_date`，通过现有 timing preset 按 locale 确定性生成，不需要 AI 翻译。

## 数据模型与兼容

- 结构化信封由 schema v2 升级为 schema v3，Feature 增加 `title_*`、`description_*`、`display_group`、`target_version`；保留 `content_*`、`body_*` 和 `highlights_*` 兼容投影。
- 不新增 Supabase 列、不删除旧字段、不执行生产数据改写。schema v2、旧 highlights 数组和旧 body 文本均继续可读。
- 旧 content-only 项按 `title=""`、`description=原文` 读取；公开页面在标题为空时显示原整句。
- 仅对 V3.2/V3.3 的已知中文原句执行精确匹配的只读结构化映射；未知文案不做启发式拆句，原始 `content_*` 保持不变。
- 新写入会校验分组枚举；future 必须填写 `target_version`。旧客户端对已结构化记录的降级覆盖仍返回 409。

## 变更文件

- 前台：`website/components/ReleasePreviewArticle.tsx`、`website/components/ReleasePreviewProgressRing.tsx`、`website/components/VersionPreviewSection.tsx`、`website/components/IncentivePage.tsx`、`website/app/globals.css`
- 后台：`website/components/AdminIncentives.tsx`
- 数据与接口：`website/data/incentives-types.ts`、`website/data/release-preview-content.ts`、`website/data/release-preview.ts`、`website/lib/incentive-store.ts`、`website/app/api/incentives/admin/previews/route.ts`、`website/esa/api.js`、`website/supabase/schema.sql`
- 翻译、测试与文档：`website/lib/content-translation.ts`、`website/scripts/test-esa-api.mjs`、`website/tests/smoke.py`、`docs/api/WEBSITE_API.md`

## 验证

- `npm run typecheck`：通过。
- `npm run test:esa-api`：通过；覆盖 schema v3 往返、稳定 ID/排序、旧数据 fallback、精确迁移、非法分组与 future 缺失目标版本等。
- `node --check esa/api.js`：通过。
- `python -m py_compile tests/smoke.py`：通过。
- `git diff --check`：通过，仅出现仓库既有 LF/CRLF 转换提示。
- `npm run build:esa`：通过，Next.js 16.2.10 生成 17 个静态页面。
- Chrome 153 CDP 响应式检查：公开页 1440/1024/768/390 均无横向溢出；后台 1440/1024/768/390 均无横向溢出，768px 和 390px 编辑器为单列。

## 未验证与边界

- 未连接或写入真实 Supabase，未执行生产数据迁移。
- 未用真实管理员会话保存/发布草稿，未调用真实 AI 翻译服务。
- 本机 Python 未安装 Playwright；安装临时浏览器运行时未完成，因此未执行完整 `tests/smoke.py` 浏览器流程。响应式检查改用本机 Chrome CDP 静态验收。
- 与任务无关的宣传图、`release5/`、`w/`、`output/canva-assets/` 均未纳入发布提交。

## 结论

代码与本地回归状态为 `READY_FOR_RELEASE`。推送与 ESA 传播证据由发布操作单独记录；生产仍需在授权后台验证真实草稿往返、四语 AI 翻译、发布/撤回，以及生产 Supabase 中现有 V3.2/V3.3 数据的只读兼容展示。
