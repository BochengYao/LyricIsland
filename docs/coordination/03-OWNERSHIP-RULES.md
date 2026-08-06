# 所有权与冲突规则

## 目录边界

- 官网前台：`website/app/(zh)`、`website/app/(en)`、公开组件、`website/public`、公开文案与 SEO。
- 官网后台：`website/app/api`、`website/app/(zh)/admin`、`website/lib`、`website/supabase`、后台数据模型。
- 桌面核心：`LyricHover.Core`；桌面 UI：`LyricHover.App`；测试：`LyricHover.Tests` 与 `website/tests`。
- 发布：`store/`、根目录发布脚本、`README*`、`CHANGELOG.md`；视觉：`视觉宣传/`、资产文件；法务：`docs/software-copyright/`、`docs/patent/`、`evidence/`。

## 共享文件

`LyricHover.sln`、`Directory.Build.props`、`.gitignore`、根 README、`website/package.json`、共享样式/文案、测试基线和发布清单均为共享文件。修改前必须在交接记录声明并由项目统筹协调；没有声明不得顺手改动。

## 工作方式

- 一项可合并改动一条短期分支；分支名前缀使用 `codex/feature/desktop-`、`codex/feature/web-`、`codex/docs-` 或 `codex/release-`。
- 功能线程先完成本地针对性验证；测试线程只承担跨模块、回归与发版门禁，不接管日常功能自测。
- 当前 `wip/local-assets-20260806` 仅保存本地未提交资产，其他线程不得清理、提交或重置其内容。
