# 桌面歌词坞恢复门禁返修初检（Terra）

日期：2026-09-08；工作树 `D:\AppleMusicDesktopLyrics\.worktrees\qa-astra-sol-fixes-20260907`；基线 `7343def786e853f236ea1707f76f3e1ee919368e`。本记录没有提交、推送、发布、修改真实用户设置、注册表或 Widgets。

## 修复范围

1. `LyricDockController` 在启动残留租约恢复失败后保留内部 pending 状态与用户启用意图。后续 `Configure(true)` 返回 false、不显示 surface，且在同一后台串行队列重新恢复；只在成功后于捕获的 UI context 启用运行时。失败继续保留 recovery 文件和 Island fallback。false、Dispose 和新代次会压制过时 continuation。
2. 成功恢复后新增 `RuntimeRecovered` 通知；`MainWindow` 才清除临时 Island fallback 与设置状态。持久化的 `LyricDockEnabled` 未被改写。
3. `LyricsPackageParser` 合并同包中的毫秒格式行与普通 LRC 行；词级时间同时可解释为绝对或相对时完整降级为逐行，保留可见文本。
4. 关闭链在 `Closing` 取消首次关闭、停止运行时活动并异步等待歌词坞队列；完成后才关闭并在 `Closed` 释放环境。重复关闭受 `shutdownWaitingForDockRestore` / `shutdownFinalizing` 保护；托盘入口改为走该路径。

## 回归与证据

首轮全套测试日志 `.tmp/terra-medium-startup-gate-tests.log` 保留：新增普通 LRC 混合反例初次失败 `Expected <2> but got <1>`。原因是毫秒格式行存在但词时序已按 fail-closed 降级时，解析器仍提前返回，漏合并普通 LRC。修复后保留原两行断言，并保留无歧义绝对、相对词时序正例。

最终环境：SDK `3.1.426`，`TargetPlatformSdkPath=C:\Program Files (x86)\Windows Kits\10\`，`TargetPlatformDisplayName=Windows`，`NUGET_PACKAGES=C:\Users\14731\.nuget\packages`；未设置 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE`。

| 命令 | 结果 | 日志 |
| --- | --- | --- |
| `dotnet build LyricHover.sln -c Release --no-restore` | 退出 0；197 warnings、0 errors | `.tmp/terra-medium-startup-gate-build-final2.log` |
| `dotnet run --project LyricHover.Tests -c Release --no-build` | 退出 0；232 PASS、0 FAIL，包含 release fixture | `.tmp/terra-medium-startup-gate-tests-final2.log` |
| `dotnet build LyricHover.Core.TranslationContractTests -c Release --no-restore` | 退出 0 | `.tmp/terra-medium-startup-gate-translation-build-final.log` |
| `dotnet run --project LyricHover.Core.TranslationContractTests -c Release --no-build` | 退出 0；translation contract tests passed | `.tmp/terra-medium-startup-gate-translation-tests-final.log` |
| Astra 探针 `startup-gate-acceptance/Gate.csproj` | 退出 0；`MIXED lines=2 texts=a|ordinary`，`AMBIGUOUS words=False`，`start=False configure=False enabled=False visible=False recovery=True` | `.tmp/terra-medium-startup-gate-probe.log` |

最终 App DLL SHA-256：`8645E7485C38EF7F85B78349BB86DE8CF15596451A6F0D50DEB1DC2A08C1E8D4`。

## 验收边界

针对性测试覆盖恢复失败后的同步设置门禁、成功重试、普通混合行、两类歧义和关闭路径的 source-level await/重复关闭保护。未操作真实 Windows Settings、注册表、Widgets 或实际退出进程；Astra 仍需对目标 DLL 进行独立探针和最终验收。

## Astra 最终验收追加：关闭重入

先前的 source-level 关闭断言不足。Astra 的受控 WPF `HostCloseProbe.cs` 从实际 `MainWindow_Closing` 抽取方法正文，令队列立即完成后调用 `Close()`；失败证据保留于根仓库 `artifacts/qa-20260907-deep/final-desktop-probe.log`：在当前 `Closing` 栈的 `finally` 再次 `Close()` 会抛出 `InvalidOperationException`。本次将最终关闭改为 `Dispatcher.BeginInvoke(new Action(Close))`，使当前关闭事件返回后才二次关闭；已有 `shutdownWaitingForDockRestore` 和 `shutdownFinalizing` 仍覆盖异步等待及重复关闭。此节点完成后需由 Astra 用同一真实 WPF 探针复验。

Terra 修复后的受控 WPF 探针退出 0：`completed`、`delayed`、`repeated` 三种队列/关闭顺序均在 `Closed` 前完成队列且只调用一次 Dispose，无重入异常；日志为 `.tmp/terra-medium-close-reentry-host-probe-final.log`。最终重新执行 Release build（退出 0，197 warnings、0 errors）与完整桌面测试（232 PASS、0 FAIL、release fixture 未跳过），日志分别为 `.tmp/terra-medium-close-reentry-build-final.log` 和 `.tmp/terra-medium-close-reentry-tests-final.log`；独立 translation build/run 均通过，日志为 `.tmp/terra-medium-close-reentry-translation-build.log` 与 `.tmp/terra-medium-close-reentry-translation-tests.log`。最终 App DLL SHA-256：`12D0B52E7214D6FED995226EDC17F94E902A066920022AB3B97F7F5029A4E19A`；提取的 `MainWindow_Closing` 方法 SHA-256：`63F4470C203725B3614E7D3BCE0798F2B5EAF4A5BA5635B3AF49293573E70AC7`。仍须 Astra 独立复验，不将受控宿主视为真实系统退出验收。

## Astra 最终验收追加：隐藏 dock 窗口与应用级退出

Astra 后续发现 Dispatcher 回调仅 `Close()` 主窗口仍不足：`LyricDockWindow` 由主窗口独立创建且不设 Owner，controller Dispose 只隐藏它；`App.xaml` 未覆写默认 `OnLastWindowClose`。因此隐藏 dock 仍在 `Application.Windows` 时，主窗口关闭不证明进程退出。此次保持队列完成后的 Dispatcher 时序，但将回调提升为 `Application.Current.Shutdown()`；`shutdownFinalizing` 允许 Shutdown 触发的第二次 `Closing` 通过，避免再次等待或重入。Astra 已准备每模式单进程、双隐藏窗口、真实 `Application.Run` 的独立探针，需重新提取最终方法正文后复验 completed/delayed/repeated。

Terra 最终本地回归：Release build 退出 0（197 warnings、0 errors），完整桌面测试 232 PASS、0 FAIL（release fixture 未跳过），translation build/run 通过。日志：`.tmp/terra-medium-app-shutdown-build-final.log`、`.tmp/terra-medium-app-shutdown-tests-final.log`、`.tmp/terra-medium-app-shutdown-translation-build-final.log`、`.tmp/terra-medium-app-shutdown-translation-tests-final.log`。最终 App DLL SHA-256：`154455BE9A39D96401026A7729C325F80B257175D7BFF7F3014285158BE63D87`；`MainWindow_Closing` 方法 SHA-256：`D1C18CB61A685659485B269576BC8BBE5104ABA6401986EB7BCA01D46FC44035`。旧失败证据未覆盖；等待 Astra 独立最终验收。
