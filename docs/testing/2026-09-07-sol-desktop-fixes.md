# 桌面修复交接：Sol 前序实现 / Astra 轻度收尾

日期：2026-09-07。基线 HEAD `7343def786e853f236ea1707f76f3e1ee919368e`，工作区 `D:\AppleMusicDesktopLyrics\.worktrees\qa-astra-sol-fixes-20260907`。未提交、推送、整合或发布。官网改动由另一任务负责，本收尾没有修改 website。

依据 root 的 `artifacts/qa-20260907-deep/astra-high-review.md`、两轮 current-version review 与 `docs/testing/2026-09-07-astra-sol-fix-validation.md`。已核对 Core/UI 章程、桌面契约、既定决策、回归基线；保留版本、身份、用户实际配置、Widgets、注册表与历史证据。

## 实现结果

| 问题 | 当前行为与限制 |
| --- | --- |
| N03 用户开关丢失 | `DisableTaskbarLyrics` 和设置同步失败只改变运行时，不再把 `LyricDockEnabled` 保存为 false。设置页提示暂停；重试/重启仍有原用户意图，明确关闭仍持久保存 false。Island 原本 false 时，临时 fallback 标志进入 `ShowIsland` 和可见性判断，不写回 Island 选项。 |
| N01 / D08 / D09 | 获取与恢复放入同一后台串行队列，旧 continuation 不恢复后继租约；关闭与 Dispose 排队恢复。Settings 操作失败保留原恢复记录，确认恢复成功后才删除。15 秒 helper 超时后仅终止本次 Process.Start 返回的 helper，并等待退出后才让队列恢复，未按进程名杀 SystemSettings 或 Explorer。若 Kill 被拒绝，后台等待自然退出，恢复可能延迟，不能声称有固定完成时限。 |
| 第一轮 1 / D01 / D02 / D03 / N02 | 缓存路径使用完整规范化 Unicode 身份哈希，保留相邻时长与语言分区；旧不可信 slug 不直接迁移。候选在组合源和 MainWindow 提交前统一验证可展示行、时间及可读身份。缓存读取异常视作 miss，写入异常不丢有效歌词；最新代次和曲目身份检查覆盖显示与缓存提交。 |
| 第一轮 2 / D04 | QQ legacy 补取网络/取消/JSON 失败保留可用 primary；跨源翻译通过原文匹配合并，不按数组下标或近似时间硬拼。既有匹配阈值不足时保留原包，不声称短样本翻译必定补齐。 |
| 第一轮 3 / D05 / D06 / D07 | 逐字解析保留完整可见原文、元数据、混合逐行内容；歧义时间安全降级为逐行，未新增可证明的旧相对格式识别。UI 按字体排版测量词边界裁剪，Core 不接收字体逻辑。设置读取损坏、备份失败和迁移保存失败分别处理，保留已经读到的有效配置。 |

Astra 轻度收尾另修复了新增 `LyricsPackageValidator` 的语言脚本漏洞：原实现仅凭中文/英文不同且歌手相符接受冲突标题；现在无可信别名 provenance 的可读 `[ti:]` 冲突一律拒绝，增加同歌手错误英文标题反例。没有伪造 metadata、改写 ILyricsClient 或新增猜测别名。

**条件兼容边界：** 现有独立本地化搜索测试仍通过，但它们使用不含冲突 `[ti:]` 的歌词包。这不证明所有含 metadata 的合法跨语标题均受支持。字符串包没有可靠别名证据时，真实合法别名也可能被拒绝；这是 fail-closed 降级，后续若扩展需明确 Core provenance 契约。

## 最后变更后的验证

SDK `3.1.426`；设置 `TargetPlatformSdkPath=C:\Program Files (x86)\Windows Kits\10\`、`TargetPlatformDisplayName=Windows`。完整测试设置 `NUGET_PACKAGES=C:\Users\14731\.nuget\packages` 并移除 `LYRICHOVER_SKIP_RELEASE_VERSION_FIXTURE`。日志位于本工作区 `.tmp/`，未覆盖历史日志。

| 命令 | 结果 | 日志 |
| --- | --- | --- |
| `dotnet build LyricHover.sln -c Release --no-restore` | 退出 0；190 条既有 warning、0 error | `astra-low-final-strict-build.log` |
| `dotnet run --project LyricHover.Tests -c Release --no-build` | 退出 0；231 PASS、0 FAIL，包含 release version fixture，无跳过 | `astra-low-final-strict-tests.log` |
| `dotnet build LyricHover.Core.TranslationContractTests -c Release --no-restore` | 退出 0 | `astra-low-final-strict-translation-sdk-build.log` |
| `dotnet run --project LyricHover.Core.TranslationContractTests -c Release --no-build` | 退出 0；全部独立翻译契约通过，含既有本地化搜索控制 | `astra-low-final-strict-translation-tests.log` |

运行环境失败也保留：最初带 restore 的 build 因沙箱拒读用户 NuGet.Config 失败，故使用已有恢复资产 `--no-restore`；首轮 fixture 使用沙箱默认 package cache 不存在，显式指定已有缓存后全套通过；独立翻译一次遗漏 SDK 环境变量导致 MSB4184，补齐后通过。这些均未修改测试断言以绕过环境。

最终 App DLL SHA-256：`EFD6A3629B5376D2868F898119881C6C535398C73371343530F1A8A43BDA5000`。
最终 Core DLL SHA-256：`2DEF35ED7297618CECAC3A3D36F91DCE2F42C95B29BEE33239FB1B392A7C0619`。

## 证据范围与下一验收

N03 自动测试覆盖临时配置、失败恢复记录、选项保留、重试和明确关闭；root 此前的真实 MainWindow 生命周期对照日志 `lifecycle-baseline-verified.log` / `lifecycle-sol-verified.log` 属于阶段证据，未冒充最后变更后的实机重启结果。临时 Island fallback 最后检查了真实调用路径，但还没有生产窗口可见截图。

租约回归包含屏障控制的 Settings 晚完成后关闭/Dispose、旧获取与后继开启、验证失败保留 Enabled/Absent 恢复记录。未实际开启 Windows Settings、修改 Widgets 或执行真实 helper 超时杀进程；该 helper 清理段本次为源码检查，不能称实机副作用验收。

字形测试使用离屏 WPF、居中、24/36 字号的 `iiiiWWWW`，证明窄字边界不按字符数量均分；字号变化不是 Windows 100%/150% DPI 切换。真实多 DPI、混排标点、实际播放器切歌和完整退出重启仍需 UI 验收。

请 root 进入独立 Astra 轻度验收：优先检查 N03 的 Island=false 可见 fallback 和重启意图、租约各代次收敛、错误跨语 metadata 拒绝。全绿仅代表上述本地回归范围，不表示所有报告验收矩阵、真实 UI、主线或发布已经通过。
