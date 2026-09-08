# 3.2.36-Beta 缺陷修复与整合验收

日期：2026-09-08。基线：`7343def786e853f236ea1707f76f3e1ee919368e`。结果未提交、未推送、未打包或发布。版本保持 3.2.36-Beta。

用户授权桌面 Core/UI 与官网对应缺陷修复。最新分工为 Astra 轻度规划/最终验收、GPT-5.6 Terra 中度编码/初检；此前 Astra 高复盘、Sol 中度主体修复及 Astra 轻度收尾作为已完成阶段保留。

## 当前状态

- 官网：Astra 独立验收通过，10 个文件按 SHA-256 清单整合到根工作区；整合后类型检查、ESA API、安全检查、W01–W05 回归、SQL/ESA 单连接 10 项、ESA 静态构建 17/17 全部通过。
- 桌面：Astra 轻度独立终验通过后，已按冻结的 18 文件 SHA-256 清单整合至主工作区。四项边界与两轮退出链问题均已覆盖：启动恢复门禁、混合 LRC 行保留、歧义逐字时间、正常退出等待后台恢复、Closing 栈内重入，以及隐藏歌词坞窗口导致应用不退出。
- 保留其他任务的 `docs/desktop-core-domain-knowledge.md` 修改及托盘交接文档；未改实际用户设置、注册表、小组件状态或历史证据。

## 修复目的与范围

桌面修复覆盖歌词缓存身份、无效响应/异常回退、异步旧请求提交、QQ 主结果保护、逐字歌词完整性与翻译对齐、字形高亮、设置读写失败，以及歌词坞启用意图和小组件恢复生命周期。用户开启意图不应被暂时恢复失败持久改成关闭；环境不可用时安全暂停并保留恢复记录。

官网修复覆盖公开列表过滤分页、原子点赞、兑换码删除与分配竞争、非法日期以及公开字段过滤。新增数据库迁移必须随代码交接。

## 证据与限制

- 执行记录：[修复与验收记录](../../testing/2026-09-07-astra-sol-fix-validation.md)。官网实现报告：[官网修复](../../testing/2026-09-07-sol-web-fixes.md)。独立检查位于 `artifacts/qa-20260907-deep/`。
- 本地受控探针、离屏 WPF、SQL 单连接及构建不证明真实桌面重启、多 DPI、实际播放器/Windows Settings helper、生产数据库多连接或四语言生产页面已验收。
- 未提供可信别名依据时，可读冲突歌词 metadata 按身份规则拒绝；不声称全部跨语言别名兼容。
- 发布顺序：先由 Website Admin/Data 在目标数据库执行并验证 `website/supabase/2026-09-07-atomic-public-incentive-likes.sql`，再部署新 API。禁止回退到旧的非原子 REST 点赞逻辑。此次没有连接生产数据库。

Suggested Owner：桌面真实 OS/播放器/DPI 与打包验收由 Desktop UI/Core + Release；数据库迁移和真实并发由 Website Admin/Data；生产四语言渲染与缓存由 Website Frontend + Release。当前代码整合与证据收敛由本任务完成。

## 最终本地门禁

- 目标根目录 Release 构建：0 errors，197 warnings。相较旧基线多出的 7 条均为测试工程将新增歌词坞类型链接进同一程序集造成的既有 `CS0436` 类别重复类型警告；没有新增警告类别。
- 根目录桌面完整套件：232 PASS / 0 FAIL，发布 fixture 未跳过；翻译契约通过。
- Astra 独立终验：[桌面最终验收](../../../artifacts/qa-20260907-deep/astra-low-desktop-final-acceptance.md)。它用真实 `Application.Run` 的两个隐藏窗口控制样本复验 completed、delayed、repeated 三种退出顺序，均在退出前完成队列、隐藏窗口关闭、应用循环退出；并复验恢复 pending 的 retry/off/dispose/off-on 四种状态。
- 官网根目录：W01–W05 行为回归、ESA API、安全检查均通过；静态 ESA 构建 17/17 通过。SQL/ESA 的受控单连接回归为 10 PASS / 0 FAIL。
- `git diff --check` 通过。未提交、推送、打包、部署或运行真实系统 helper。
