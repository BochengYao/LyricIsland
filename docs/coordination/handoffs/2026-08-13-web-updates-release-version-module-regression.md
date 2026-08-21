# 官网前台交接：新功能页按完整版本展示全部模块

- 日期：2026-08-13
- 线程：01-web-frontend
- 基线：`origin/main` `42aaaa44bed2b906535dabba9dd610c4b95d0b51`
- 范围：仅公开官网前台组件；未修改管理端、API、鉴权、数据库、内容数据或发布环境。

## 完成内容

- `website/components/ManagedFeatureContent.tsx` 的下拉选项现在一项对应一个规范的完整 `release_version`，不再把 section/模块、`major_version` 或预告混入菜单。
- 版本按语义版本倒序去重；仅保留 `/api/features` 返回且至少有一个可见模块的版本。没有模块内容的版本（例如当前可能为空的 `V2.0.40`）不会造成可选的空白页。
- 选择版本后，页面一次渲染该 `release_version` 的全部可见模块。每个模块仍分别保留标题、描述和完整小点列表；四语继续由既有 `localizedFeatureContent` 字段映射处理。
- 保持 `UpdatesPage` 的既有页面外壳、导航、下载收尾区和页脚；`updates-copy.ts` 已完全恢复 `42aaaa44` 的原有四语标题、引言与 release label，未随本次提交变更。

## 接口依赖

- 公开读取接口：`GET /api/features`。
- 每个模块需要唯一完整 `release_version`（例如 `V2.0.36`）；`major_version` 仅为派生分组信息，不作为选择器或可见版本标签来源。

## 验证与后续

- 本轮用户明确授权跳过浏览器/质量验收，待 ESA 上线后由用户人工验收；未部署 ESA。
- 在文案恢复前曾成功运行：`npm run typecheck`、`npm run build:esa`、`npm run test:esa-api`。根据用户最新授权，恢复文案后的最终工作树不再重复执行测试。
- 建议人工验收 `/updates`、`/en/updates`、`/zh-hant/updates`、`/ja/updates`：确认原标题与引言、版本菜单仅显示真实有模块的版本、选择一个版本后显示该版本所有模块与完整描述/小点。

## 回滚

- 回滚本次提交即可恢复此前前台版本选择实现；不涉及服务端或数据迁移。
