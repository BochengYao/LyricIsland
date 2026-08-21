# 回归测试基线

本页记录可复现的官网与桌面验证命令，以及已确认存在的失败基线。测试线程必须在报告中标明所用提交、环境和命令，不能将既有失败误判为新改动引入。

## 官网

工作目录：`website/`

```powershell
npm run typecheck
npm run build:esa
npm run test:esa-api
```

根据任务额外运行 `npm run check:support-security`。构建成功只验证静态产物；生产发布仍须按照 [发布清单](../release/RELEASE_CHECKLIST.md) 检查真实路由。

## 桌面端

工作目录：仓库根目录。Windows SDK 环境需要时先设置：

```powershell
$env:TargetPlatformSdkPath = 'C:\Program Files (x86)\Windows Kits\10\'
$env:TargetPlatformDisplayName = 'Windows'
dotnet build LyricHover.sln -c Release
dotnet run --project LyricHover.Tests -c Release --no-build
```

构建与测试要分开报告；`--no-build` 的测试必须建立在刚完成的同一配置构建上。

## 已知桌面失败基线

2026-08-06，在未修改的 `origin/main` 基线上复现以下两项失败：

1. `translation mode explains why single line is unavailable`
2. `mouse avoidance settings fit without scrolling`

这两项在后续变更中仍应单独报告：若失败信息、数量或关联行为改变，则视为需要调查的回归，而不是自动忽略。其他失败、超时、编译错误或环境错误都不是本基线的一部分。

### 2026-08-21 基线解除记录

上述两项失败已在 V3 任务栏歌词分支（`codex/feature/v3-taskbar-lyrics`）上定位并解除，均为测试自身缺陷而非产品行为缺陷，未为绿灯修改无关业务代码：

1. `translation mode explains why single line is unavailable`：断言全文件不得出现 `MessageBox.Show(`，被徽章刻字与任务栏歌词确认弹窗等无关合法确认误中；已收窄为仅约束翻译单行限制提示路径（`SingleLineRadioButton_PreviewMouseLeftButtonDown` 至 `FadeOutTranslationModeToast` 区域）。
2. `mouse avoidance settings fit without scrolling`：断言依赖 LF 换行的跨行精确匹配，在 CRLF 检出环境下必然失败；已先归一化换行再断言，断言语义不变。

解除证据：同一 Release 构建下 `dotnet run --project LyricHover.Tests -c Release --no-build` 输出 192 项全部 PASS、0 项 FAIL。自本记录起，桌面端不再存在已登记的失败基线；后续任何失败都按新增回归处理。

## 交接要求

将命令、退出码、关键输出、手动验收条件及已知失败的状态写入各任务独立的交接文件。模板见 [HANDOFF](../coordination/templates/HANDOFF.md)。
