# 任务交接：版本预告结构化进度、紧凑后台与公开图例

- 日期：2026-09-20
- 任务线程：升级版本预告功能结构
- 交接目标：Quality & Release
- 基线提交：`77cd50ff5a7566397b1d7e26a72fde8565193da9`
- 结果提交：`2564b533d14281972cad94aae330081c47d1b351`
- 远端状态：`refs/heads/main` 已指向结果提交；推送已触发既有 ESA 自动部署流程，生产页面尚需独立浏览器复核
- 允许修改范围：版本预告公开组件、维护者后台、预告 API/存储兼容层、ESA 运行时镜像、官网测试、接口文档与交接文档

## 已完成

### 内容结构与兼容

- 版本级 Note 与逐条 Feature 分离，四语内容按稳定 Feature ID 对应；后台支持批量导入、新增、删除、排序、逐语编辑、翻译全部和仅补缺失翻译。
- 沿用 `release_previews` 及既有四语字段，不新增数据库列。结构化 Feature 写入 `highlights_zh` 的 schema v2 兼容信封，同时保留旧 `body_*` / `highlights_*` 响应。
- 旧数组记录继续可读；缺失进度按 0 读取，历史任意百分比向下归入相邻锚点，历史 100% 默认按“测试中”读取。
- schema v2 记录拒绝旧客户端仅携带 `body_*` 的正文 PATCH，避免稳定 ID、进度和译文被静默清空。

### 进度与状态

- 后台每条功能使用一条 10 档滑块：`0、10、30、50、65、80、90、95、测试中、待上线`。
- 开发阶段只允许 `0/10/30/50/65/80/90/95`；“测试中”和“待上线”分别保存为 `stage=testing/ready`、`progress=100`。
- Next API 与 ESA API 均拒绝任意开发百分比、非法 stage、重复/保留 ID 以及不一致的 stage/progress 组合。

### 前后台视觉

- 公开版本预告使用四态 16×16 SVG 圆环：0 为灰环，10–95 为橙黄进度环，测试中为橙黄呼吸环，待上线为橙黄静态环；updates 与 incentives 均显示四语图例。
- 测试中只绘制一个圆环，不叠加底环；呼吸效果仅改变透明度，不使用发光阴影。
- 呼吸动画作用于固定 16×16 SVG 外框，图例内四个图标清除额外顶部偏移并与文字垂直居中。
- 后台功能项使用紧凑编辑布局：宽屏下内容与进度并排，短文本框约两行高，长内容自动增高且不截断；较窄屏恢复单列，移动端保留 44px 操作按钮。

## 提交链

1. `74a4a02e9d8992ec7f66ecb79ff5033d838c3e6c` `feat(website): structure release preview progress`
2. `84eee6ca2d80955ec39d980e0e8caafdf64045f6` `feat(website): add discrete release preview states`
3. `6877ed5e13d9d0cdeed56774ab3afac4aa3f7846` `feat(website): use slider for release preview states`
4. `687c417f3cb4a2f93dc3b94d30269abc5f6bbbcf` `fix(website): reject unknown preview states`
5. `e9355a771c89a738befbd5e3f373fda39d0c74b7` `fix(website): refine preview progress anchors`
6. `0fbc2f0651e810cd4d9d63050b482f1bb01567e0` `fix(website): compact preview feature editor`
7. `2564b533d14281972cad94aae330081c47d1b351` `fix(website): align preview testing legend`

## 变更文件

- 契约与交接：`docs/api/WEBSITE_API.md`、`docs/coordination/handoffs/2026-09-16-web-release-preview-structured-progress.md`
- 管理端：`website/components/AdminIncentives.tsx`、`website/app/api/incentives/admin/previews/route.ts`
- 公开端：`website/components/IncentivePage.tsx`、`website/components/VersionPreviewSection.tsx`、`website/components/ReleasePreviewProgressRing.tsx`
- 数据与兼容：`website/data/incentives-types.ts`、`website/data/release-preview-content.ts`、`website/data/release-preview.ts`、`website/lib/content-translation.ts`、`website/lib/incentive-store.ts`、`website/supabase/schema.sql`
- ESA 与测试：`website/esa/api.js`、`website/scripts/test-esa-api.mjs`、`website/tests/smoke.py`、`website/app/globals.css`

