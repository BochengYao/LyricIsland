# 已确定决策

| 决策 | 约束 |
| --- | --- |
| 品牌 | 英文使用 `LyricHover`；中文可见品牌使用“歌词岛 | LyricHover”。 |
| 域名 | 继续使用 `lyric-island.top`，未获新任务不得迁移。 |
| 兼容 | Store 包名 `70643607.LyricIsland`、单实例名 `LyricsIsland.DesktopLyrics.SingleInstance`、旧数据目录 `LyricsIsland` 均为兼容边界。 |
| 分发 | Microsoft Store 为正式分发渠道；GitHub 变更不能被表述为已发布或已提交 Store。 |
| 发布证明 | 构建或推送不等于发布完成；必须分别验证产物、Store 状态和官网生产路由。 |
| 历史证据 | `evidence/softcopyright-v2.0.36` 为历史证据快照，不得覆盖或当作缓存清理。 |
| V3 版本 | 开发基线为 `3.0.0-Beta`；只有成功生成可测试候选才递增补丁位，首个任务栏歌词候选为 `v3.0.1 Beta`。 |
| 任务栏歌词 | Windows 11 使用独立非激活浮层，不注入 Explorer；与顶部歌词岛独立开关并可同时显示，显示屏沿用 `ScreenName`。 |
| Widgets 恢复 | 只按当前用户租约临时修改 `TaskbarDa`；关闭、正常退出或下次启动恢复残留租约时必须精确还原 absent/0/1 原状态，不写策略、不提权、不重启 Explorer。 |
| V3 首版发布 | 任务栏歌词通过实机验收后才可生成本地 `v3.0.1 Beta` 候选；本阶段不得上传或提交 Microsoft Store。 |

本页只记录已确认结论；待讨论事项写入任务交接记录，不写入本页。
