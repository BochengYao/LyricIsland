# Astra 复盘 / Sol 修复 / Astra 验收记录

- 用户明确要求：Astra 高推理复盘，Sol 中等推理修复，Astra 中等推理验收；并指定优先检查“歌词坞首次开启后，退出重开丢失开关与设置”。
- 后续用户更新：**所有子智能体统一使用 Astra 轻度推理**。已完成的高推理复盘和 Sol 修复保留，桌面修复收尾及独立验收改由 Astra 轻度执行。
- 2026-09-08 最新分工：**规划和最终验收使用 Astra 轻度；编码和初步检查使用 GPT-5.6 Terra 中度**。保留前述已完成工作，剩余桌面恢复门禁修复按新分工执行。
- 基线：`7343def786e853f236ea1707f76f3e1ee919368e`。
- 独立工作区：`D:\AppleMusicDesktopLyrics\.worktrees\qa-astra-sol-fixes-20260907`。
- 分支：`codex/feature/qa-astra-sol-fixes-20260907`。
- 本任务授权 Core/App/官网相应缺陷修复及必要回归，按桌面和官网分工；不重做设置系统，不改版本、商店身份、实际用户配置、历史证据或生产环境。

## 阶段状态

1. Astra 高推理：已完成[独立复盘报告](../../artifacts/qa-20260907-deep/astra-high-review.md)，用户重启反馈作为 N03 优先交接。
2. 修复：官网 Sol 已完成；桌面 Sol 完成主要修改、Astra 轻度完成第一轮收尾，桌面套件 231 PASS。独立验收发现剩余恢复门禁问题，由 Terra 中度继续修复和初检。
3. 独立验收：官网 Astra 轻度已通过；桌面 Astra 轻度已确认启动恢复失败后 Configure(true) 可绕过暂停保护，等待修复后复验。当前不视为桌面最终通过。

## 用户反馈的新增复现

只读检查显示本机存储的 `LyricDockEnabled=false`；仍存在内容为 `Enabled` 的小组件恢复记录，诊断日志多次记录恢复写入被拒绝。此为与代码路径吻合的现场线索，未自动重启用户程序、未修改任何实际系统状态。

临时隔离探针调用真实 `LyricDockController.Start` 和 `MainWindow.DisableTaskbarLyrics`：先保存开关 true，模拟启动恢复失败，再重新加载设置，输出：

```text
startup dock preference persistence: requestedBefore=True started=False persistedAfter=False recoveryKept=True
HARNESS_ERRORS=0
```

证据：[新复现日志](../../artifacts/qa-20260907-deep/app-probes-dock-persistence-verified.log)，[真实方法探针](../../artifacts/qa-20260907-deep/app-probes/LeaseProbes.cs)。此前探针反射字段名错误的日志保留，不将其作为产品失败。

验收要求：正常开启/修改/退出/重新加载后保留用户选项；恢复或任务栏探测暂时失败时安全暂停运行，不将用户开启意图覆盖保存为 false；再次启动或环境恢复时可按已保存意图恢复；用户明确关闭仍持久保持关闭；原始小组件状态恢复记录不可因暂时失败删除。

## 独立工作区修复前基线

- Release 全新构建：退出 0，190 条既有警告、0 错误（以测试链接源码导致的 CS0436 重复类型为主）。
- 完整桌面套件：221 PASS / 0 FAIL，包含发布 fixture，没有跳过项。
- `npm ci --ignore-scripts --no-audit --no-fund`：退出 0，353 个包，锁文件未修改。
- 原 `npm run typecheck` 在沙箱子进程报 `tsc is not recognized`；直接 `node node_modules/typescript/bin/tsc --noEmit` 退出 0。记录为命令启动环境差异，不当作 TypeScript 源码错误。
- 日志均在 `artifacts/qa-20260907-deep/worktree-baseline-*` 和 `worktree-npm-ci.log`。

## 修复与验收结果

桌面修复仍在上述独立工作区；官网 10 个已验收文件于 2026-09-08 按 SHA-256 清单整合进主目录，整合前逐个确认目标没有其他未提交修改，整合后哈希一致。未发布。

### 2026-09-08 继续检查