## 未修改 / 非目标

- 未连接或写入真实 Supabase，未执行迁移、批量改写或删除生产数据。
- 未修改管理员认证、会话和权限模型。
- 未修改桌面端、安装包、Microsoft Store、视觉宣传资产或版本号。
- 未把本地静态构建当作生产页面或真实管理员会话验收。

## 验证

- `node node_modules/typescript/bin/tsc --noEmit`：通过。
- `node scripts/test-esa-api.mjs`：通过，输出 `ESA API tests passed`；覆盖结构化保存/读取、排序、稳定 ID、10 档规则、testing/ready、非法输入、旧数据和旧客户端降级写入保护。
- `node --check esa/api.js`：通过。
- `python -m py_compile tests/smoke.py`：通过。
- `git diff --check`：通过。
- `node scripts/build-esa-static.mjs`：通过；Next.js 16.2.10 完成 17 个静态页面构建。
- `git ls-remote origin refs/heads/main`：确认远端为 `2564b533d14281972cad94aae330081c47d1b351`。

## 已知限制

- 本机 Python 环境未安装 Playwright，未执行 `website/tests/smoke.py` 的真实 Chromium 流程。已有 smoke 断言覆盖 10 个滑块档位、无后台额外圆环、紧凑条目高度、长文本自动增高、测试中单 circle 与图标/文字中心对齐，但这些断言需要 Quality & Release 在可用浏览器环境实际运行。
- 本轮仅确认 GitHub `main` 推送及 ESA 自动部署触发，未把生产传播完成或缓存刷新视为已验证。
- 真实管理员会话、四语 AI 翻译服务和生产数据保存未由自动化触碰。

## Quality & Release 验收清单

1. 在生产 `/admin` 使用授权会话进入“版本预告”，确认每条功能仅有一条滑块，依次停靠 0、10、30、50、65、80、90、95、测试中、待上线。
2. 确认宽屏典型条目约 120–150px 高，内容与进度并排；长文本完整自动增高，无截断、遮挡、内部异常滚动或横向溢出。
3. 在 1024px、768px、390px 宽度确认布局回落为可读单列，按钮可点击且不溢出。
4. 在 `/updates` 与 `/incentives` 检查四语页面：0 灰环、开发中进度环、测试中单个呼吸环、待上线单个静态环。
5. 重点确认“测试中”圆环与同排文字垂直居中，不出现双环、下沉、发光或位置跳动；开启 `prefers-reduced-motion` 后不播放动画。
6. 使用草稿验证新增、排序、删除、切换语言、翻译全部、仅补缺失翻译、保存后刷新读取与发布/撤回；不要以正式生产预告作为破坏性测试数据。
7. 用无权限/过期会话确认管理接口仍返回 401，公开接口不泄露管理字段。
8. 使用无缓存请求确认生产静态资源已传播到结果提交；记录页面响应、资源指纹和验收时间。

## 风险与回滚

- 代码回滚点为基线 `77cd50ff5a7566397b1d7e26a72fde8565193da9`；如仅回滚末端视觉问题，可按提交逆序逐个 revert，不要重置共享分支历史。
- schema v2 数据对旧公开运行时保持可读，但旧后台再次保存可能丢失结构化元数据。若生产已用新后台保存记录，完整回滚前先导出对应 `release_previews` 行。
- 若生产出现 API/静态版本混部署，保持新 API 的 409 降级写入保护，先完成静态资源传播或回滚整条提交链，不要关闭校验绕过。

## 交接结论

实现、接口回归、类型检查、语法检查、静态构建、GitHub 推送均已完成。当前状态为 `READY_FOR_REVIEW`：等待 Quality & Release 完成真实生产浏览器、授权后台、响应式和数据往返验收后再标记最终完成。
