# 任务交接：网易云音乐 SMTC 四语提示与 ESA 发布

- 任务线程：官网前台 / 官网发布。
- 基线提交：`c3d8ba17cb70e727ed0e10647523e45f4c53d78d`（任务开始时的 `origin/main`）。
- 允许修改范围：`website/data/site-copy.ts` 中播放器兼容提示、`website/tests/smoke.py` 中对应四语断言及本交接记录。
- 禁止范围：根工作树现有 WIP、管理后台、API、数据库、桌面端、Store 与视觉资产。

## 目标

将网易云音乐旧的接口限制提示改为手动在“设置-系统”开启 SMTC 的操作提示，并同步简中、繁中、英文和日文公开页面；通过门禁后推送 `main` 触发 ESA，最后复核生产环境。

## 验证计划

- 在隔离工作树的 `website/` 中串行运行 `npm run typecheck`、`npm run test:esa-api`、`npm run build:esa`。
- 运行 `git diff --check`，确认提交只包含上述允许范围。
- 推送后核对 `origin/main` 的 SHA。
- 生产环境核对四语首页 HTTP 200、`Server: ESA` 和新提示内容。

## 结果

- `npm run typecheck`：通过。
- `npm run test:esa-api`：通过，输出 `ESA API tests passed`。
- `npm run build:esa`：通过，生成 17 个静态页面，包含简中、英文、繁中和日文首页。
- 构建产物已分别匹配四种语言的新 SMTC 提示。
- `git diff --check`：通过。
- 生产发布与实际路由验证：提交推送后执行；不能由本地构建结果替代。