- 官网独立验收通过，报告 `artifacts/qa-20260907-deep/astra-low-web-acceptance.md`；整合清单 `web-reviewed-files.json`。根目录直接执行 TypeScript、ESA API、安全检查、W01–W05 行为回归均退出 0。
- 桌面独立验收发现启动恢复门禁仍可被普通设置同步绕过：真实目标 DLL 探针输出 `start=False configure=True enabled=True visible=True recovery=True`。证据 `startup-gate-final.log`，交 Terra 中度修复。
- 同次审查追加混合逐字/普通 LRC 行丢失，以及双重合法时序被首词起点启发式误判两个反例，证据 `startup-gate-second.log`。同组修复，不将此前 231 PASS 当作最终验收通过。
- Astra 源码审查补充正常退出缺口：宿主在后台恢复队列完成前释放环境并结束进程。与前三项一并交 Terra，独立报告为 `astra-low-desktop-acceptance.md`；该退出项为源码证据，未操作真实 Widgets。
- 根目录整合后 ESA 构建 17/17 通过，`app/api` 已恢复，日志 `integrated-web-build.log`；实际 SQL/ESA 受控回归 10 PASS / 0 FAIL（同样仅单连接）。`git diff --check` 退出 0。
- Terra 四项返修初检完成：完整桌面 232 PASS / 0 FAIL，翻译契约通过，三个原失败反例通过。首轮混合行失败日志保留，修复后未放宽两行断言。此时 App SHA-256 为 `8645E7485C38EF7F85B78349BB86DE8CF15596451A6F0D50DEB1DC2A08C1E8D4`；18 文件审查输入清单 `terra-final-desktop-input-files.json`。独立最终验收仍在进行，编译警告由 190 增至 197 也纳入检查，未预先归为既有警告。
- Astra 独立运行确认恢复待完成状态中的重试、关闭、Dispose、关闭再开启四项通过；退出方法的真实 WPF 受控宿主反例发现：队列 Task 已完成时 await 同步返回，Closing 内再次 Close 导致 `InvalidOperationException`。证据 `final-desktop-probe.log`，继续交 Terra 将最终关闭排到 Dispatcher 后续回调；第一轮初检通过不代表此分支通过。

## 最终验收与整合

Terra 中度完成两轮退出修正：先将二次关闭排入 Dispatcher，避免在 `Closing` 调用栈内重入；Astra 再发现隐藏歌词坞窗口会使默认 `OnLastWindowClose` 无法结束进程，Terra 改为在队列完成后的 Dispatcher 回调调用应用级 Shutdown。Astra 轻度最终独立验收通过：双隐藏窗口真实 `Application.Run` 的 completed、delayed、repeated 三种顺序均在关闭前完成恢复、关闭隐藏窗口并退出；pending retry/off/dispose/off-on 和原解析反例均通过。报告 `artifacts/qa-20260907-deep/astra-low-desktop-final-acceptance.md`。

冻结的桌面 18 文件清单与最终程序集 SHA-256 `154455BE9A39D96401026A7729C325F80B257175D7BFF7F3014285158BE63D87` 已比对后整合入主工作区。主工作区重新执行 Release 构建（0 errors、197 warnings）、桌面完整 232 PASS / 0 FAIL、翻译契约通过；官网 W01–W05、ESA API、安全检查通过，ESA 构建 17/17 已通过。197 条警告中新增的 7 条是测试工程链接新增歌词坞类型后产生的同类 `CS0436`，无新增警告类别。未提交、推送、打包、部署或操作真实系统状态。

### 阶段内独立验证

- 首要 N03 使用同一个[生命周期断言探针](../../artifacts/qa-20260907-deep/lifecycle-acceptance/Program.cs)对比真实程序集：旧基线失败（`persistedAfter=False`），Sol 当前构建通过（`persistedAfter=True recoveryKept=True`）。每次执行前比较探针复制的 App DLL 与目标构建 SHA-256，确保没有串用程序集。日志为 `lifecycle-baseline-verified.log`、`lifecycle-sol-verified.log`。此用例覆盖 Island 原本启用的启动恢复失败；不是整个实际桌面的重启验收。
- 初次 `dotnet run -p:ProductRoot=...` 未切换该 SDK 的构建属性，默认引用修复工作区；该日志保留为 `lifecycle-initial-default-worktree.log`，不作为旧版失败证据。正式对照使用单独 `dotnet build ... -p:ProductRoot=...`、哈希校验、再执行 DLL。
- 根目录 artifacts 中临时安装 PGlite 0.5.8，运行 PostgreSQL 18.3 的内存引擎，执行实际仓库 schema/RPC 与实际 ESA 入口经受控 SQL 传输层的请求。项目依赖不变，无生产连接。
- SQL/ESA 对照：旧基线 5 PASS / 4 FAIL，包含实际两访客请求明细为 2、计数为 1；Sol 第一版修正为 9 PASS。追加 JSON `public` 必须严格为布尔值的检查后发现 1 项新边界失败，交回 Sol 修改 schema 和迁移后 10 PASS / 0 FAIL。
- 进一步从旧 schema、已有点赞数据执行独立迁移并重复迁移，加入历史数据保留检查后 **12 PASS / 0 FAIL**。证据：[实际 SQL 验收程序](../../artifacts/qa-20260907-deep/db-acceptance.mjs)、[迁移验证日志](../../artifacts/qa-20260907-deep/db-sol-migration-verified.log)。这不是多连接生产锁竞争验证：PGlite 使用单连接，测试范围为 SQL 执行、事务回滚、权限、幂等、迁移和受控 API 请求交错；[引擎限制说明](https://pglite.dev/docs/pglite-socket)。

这些是修复期间的节点结果，最终以按用户最新要求执行的 Astra 轻度独立验收和整合后的代码为准。
