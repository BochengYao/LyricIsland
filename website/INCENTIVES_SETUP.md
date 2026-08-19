# 用户激励计划数据配置

前台页面和后台界面可以在没有数据服务时正常预览，但真实提交、附件、审阅和版本预告需要一个持久化 Supabase 项目。

1. 在 Supabase SQL Editor 中执行 `supabase/schema.sql`。已有项目升级时也要重新执行一次；脚本使用 `if not exists`，会补建访问日志表和索引而不删除既有反馈。
2. 按 `.env.example` 配置五个服务端变量。`SUPABASE_SERVICE_ROLE_KEY` 可以填写当前的 `sb_secret_` 密钥或旧版 `service_role` 密钥，只能放在服务端或 ESA 构建环境变量中，不能添加 `NEXT_PUBLIC_` 前缀。
3. `ADMIN_PASSWORD` 用于 `/admin` 登录；`ADMIN_SESSION_SECRET` 建议使用 32 字节以上随机字符串。
4. 本地开发把变量写入未提交的 `.env.local`。生产环境通过 ESA「函数和 Pages → 基本信息 → 构建信息 → 环境变量」保存，不能写入仓库。

检查清单：

- 新增数据表功能后必须在主 Supabase 项目（非支持者项目）重跑 `supabase/schema.sql`，否则依赖新表的后台功能（如促销代码管理）会因表缺失而返回 503 `TABLE_NOT_INITIALIZED`。

ESA 部署会在构建期间生成仅供边缘函数使用的 `esa-dist/entry.js`。该文件不会进入静态资源目录，也被 Git 忽略；浏览器只能调用 `/api/incentives/*`，不能读取服务器密钥。

数据安全边界：

- 用户附件位于私有 Storage bucket，后台每次只生成一小时有效的签名地址。
- 数据表启用 RLS，浏览器不能直接访问；所有请求都经过站点的 Route Handler。
- 后台会话使用 HttpOnly、SameSite=Strict Cookie 和 HMAC 签名。
- 前后台页面访问、后台登录及反馈管理操作会写入 `access_logs`。日志不保存明文 IP，而是使用 `ADMIN_SESSION_SECRET` 生成不可逆的稳定访客标识；禁止把密码、邮箱正文、表单内容或 Cookie 写入日志。
- 登录失败、跨站登录请求和未授权的后台修改/删除会标记为异常，并在下次成功进入后台时显示未读提醒；管理员确认后只更新 `acknowledged_at`，不删除原始审计记录。
- 前台昵称/邮箱 Cookie 仅用于在两个表单之间自动带入，不用于后台登录。
- 单次最多 3 个图片或视频；单个 15 MB，总计 30 MB。
