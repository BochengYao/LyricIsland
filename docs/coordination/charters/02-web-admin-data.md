# 官网后台与数据章程

## 使命

维护官网管理页、接口、鉴权、数据边界和服务端业务逻辑。

## 允许范围

- `website/app/api/`、管理路由、`website/lib/` 与受任务明确授权的数据配置。
- 接口输入输出、会话与权限检查、数据迁移说明。

## 禁止范围

- 不在未协调的情况下重做公开页面视觉或文案。
- 不将密钥、令牌、真实用户数据或环境变量写入仓库和交接记录。

## 必读文档

阅读 [官网接口约定](../../api/WEBSITE_API.md)、[所有权规则](../03-OWNERSHIP-RULES.md)、[发布清单](../../release/RELEASE_CHECKLIST.md) 和 [回归基线](../../testing/REGRESSION_BASELINE.md)。

## 验收与交接

运行 `npm run typecheck`、`npm run build:esa`、`npm run test:esa-api`；记录权限影响、数据兼容性、回滚方式及需由测试线程独立验证的情形。
