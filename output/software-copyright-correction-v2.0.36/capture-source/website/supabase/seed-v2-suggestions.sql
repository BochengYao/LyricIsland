-- Curated v2 feature cards. These are product-preview entries, not user testimonials.
insert into public.incentive_submissions (
  id, kind, nickname, email, title, body, status, reward_status, reviewer_note, created_at, updated_at
)
values
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a001', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '模块自由布局', '封面、歌词、播放控制、歌曲信息、进度与分割线，都能按习惯重新排列。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-06-18T09:20:00+08:00', '2026-06-18T09:20:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a002', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '直接拖动真实歌词岛', '从模块工具箱拖到真实歌词岛，支持 18 px 吸附、重排、保存与取消。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-06-22T14:05:00+08:00', '2026-06-22T14:05:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a003', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', 'A / C 双布局', 'A 与 C 模式各自保存布局；C 模式保持紧凑，鼠标悬停后再展开。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-06-27T20:40:00+08:00', '2026-06-27T20:40:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a004', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '锁定常用播放器', '自动跟随最近活跃播放器，也可以在设置里固定 Apple Music、酷狗等指定播放器。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-01T11:18:00+08:00', '2026-07-01T11:18:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a005', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '歌词源自动补位', '可选择首选歌词源；匹配不理想时，歌词岛会临时尝试其他已支持来源。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-03T16:32:00+08:00', '2026-07-03T16:32:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a006', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '长歌词不再省略', '长句会根据歌词时长横向滚动，让一句歌词完整经过，而不是被省略号切断。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-05T19:46:00+08:00', '2026-07-05T19:46:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a007', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '局部鼠标避让', '只有鼠标附近的歌词岛区域降低透明度，探测范围、光晕与频谱都可以调整。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-08T10:12:00+08:00', '2026-07-08T10:12:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a008', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '浅色、深色、跟随系统', '新版偏好设置可以选择界面主题，并集中调整布局、播放器、缓存与避让效果。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-10T13:54:00+08:00', '2026-07-10T13:54:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a009', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '同步歌词与歌词库翻译', '优先使用歌词源已经提供的逐行同步歌词和中文翻译，不额外生成机器翻译。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-12T08:25:00+08:00', '2026-07-12T08:25:00+08:00'),
  ('21b0d5ea-8d2e-4d1e-91aa-4ad6ee01a00a', 'feature', '歌词岛 v2', 'v2-preview@lyric-island.local', '本地 LRU 歌词缓存', '按歌曲管理本地歌词缓存，旧内容会按容量自动淘汰，也能在设置里调整缓存大小。', 'accepted', 'not_eligible', 'v2 产品预览卡片', '2026-07-13T21:06:00+08:00', '2026-07-13T21:06:00+08:00')
on conflict (id) do update
set title = excluded.title,
    body = excluded.body,
    status = excluded.status,
    reviewer_note = excluded.reviewer_note,
    updated_at = excluded.updated_at;
