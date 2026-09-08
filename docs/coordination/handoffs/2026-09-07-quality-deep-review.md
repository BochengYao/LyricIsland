# 任务交接：3.2.36-Beta 第二轮异常与并发检查

- 日期：2026-09-07。
- 任务线程：Quality & Integration，承接“继续检查所有bug”。
- 基线提交：`7343def786e853f236ea1707f76f3e1ee919368e`，功能源码与第一轮 `d78e57a85cccca8871b8ef400c9df85bd6ef3aa2` 相同。
- 结果提交：未提交。
- 允许修改范围：独立测试报告、本交接、本地 `artifacts/qa-20260907-deep/` 验证辅助材料。

## 已完成

- 真实 App/Core 程序集上的缓存、请求代次、翻译、逐字显示、设置恢复和歌词坞生命周期探针；WPF 字形证据和 2000 次有限布局压力测试。
- 真实 ESA API 本地 mock 的公开列表、并发点赞、兑换码删除/分配、鉴权及公开字段测试；真实 TSV 解析器边界测试。
- 新增 14 项可复现场景，逐项 Evidence + Impact + Suggested Owner、输入、位置、建议及局限见[完整报告](../../testing/2026-09-07-current-version-deep-review.md)。第一轮问题仍未修复。

| 优先级 / 接收方 | Evidence | Impact |
| --- | --- | --- |
| P1 / Desktop UI 歌词坞，Release 协作验收 | D08：关闭小组件后验证失败，恢复文件被删除 | 不能恢复原 Windows 状态 |
| P1 / Website Admin & Data | W03：读取 available 后发生分配，DELETE 不带状态条件 | 可能删除已分配兑换码 |
| P2 / Desktop UI + Core | D01–D03：无效响应覆写缓存、坏缓存阻断强刷、旧请求覆写新缓存 | 歌词不可用与异步状态不一致 |
| P2 / Desktop Core + UI | D04–D07、D09：跨源翻译错位、相对词时间误判、字宽高亮偏差、设置锁定异常、共享租约竞态 | 展示、恢复和生命周期异常 |
| P2 / Website Admin & Data + Frontend | W01–W02、W04–W05：先截断后过滤、点赞少计、非法日期放行、公开标题正文漏过滤 | 数据一致性和公开内容策略失效 |

## 未修改 / 非目标

- 业务实现、正式测试断言、用户设置、注册表、实际小组件、生产数据、版本和发布候选均未改动。
- 保留其他任务 WIP 与第一轮全部证据；未提交、推送、上传、发布或向其他任务发送消息。
- 三项未证实正常用户路径的观察单独列出，未混入 14 项计数。

## 验证

```powershell
$env:TargetPlatformSdkPath='C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName='Windows'
dotnet run --project artifacts/qa-20260907-deep/app-probes -c Release --no-restore
node artifacts/qa-20260907-deep/web-probes.mjs
git diff --check
```

- 两组探针退出 0，桌面 `HARNESS_ERRORS=0`，表示执行完成；输出中包含确认的异常行为，不能当作产品绿灯。
- 桌面日志：`artifacts/qa-20260907-deep/app-probes-chain-verified.log`；官网：`artifacts/qa-20260907-deep/web-probes.log`；图片：`artifacts/qa-20260907-deep/word-boundary.png`。
- DLL 哈希、源码位置、准确复现条件和控制样本见完整报告。
- 221 项完整桌面测试及官网构建结果属于第一轮既有执行证据，本轮未重复报成新执行。

## 风险与后续

- 已知限制：合成数据和受控竞态证明实现边界问题；未执行生产数据库/现场 OS 恢复、透明浮层真实交互、多 DPI、多播放器及整机长时测试。
- 交接目标：先处理 D08/W03，再按完整报告拆分对应模块修复；待修复 SHA 明确后针对性复测，并补真实 UI 场景。当前不能宣称全部 bug 已查完。
- 回滚点：业务源码保持基线；新增质量文档和 artifacts 独立，无业务回滚操作。
