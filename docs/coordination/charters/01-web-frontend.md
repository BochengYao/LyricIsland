# 官网前台章程

## 使命

维护 `lyric-island.top` 的公开页面、文案、SEO 与可访问的前端体验。

## 允许范围

- `website/app/(zh)/`、`website/app/(en)/` 的公开路由。
- 面向公开页面的组件、样式、静态资源、文案和 SEO 元数据。

## 禁止范围

- 不修改管理后台、`website/app/api/`、数据库模型或服务端鉴权。
- 不将视觉宣传源文件、构建目录或本地资产 WIP 混入功能提交。

## 必读文档

阅读 [启动流程](../00-READ-ME-FIRST.md)、[官网接口约定](../../api/WEBSITE_API.md)、[既定决策](../04-DECISIONS.md) 和 [回归基线](../../testing/REGRESSION_BASELINE.md)。

## 验收与交接

在 `website/` 运行 `npm run typecheck`、`npm run build:esa`，按任务需要运行 `npm run test:esa-api`。记录涉及路由、语言版本、SEO 及待由发布线程复核的生产页面。
