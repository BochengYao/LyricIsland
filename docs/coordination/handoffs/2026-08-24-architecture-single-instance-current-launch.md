# Architecture 线程交接：current 启动后歌词岛不显示

- 日期：2026-08-24
- 受影响包：`D:\AppleMusicDesktopLyrics\publish\current`
- 当前版本：`v3.1.36 Beta` / 文件版本 `3.1.36.0`
- 当前 DLL SHA-256：`C5E2FD5CEFF9003CB7C979C033F70A0FB80E6DA9EBA9EDCC4ECA1078EEF785B7`

## 现象与已证实原因

用户报告新版歌词岛不显示。检查结果如下：

- 本地设置 `C:\Users\14731\AppData\Local\LyricHover\settings.json` 中 `IslandEnabled` 为 `true`，不是设置关闭。
- 手动启动 `publish/current/LyricHover.App.exe` 后约 5 秒内退出，退出码为 `0`，没有常驻 `LyricHover.App.exe` 进程。
- 通过 `Mutex.OpenExisting("LyricsIsland.DesktopLyrics.SingleInstance")` 打开单实例锁成功，但非阻塞 `WaitOne(0)` 返回 `false`；锁由另一个仍在运行的实例持有。

因此新包实际走的是“已有实例”路径，而不是渲染/歌词获取路径：它发激活信号后主动退出，用户误以为歌词岛没有显示。

## 相关实现

- `LyricHover.App/App.xaml.cs`
  - `App.OnStartup` 调用 `SingleInstanceGuard.TryAcquire("LyricsIsland.DesktopLyrics.SingleInstance")`。
  - `HasHandle == false` 时调用 `SignalExistingInstance()`，随后 `Shutdown()`。
- `LyricHover.Core/SingleInstanceGuard.cs`
  - 使用同名 `Mutex` 和 `EventWaitHandle` 实现单实例与激活通知。
  - Windows 不会永久保留已退出进程的 Mutex，因此“锁被占用”意味着仍存在持锁线程/进程，而不是普通的遗留锁文件。

## 当前边界

- 诊断时未从当前会话的常规进程枚举中发现 `LyricHover.App.exe`，持锁者可能来自旧发布路径、不同会话或不可枚举的后台/托盘实例。
- 不应为了绕过单实例约束而直接改用版本号相关的 Mutex 名称；这会导致两个歌词岛同时运行、重复注册热键/托盘图标和争用本地设置。
- 建议用户先在系统托盘退出旧歌词岛，或在任务管理器中结束旧实例后再启动 `publish/current`。

## Architecture 后续建议

1. 为单实例分支加入可审计日志：记录 `HasHandle=false`、激活信号发送结果、当前会话 ID、启动 EXE 路径和 PID；避免只有“静默退出”。
2. 在窗口创建前，提供可见的旧实例激活/失败反馈（例如一次性通知或诊断日志），区分“旧实例已唤醒”和“激活通道无人接收”。
3. 增加集成测试：首实例持锁时第二实例必须退出、发送激活事件；释放首实例后新实例必须可取得锁。
4. 若需要跨会话单实例，明确改为 `Global\` 命名并补齐权限策略；当前无前缀名称是会话内语义，不应在未定义 UX 的情况下改变。

## 不在本次范围内

- 未修改单实例架构或杀死未知持锁进程。
- 未重新打包版本；`current` 已是正确的 v3.1.36 产物。
