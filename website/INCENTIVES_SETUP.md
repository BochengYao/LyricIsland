# 用户激励计划数据配置

前台页面和后台界面可以在没有数据服务时正常预览，但真实提交、附件、审阅和版本预告需要一个持久化 Supabase 项目。

1. 在 Supabase SQL Editor 中执行 `supabase/schema.sql`。
2. 按 `.env.example` 配置五个服务端变量。`SUPABASE_SERVICE_ROLE_KEY` 只能放在服务端或 Sites 环境变量中，不能添加 `NEXT_PUBLIC_` 前缀。
3. `ADMIN_PASSWORD` 用于 `/admin/incentives` 登录；`ADMIN_SESSION_SECRET` 建议使用 32 字节以上随机字符串。
4. 本地开发把变量写入未提交的 `.env.local`。生产环境通过 Sites 环境变量保存，不能写入仓库。

数据安全边界：

- 用户附件位于私有 Storage bucket，后台每次只生成一小时有效的签名地址。
- 数据表启用 RLS，浏览器不能直接访问；所有请求都经过站点的 Route Handler。
- 后台会话使用 HttpOnly、SameSite=Strict Cookie 和 HMAC 签名。
- 前台昵称/邮箱 Cookie 仅用于在两个表单之间自动带入，不用于后台登录。
- 单次最多 3 个图片或视频；单个 15 MB，总计 30 MB。
