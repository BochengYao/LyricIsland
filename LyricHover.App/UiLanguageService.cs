using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LyricHover.App
{
    public sealed class LanguageOption
    {
        public LanguageOption(AppLanguagePreference value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public AppLanguagePreference Value { get; }

        public string DisplayName { get; }
    }

    public static class UiLanguageService
    {
        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
        {
            ["偏好设置"] = new[] { "偏好设置", "偏好設定", "Preferences", "設定" },
            ["语言"] = new[] { "语言", "語言", "Language", "言語" },
            ["跟随系统"] = new[] { "跟随系统", "跟隨系統", "System default", "システムに従う" },
            ["简体中文"] = new[] { "简体中文", "簡體中文", "Simplified Chinese", "簡体字中国語" },
            ["繁體中文"] = new[] { "繁體中文", "繁體中文", "Traditional Chinese", "繁体字中国語" },
            ["英语"] = new[] { "英语", "英語", "English", "英語" },
            ["日语"] = new[] { "日语", "日本語", "Japanese", "日本語" },
            ["外观"] = new[] { "外观", "外觀", "Appearance", "外観" },
            ["浅色模式"] = new[] { "浅色模式", "淺色模式", "Light mode", "ライトモード" },
            ["深色模式"] = new[] { "深色模式", "深色模式", "Dark mode", "ダークモード" },
            ["歌词显示"] = new[] { "歌词显示", "歌詞顯示", "Lyrics", "歌詞表示" },
            ["位置与状态"] = new[] { "位置与状态", "位置與狀態", "Position & status", "位置と状態" },
            ["缓存"] = new[] { "缓存", "快取", "Cache", "キャッシュ" },
            ["鼠标避让"] = new[] { "鼠标避让", "滑鼠避讓", "Mouse avoidance", "マウス回避" },
            ["快捷键"] = new[] { "快捷键", "快速鍵", "Shortcuts", "ショートカット" },
            ["模块布局"] = new[] { "模块布局", "模組佈局", "Module layout", "モジュールレイアウト" },
            ["关于"] = new[] { "关于", "關於", "About", "このアプリについて" },
            ["支持开发者"] = new[] { "支持开发者", "支持開發者", "Support development", "開発を支援" },
            ["首选歌词源"] = new[] { "首选歌词源", "偏好歌詞來源", "Preferred lyrics source", "優先する歌詞ソース" },
            ["行数"] = new[] { "行数", "行數", "Lines", "行数" },
            ["单行"] = new[] { "单行", "單行", "Single line", "1 行" },
            ["多行"] = new[] { "多行", "多行", "Multiple lines", "複数行" },
            ["翻译"] = new[] { "翻译", "翻譯", "Translation", "翻訳" },
            ["显示歌词库里的中文翻译"] = new[] { "显示歌词库里的中文翻译", "顯示歌詞庫中的中文翻譯", "Show Chinese lyric translations", "中国語の歌詞翻訳を表示" },
            ["显示器"] = new[] { "显示器", "顯示器", "Monitor", "モニター" },
            ["边缘位置"] = new[] { "边缘位置", "邊緣位置", "Edge position", "配置位置" },
            ["居中"] = new[] { "居中", "置中", "Center", "中央" },
            ["无播放后收起"] = new[] { "无播放后收起", "無播放後收起", "Hide when idle", "再生停止時に隠す" },
            ["展开停留"] = new[] { "展开停留", "展開停留", "Expanded duration", "展開の保持時間" },
            ["缓存容量"] = new[] { "缓存容量", "快取容量", "Cache size", "キャッシュ容量" },
            ["恢复默认"] = new[] { "恢复默认", "恢復預設", "Reset", "リセット" },
            ["光晕大小"] = new[] { "光晕大小", "光暈大小", "Aura size", "オーラのサイズ" },
            ["探测范围"] = new[] { "探测范围", "偵測範圍", "Detection range", "検出範囲" },
            ["光晕形状"] = new[] { "光晕形状", "光暈形狀", "Aura shape", "オーラの形状" },
            ["鼠标穿透"] = new[] { "鼠标穿透", "滑鼠穿透", "Click-through", "クリック透過" },
            ["实时预览"] = new[] { "实时预览", "即時預覽", "Live preview", "ライブプレビュー" },
            ["光晕频谱"] = new[] { "光晕频谱", "光暈頻譜", "Aura spectrum", "オーラスペクトラム" },
            ["中心"] = new[] { "中心", "中心", "Center", "中心" },
            ["边缘"] = new[] { "边缘", "邊緣", "Edge", "端" },
            ["过渡位置"] = new[] { "过渡位置", "過渡位置", "Transition position", "遷移位置" },
            ["中心透明"] = new[] { "中心透明", "中心透明", "Center transparency", "中心の透明度" },
            ["过渡透明"] = new[] { "过渡透明", "過渡透明", "Transition transparency", "遷移の透明度" },
            ["边缘透明"] = new[] { "边缘透明", "邊緣透明", "Edge transparency", "端の透明度" },
            ["编辑布局"] = new[] { "编辑布局", "編輯佈局", "Edit layout", "レイアウトを編集" },
            ["播放器"] = new[] { "播放器", "播放器", "Player", "プレーヤー" },
            ["自定义模块"] = new[] { "自定义模块", "自訂模組", "Custom modules", "カスタムモジュール" },
            ["歌词宽度"] = new[] { "歌词宽度", "歌詞寬度", "Lyrics width", "歌詞の幅" },
            ["透明度"] = new[] { "透明度", "透明度", "Opacity", "不透明度" },
            ["左右间距"] = new[] { "左右间距", "左右間距", "Horizontal spacing", "左右の間隔" },
            ["取消"] = new[] { "取消", "取消", "Cancel", "キャンセル" },
            ["应用"] = new[] { "应用", "套用", "Apply", "適用" },
            ["保存"] = new[] { "保存", "儲存", "Save", "保存" },
            ["退出"] = new[] { "退出", "結束", "Exit", "終了" },
            ["暂无播放内容"] = new[] { "暂无播放内容", "暫無播放內容", "Nothing playing", "再生中のコンテンツはありません" },
            ["歌词偏移 {0}s"] = new[] { "歌词偏移 {0}s", "歌詞偏移 {0}s", "Lyric offset {0}s", "歌詞オフセット {0}s" },
            ["未播放内容时，LyricHover将保持显示"] = new[] { "未播放内容时，LyricHover将保持显示", "沒有播放內容時，LyricHover 會保持顯示", "LyricHover stays visible while nothing is playing", "再生していない間も LyricHover を表示します" },
            ["LyricHover将在 {0} 秒后自动收起"] = new[] { "LyricHover将在 {0} 秒后自动收起", "LyricHover 將在 {0} 秒後自動收起", "LyricHover retracts automatically in {0} seconds", "LyricHover は {0} 秒後に自動で収納されます" },
            ["正在搜索同步歌词..."] = new[] { "正在搜索同步歌词...", "正在搜尋同步歌詞...", "Searching synced lyrics...", "同期歌詞を検索中..." },
            ["未找到同步歌词"] = new[] { "未找到同步歌词", "找不到同步歌詞", "No synced lyrics found", "同期歌詞が見つかりません" },
            ["读取播放状态失败"] = new[] { "读取播放状态失败", "讀取播放狀態失敗", "Could not read playback status", "再生状態を読み取れませんでした" }
            ,
            ["LyricHover | LYRIC HOVER - 偏好设置"] = new[] { "LyricHover | LYRIC HOVER - 偏好设置", "LyricHover | LYRIC HOVER - 偏好設定", "LyricHover | LYRIC HOVER - Preferences", "LyricHover | LYRIC HOVER - 設定" },
            ["自动选择"] = new[] { "自动选择", "自動選擇", "Automatic", "自動選択" },
            ["翻译模式下仅支持多行模式"] = new[] { "翻译模式下仅支持多行模式", "翻譯模式下僅支援多行模式", "Translation mode requires multiple lines", "翻訳モードでは複数行表示が必要です" },
            ["永不"] = new[] { "永不", "永不", "Never", "なし" },
            ["秒"] = new[] { "秒", "秒", "s", "秒" },
            ["水平积木"] = new[] { "水平积木", "水平積木", "Horizontal blocks", "横並びブロック" },
            ["自动折叠"] = new[] { "自动折叠", "自動摺疊", "Auto collapse", "自動折りたたみ" },
            ["所有模块像积木一样横向排列，始终完整显示。"] = new[] { "所有模块像积木一样横向排列，始终完整显示。", "所有模組如積木般橫向排列，始終完整顯示。", "Keep every module in one horizontal row.", "すべてのモジュールを横一列に常時表示します。" },
            ["平时保持紧凑，按住 "] = new[] { "平时保持紧凑，按住 ", "平時保持緊湊，按住 ", "Stay compact; hold ", "通常はコンパクトに表示。 " },
            ["即展开，松开后自动折叠。"] = new[] { "即展开，松开后自动折叠。", "即可展開，放開後自動摺疊。", " to expand, then release to collapse.", " を押すと展開し、離すと折りたたみます。" },
            ["自动选择会跟随最近活跃的播放器"] = new[] { "自动选择会跟随最近活跃的播放器", "自動選擇會跟隨最近使用中的播放器", "Automatically follows the most recently active player", "直近でアクティブなプレーヤーに自動追従します" },
            ["未检测到，启动播放器后生效"] = new[] { "未检测到，启动播放器后生效", "未偵測到，啟動播放器後生效", "Not detected; takes effect when the player starts", "検出されていません。プレーヤー起動後に有効になります" },
            ["优先选择"] = new[] { "优先选择", "優先選擇", "Prefer", "優先" },
            ["注：网易云音乐由于接口限制无法实时同步歌曲进度（播放器内拖动进度条无法同步）"] = new[] { "注：网易云音乐由于接口限制无法实时同步歌曲进度（播放器内拖动进度条无法同步）", "註：網易雲音樂受介面限制，無法即時同步歌曲進度（播放器內拖動進度條無法同步）", "Note: NetEase Cloud Music cannot sync seeking in real time because of its API limits.", "注：NetEase Cloud Music は API の制限により、シーク位置をリアルタイム同期できません。" },
            ["分割线样式"] = new[] { "分割线样式", "分隔線樣式", "Divider style", "区切り線のスタイル" },
            ["临时启用交互"] = new[] { "临时启用交互", "暫時啟用互動", "Temporary interaction", "一時操作" },
            ["歌词提前 0.5 秒"] = new[] { "歌词提前 0.5 秒", "歌詞提前 0.5 秒", "Lyrics 0.5 s earlier", "歌詞を 0.5 秒早める" },
            ["歌词延后 0.5 秒"] = new[] { "歌词延后 0.5 秒", "歌詞延後 0.5 秒", "Lyrics 0.5 s later", "歌詞を 0.5 秒遅らせる" },
            ["重置歌词偏移"] = new[] { "重置歌词偏移", "重設歌詞偏移", "Reset lyric offset", "歌詞のオフセットをリセット" },
            ["单击后直接按下新的快捷键"] = new[] { "单击后直接按下新的快捷键", "點擊後直接按下新的快速鍵", "Click, then press a new shortcut", "クリックしてから新しいショートカットを押してください" },
            ["单击后，按下新的快捷键组合"] = new[] { "单击后，按下新的快捷键组合", "點擊後，按下新的快速鍵組合", "Click, then press a new key combination", "クリックしてから新しいキーの組み合わせを押してください" },
            ["歌词光影预览"] = new[] { "歌词光影预览", "歌詞光影預覽", "Lyrics aura preview", "歌詞オーラのプレビュー" },
            ["鼠标附近会变淡，底下文字应能看清"] = new[] { "鼠标附近会变淡，底下文字应能看清", "滑鼠附近會變淡，底下文字應能看清", "The area under the pointer fades so text beneath stays readable", "ポインター付近を薄くして、下の文字を読みやすくします" },
            ["透明避让时允许左键点到下方窗口"] = new[] { "透明避让时允许左键点到下方窗口", "透明避讓時允許左鍵點到下方視窗", "Let left clicks pass through while faded", "薄く表示している間、左クリックを背面のウィンドウに通します" },
            ["拖动到上方LyricHover指定位置"] = new[] { "拖动到上方LyricHover指定位置", "拖動到上方 LyricHover 指定位置", "Drag to the highlighted LyricHover slot above", "上の LyricHover の指定位置へドラッグします" },
            ["拖上岛内插入；岛内拖动排序；拖出岛外删除"] = new[] { "拖上岛内插入；岛内拖动排序；拖出岛外删除", "拖到島內插入；島內拖動排序；拖出島外刪除", "Drop into the island to add, drag inside to reorder, drag out to remove", "島にドロップして追加、島内で並べ替え、外へドラッグして削除します" },
            ["版本号："] = new[] { "版本号：", "版本號：", "Version:", "バージョン:" },
            ["作者："] = new[] { "作者：", "作者：", "Author:", "作者:" },
            ["桌面同步歌词与模块化媒体控制"] = new[] { "桌面同步歌词与模块化媒体控制", "桌面同步歌詞與模組化媒體控制", "Desktop synced lyrics and modular media controls", "デスクトップ同期歌詞とモジュール式メディア操作" },
            ["官方网站"] = new[] { "官方网站", "官方網站", "Official website", "公式サイト" },
            ["访问LyricHover官方网站"] = new[] { "访问LyricHover官方网站", "造訪 LyricHover 官方網站", "Visit the LyricHover website", "LyricHover の公式サイトを開く" },
            ["打开官网"] = new[] { "打开官网", "開啟官網", "Open website", "公式サイトを開く" },
            ["GitHub 项目主页"] = new[] { "GitHub 项目主页", "GitHub 專案首頁", "GitHub project", "GitHub プロジェクト" },
            ["查看源代码、版本记录和问题反馈"] = new[] { "查看源代码、版本记录和问题反馈", "查看原始碼、版本記錄與問題回饋", "View source, release notes, and issue tracking", "ソース、更新履歴、課題を確認" },
            ["打开 GitHub"] = new[] { "打开 GitHub", "開啟 GitHub", "Open GitHub", "GitHub を開く" },
            ["请右键LyricHover打开设置"] = new[] { "请右键LyricHover打开设置", "請右鍵點擊 LyricHover 開啟設定", "Right-click LyricHover to open Settings", "LyricHover を右クリックして設定を開きます" },
            ["从菜单中选择“偏好设置”"] = new[] { "从菜单中选择“偏好设置”", "從選單中選擇「偏好設定」", "Choose Preferences from the menu", "メニューから「設定」を選択します" },
            ["教学模式"] = new[] { "教学模式", "教學模式", "Tutorial", "チュートリアル" },
            ["重新显示首次使用教学流程"] = new[] { "重新显示首次使用教学流程", "重新顯示首次使用教學流程", "Show the first-use tutorial again", "初回チュートリアルをもう一度表示" },
            ["重新开始教学"] = new[] { "重新开始教学", "重新開始教學", "Restart tutorial", "チュートリアルを再開" },
            ["即将开始教学模式"] = new[] { "即将开始教学模式", "即將開始教學模式", "Tutorial is about to begin", "チュートリアルを始めます" },
            ["单击LyricHover继续"] = new[] { "单击LyricHover继续", "點擊 LyricHover 繼續", "Click LyricHover to continue", "LyricHover をクリックして続けます" },
            ["退出教学模式"] = new[] { "退出教学模式", "結束教學模式", "Exit tutorial", "チュートリアルを終了" },
            ["请点击左侧“模块布局”"] = new[] { "请点击左侧“模块布局”", "請點擊左側「模組佈局」", "Select Module layout on the left", "左側の「モジュールレイアウト」を選択" },
            ["真棒！"] = new[] { "真棒！", "太棒了！", "Great!", "いいですね！" },
            ["拖动LyricHover可左右移动"] = new[] { "拖动LyricHover可左右移动", "拖曳 LyricHover 可左右移動", "Drag LyricHover left or right", "LyricHover を左右にドラッグできます" },
            ["在“设置-位置”里也可以调整位置"] = new[] { "在“设置-位置”里也可以调整位置", "也可在「設定 - 位置與狀態」調整位置", "You can also adjust it in Settings > Position & status", "設定の「位置と状態」でも調整できます" },
            ["接下来演示鼠标避让"] = new[] { "接下来演示鼠标避让", "接下來示範滑鼠避讓", "Next, mouse avoidance", "次はマウス回避です" },
            ["请把鼠标移动到岛上"] = new[] { "请把鼠标移动到岛上", "請將滑鼠移到島上", "Move the pointer onto the island", "マウスを島の上に移動してください" },
            ["该功能可方便看到岛下内容"] = new[] { "该功能可方便看到岛下内容", "此功能讓您能輕鬆查看島下內容", "This lets you see what's beneath the island", "島の下にある内容を確認しやすくなります" },
            ["无需频繁拖动LyricHover，助你高效工作"] = new[] { "无需频繁拖动LyricHover，助你高效工作", "無需頻繁拖曳 LyricHover，讓工作更有效率", "No frequent dragging needed, so you can stay focused", "何度もドラッグせず、作業に集中できます" },
            ["可以透过岛直接左键点击控制岛下内容"] = new[] { "可以透过岛直接左键点击控制岛下内容", "可直接穿透島左鍵點擊下方內容", "Click through the island to use what's underneath", "島を通して下の内容を左クリックできます" },
            ["新版本增加了音乐控制功能"] = new[] { "新版本增加了音乐控制功能", "新版本加入了音樂控制功能", "Music controls are now available", "音楽コントロールが追加されました" },
            ["按下{0}可暂时关闭鼠标避让来点击控制按钮"] = new[] { "按下{0}可暂时关闭鼠标避让来点击控制按钮", "按下 {0} 可暫時關閉滑鼠避讓並點擊控制按鈕", "Hold {0} to pause mouse avoidance and use the controls", "{0} を押すとマウス回避を一時停止して操作できます" },
            ["来试试看！"] = new[] { "来试试看！", "來試試看！", "Try it now!", "試してみましょう！" },
            ["快捷键可在设置中修改"] = new[] { "快捷键可在设置中修改", "快速鍵可在設定中修改", "You can change shortcuts in Settings", "ショートカットは設定で変更できます" },
            ["现在我们来体验新功能——自定义模块"] = new[] { "现在我们来体验新功能——自定义模块", "現在來試試新功能：自訂模組", "Now let's try custom modules", "次はカスタムモジュールを試しましょう" },
            ["现在右键岛打开设置"] = new[] { "现在右键岛打开设置", "現在右鍵點擊島來開啟設定", "Right-click the island to open Settings", "島を右クリックして設定を開きます" },
            ["您可以直接拖动“自定义模块”部分的内容到岛里"] = new[] { "您可以直接拖动“自定义模块”部分的内容到岛里", "可將「自訂模組」中的內容直接拖入島內", "Drag items from Custom modules directly into the island", "「カスタムモジュール」から島へ直接ドラッグできます" },
            ["进行自定义布局"] = new[] { "进行自定义布局", "進行自訂佈局", "to customize your layout", "レイアウトをカスタマイズします" },
            ["所有模块可直接鼠标拖入岛添加、拖动排序、拖出岛删除"] = new[] { "所有模块可直接鼠标拖入岛添加、拖动排序、拖出岛删除", "所有模組都可拖入島內新增、在島內排序、拖出島外刪除", "Drag modules in to add, reorder inside, or drag out to remove", "モジュールを島へ入れて追加、島内で並べ替え、外へ出して削除できます" },
            ["同一模块可拖入多个"] = new[] { "同一模块可拖入多个", "同一模組可加入多個", "You can add a module more than once", "同じモジュールを複数追加できます" },
            ["来拖动试试看吧"] = new[] { "来拖动试试看吧", "拖曳試試看吧", "Give it a try", "ドラッグしてみましょう" },
            ["下一步"] = new[] { "下一步", "下一步", "Next", "次へ" },
            ["现在我们来看看两种布局模式"] = new[] { "现在我们来看看两种布局模式", "現在來看看兩種佈局模式", "Let's look at the two layout modes", "2 種類のレイアウトを見てみましょう" },
            ["你设置的布局一字排开，信息一眼可见"] = new[] { "你设置的布局一字排开，信息一眼可见", "您設定的佈局會橫向排列，資訊一目了然", "Your modules line up in one row for an easy overview", "設定したモジュールが横一列に並び、ひと目で確認できます" },
            ["刚刚你设置的模块布局已经保存在了水平积木"] = new[] { "刚刚你设置的模块布局已经保存在了水平积木", "剛才設定的模組佈局已儲存在水平積木模式", "Your module layout has been saved to Horizontal blocks", "設定したモジュール配置は横並びブロックに保存されました" },
            ["自动折叠模式"] = new[] { "自动折叠模式", "自動摺疊模式", "Auto-collapse mode", "自動折りたたみモード" },
            ["按住 {0} 即时展开，松开后自动折叠"] = new[] { "按住 {0} 即时展开，松开后自动折叠", "按住 {0} 即時展開，放開後自動摺疊", "Hold {0} to expand; release to collapse", "{0} を押すと展開し、離すと折りたたまれます" },
            ["平时保持紧凑，只显示核心模块"] = new[] { "平时保持紧凑，只显示核心模块", "平時保持精簡，只顯示核心模組", "It stays compact and shows only core modules", "普段はコンパクトに、主要なモジュールだけを表示します" },
            ["按住 {0} 后显示你的完整模块布局"] = new[] { "按住 {0} 后显示你的完整模块布局", "按住 {0} 後顯示完整模組佈局", "Hold {0} to show your full module layout", "{0} を押すと完全なモジュールレイアウトを表示します" },
            ["与水平积木布局独立"] = new[] { "与水平积木布局独立", "與水平積木佈局獨立", "Independent from Horizontal blocks", "横並びブロックとは別に設定できます" },
            ["🎉教学模式已结束！快去体验吧！！"] = new[] { "🎉教学模式已结束！快去体验吧！！", "🎉教學模式已結束！快去體驗吧！", "🎉 Tutorial complete. Explore LyricHover!", "🎉 チュートリアルは完了です。LyricHover を楽しんでください！" },
            ["v2.0 更新内容"] = new[] { "v2.0 更新内容", "v2.0 更新內容", "What's new in v2.0", "v2.0 の更新内容" },
            ["新增水平积木与自动折叠两种LyricHover布局"] = new[] { "新增水平积木与自动折叠两种LyricHover布局", "新增水平積木與自動摺疊兩種 LyricHover 佈局", "New horizontal-block and auto-collapse LyricHover layouts", "横並びブロックと自動折りたたみの LyricHover レイアウトを追加" },
            ["支持拖动添加、排序与删除封面、歌词、控制、信息、进度和分割线模块"] = new[] { "支持拖动添加、排序与删除封面、歌词、控制、信息、进度和分割线模块", "支援拖動新增、排序與刪除封面、歌詞、控制、資訊、進度與分隔線模組", "Drag to add, arrange, or remove album art, lyrics, controls, info, progress, and dividers", "アートワーク、歌詞、操作、情報、進捗、区切り線をドラッグで追加・並べ替え・削除できます" },
            ["支持 Apple Music、QQ 音乐、网易云、酷狗、酷我、Spotify 等 SMTC 播放器"] = new[] { "支持 Apple Music、QQ 音乐、网易云、酷狗、酷我、Spotify 等 SMTC 播放器", "支援 Apple Music、QQ 音樂、網易雲、酷狗、酷我、Spotify 等 SMTC 播放器", "Supports Apple Music, QQ Music, NetEase Cloud Music, KuGou, KuWo, Spotify, and more", "Apple Music、QQ 音楽、NetEase Cloud Music、KuGou、KuWo、Spotify などに対応" },
            ["新增同步歌词缓存、播放器锁定、鼠标避让、快捷键和主题设置"] = new[] { "新增同步歌词缓存、播放器锁定、鼠标避让、快捷键和主题设置", "新增同步歌詞快取、播放器鎖定、滑鼠避讓、快速鍵與主題設定", "Synced-lyrics cache, player lock, mouse avoidance, shortcuts, and themes", "同期歌詞キャッシュ、プレーヤー固定、マウス回避、ショートカット、テーマを追加" },
            ["© 2026 大丞子 · 感谢参与 v2.0 测试"] = new[] { "© 2026 大丞子 · 感谢参与 v2.0 测试", "© 2026 大丞子 · 感謝參與 v2.0 測試", "© 2026 大丞子 · Thank you for testing v2.0", "© 2026 大丞子 · v2.0 テストへのご参加ありがとうございます" },
            ["歌词"] = new[] { "歌词", "歌詞", "Lyrics", "歌詞" },
            ["封面"] = new[] { "封面", "封面", "Art", "アート" },
            ["播放"] = new[] { "播放", "播放", "Play", "再生" },
            ["信息"] = new[] { "信息", "資訊", "Info", "情報" },
            ["进度"] = new[] { "进度", "進度", "Progress", "進捗" },
            ["分割"] = new[] { "分割", "分隔", "Split", "分割" },
            ["查看我的支持者徽章"] = new[] { "查看我的支持者徽章", "查看我的支持者徽章", "View badge", "バッジを見る" },
            ["缓存用于保存已经下载过的同步歌词，下次播放同一首歌时可以直接读取，减少等待和重复请求"] = new[] { "缓存用于保存已经下载过的同步歌词，下次播放同一首歌时可以直接读取，减少等待和重复请求", "快取用於保存已下載的同步歌詞，下次播放同一首歌時可直接讀取，減少等待和重複請求", "Cache keeps downloaded synced lyrics ready for the next time you play a song.", "キャッシュはダウンロード済みの同期歌詞を保存し、次回の再生をすばやくします。" },
            ["按每首约10KB计算:实际数量会随歌词长度变化\n1 MB≈100首\n500 MB≈50,000首\n1000 MB≈100,000首"] = new[] { "按每首约10KB计算:实际数量会随歌词长度变化\n1 MB≈100首\n500 MB≈50,000首\n1000 MB≈100,000首", "每首約以 10KB 計算：實際數量會依歌詞長度而變化\n1 MB≈100 首\n500 MB≈50,000 首\n1000 MB≈100,000 首", "Estimated at 10 KB per song; actual capacity varies with lyric length.\n1 MB≈100 songs\n500 MB≈50,000 songs\n1000 MB≈100,000 songs", "1 曲あたり約 10 KB の目安です。実際の件数は歌詞の長さで変わります。\n1 MB≈100 曲\n500 MB≈50,000 曲\n1000 MB≈100,000 曲" },
            ["写入歌词或修改容量后会检查总大小\n超过上限时优先删除最久未使用的歌词，直到低于容量上限"] = new[] { "写入歌词或修改容量后会检查总大小\n超过上限时优先删除最久未使用的歌词，直到低于容量上限", "寫入歌詞或修改容量後會檢查總大小\n超過上限時優先刪除最久未使用的歌詞，直到低於容量上限", "The cache size is checked after writing lyrics or changing the limit.\nWhen it exceeds the limit, least-used lyrics are removed first.", "歌詞の保存や容量変更後に合計サイズを確認します。\n上限を超えた場合、最も長く使われていない歌詞から削除します。" },
            ["LyricHover主体功能始终免费。您可以通过免费方式支持项目，您也可以升级Pro来支持开发者。"] = new[] { "LyricHover主体功能始终免费。您可以通过免费方式支持项目，您也可以升级Pro来支持开发者。", "LyricHover 主體功能始終免費。您可以透過免費方式支持專案，也可以升級 Pro 來支持開發者。", "LyricHover's core features are always free. You can support the project for free or upgrade to Pro.", "LyricHover の基本機能はいつでも無料です。無料で応援するか、Pro へアップグレードできます。" },
            ["这对我们真的很重要，谢谢！❤️"] = new[] { "这对我们真的很重要，谢谢！❤️", "這對我們真的很重要，謝謝！❤️", "It truly means a lot. Thank you! ❤️", "本当に大きな励みになります。ありがとうございます！❤️" },
            ["免费支持"] = new[] { "免费支持", "免費支持", "Free support", "無料で応援" },
            ["评价与撰写评价"] = new[] { "评价与撰写评价", "評分與撰寫評論", "Rate and review", "評価・レビュー" },
            ["你的想法会让LyricHover更好"] = new[] { "你的想法会让LyricHover更好", "你的想法會讓 LyricHover 更好", "Your feedback makes LyricHover better", "あなたの意見が LyricHover をより良くします" },
            ["去评价  >"] = new[] { "去评价  >", "去評分  >", "Rate it  >", "評価する  >" },
            ["去评价"] = new[] { "去评价", "去評分", "Rate", "評価" },
            ["分享给身边的朋友"] = new[] { "分享给身边的朋友", "分享給身邊的朋友", "Share with friends", "友だちにシェア" },
            ["让更多人发现并使用LyricHover"] = new[] { "让更多人发现并使用LyricHover", "讓更多人發現並使用 LyricHover", "Help more people discover LyricHover", "より多くの人に LyricHover を届けます" },
            ["立即分享  >"] = new[] { "立即分享  >", "立即分享  >", "Share now  >", "今すぐ共有  >" },
            ["立即分享"] = new[] { "立即分享", "立即分享", "Share", "共有" },
            ["在 GitHub 上点 Star"] = new[] { "在 GitHub 上点 Star", "在 GitHub 上點 Star", "Star on GitHub", "GitHub で Star" },
            ["Star 越多，越多人看到LyricHover"] = new[] { "Star 越多，越多人看到LyricHover", "Star 越多，越多人看到 LyricHover", "More stars help more people find LyricHover", "Star が増えるほど、LyricHover を見つける人が増えます" },
            ["去 GitHub  >"] = new[] { "去 GitHub  >", "前往 GitHub  >", "Go to GitHub  >", "GitHub へ  >" },
            ["去 GitHub"] = new[] { "去 GitHub", "前往 GitHub", "GitHub", "GitHub" },
            ["意见反馈"] = new[] { "意见反馈", "意見回饋", "Feedback", "フィードバック" },
            ["提交问题与功能建议"] = new[] { "提交问题与功能建议", "提交問題與功能建議", "Send bugs and feature ideas", "不具合や機能の提案を送る" },
            ["去反馈  >"] = new[] { "去反馈  >", "前往回饋  >", "Send feedback  >", "フィードバック  >" },
            ["去反馈"] = new[] { "去反馈", "前往回饋", "Feedback", "フィードバック" },
            ["通过 Microsoft Store 升级 Pro，支持LyricHover持续开发，并解锁更多专属权益。"] = new[] { "通过 Microsoft Store 升级 Pro，支持LyricHover持续开发，并解锁更多专属权益。", "透過 Microsoft Store 升級 Pro，支持 LyricHover 持續開發，並解鎖更多專屬權益。", "Upgrade to Pro in Microsoft Store to support LyricHover and unlock more benefits.", "Microsoft Store で Pro にアップグレードして LyricHover を応援し、さらに多くの特典を利用できます。" },
            ["抢先体验"] = new[] { "抢先体验", "搶先體驗", "Early access", "先行体験" },
            ["优先体验新功能。"] = new[] { "优先体验新功能。", "優先體驗新功能。", "Try new features first.", "新機能を先行利用。" },
            ["支持者徽章"] = new[] { "支持者徽章", "支持者徽章", "Supporter badge", "支援バッジ" },
            ["永久展示支持者身份。"] = new[] { "永久展示支持者身份。", "永久展示支持者身分。", "Always visible.", "サポーターとして永久表示。" },
            ["永久有效"] = new[] { "永久有效", "永久有效", "Lifetime", "永久" },
            ["一次购买，权益长期有效。"] = new[] { "一次购买，权益长期有效。", "一次購買，權益長期有效。", "One payment. Yours for life.", "一度の購入で永久利用。" },
            ["升级 Pro · ¥7"] = new[] { "升级 Pro · ¥7", "升級 Pro · ¥7", "Upgrade to Pro · ¥7", "Pro にアップグレード · ¥7" },
            ["输入徽章署名后按 Enter"] = new[] { "输入徽章署名后按 Enter", "輸入徽章署名後按 Enter", "Enter your badge name, then press Enter", "バッジ名を入力して Enter を押してください" },
            ["2–18 个字符；输入后按 Enter 确认，提交后不可修改"] = new[] { "2–18 个字符；输入后按 Enter 确认，提交后不可修改", "2–18 個字元；輸入後按 Enter 確認，提交後不可修改", "2–18 characters. Press Enter to confirm; this cannot be changed.", "2～18 文字。入力後に Enter で確定し、以後は変更できません。" },
            ["Pro 支持计划"] = new[] { "Pro 支持计划", "Pro 支持計畫", "Pro support plan", "Pro サポートプラン" },
            ["Pro 支持计划：已加入"] = new[] { "Pro 支持计划：已加入", "Pro 支持計畫：已加入", "Pro support plan: joined", "Pro サポートプラン：参加済み" },
            ["已自动激活 Pro，感谢你曾经购买并支持 LYRIC HOVER。"] = new[] { "已自动激活 Pro，感谢你曾经购买并支持 LYRIC HOVER。", "已自動啟用 Pro，感謝你曾經購買並支持 LYRIC HOVER。", "Pro is active. Thank you for supporting LYRIC HOVER.", "Pro が有効になりました。LYRIC HOVER を応援いただきありがとうございます。" },
            ["感谢你对LyricHover的支持。你将抢先体验所有新功能，支持者徽章已发放，点击右侧按钮即可查看。更多专属权益，正在陆续加入。"] = new[] { "感谢你对LyricHover的支持。你将抢先体验所有新功能，支持者徽章已发放，点击右侧按钮即可查看。更多专属权益，正在陆续加入。", "感謝你對 LyricHover 的支持。你將搶先體驗所有新功能，支持者徽章已發放，點擊右側按鈕即可查看。更多專屬權益正在陸續加入。", "Thank you for supporting LyricHover. Enjoy early access to new features; your supporter badge is ready to view.", "LyricHover を応援いただきありがとうございます。新機能を先行利用でき、サポーターバッジも確認できます。" },
            ["正在验证 Pro 状态…"] = new[] { "正在验证 Pro 状态…", "正在驗證 Pro 狀態…", "Checking Pro status…", "Pro の状態を確認中…" },
            ["购买成功，感谢您支持 LYRIC HOVER！"] = new[] { "购买成功，感谢您支持 LYRIC HOVER！", "購買成功，感謝您支持 LYRIC HOVER！", "Purchase complete. Thank you for supporting LYRIC HOVER!", "購入が完了しました。LYRIC HOVER を応援いただきありがとうございます！" },
            ["购买已取消。"] = new[] { "购买已取消。", "購買已取消。", "Purchase cancelled.", "購入をキャンセルしました。" },
            ["网络连接异常，暂时无法完成购买。"] = new[] { "网络连接异常，暂时无法完成购买。", "網路連線異常，暫時無法完成購買。", "Network connection issue. Purchase could not be completed.", "ネットワーク接続の問題により、購入を完了できませんでした。" },
            ["暂时无法保存徽章署名，请稍后重试。"] = new[] { "暂时无法保存徽章署名，请稍后重试。", "暫時無法儲存徽章署名，請稍後重試。", "Couldn't save the badge name. Please try again.", "バッジ名を保存できませんでした。もう一度お試しください。" },
            ["徽章署名已永久刻印。"] = new[] { "徽章署名已永久刻印。", "徽章署名已永久刻印。", "Your badge name has been permanently engraved.", "バッジ名を永久に刻印しました。" },
            ["LyricHover LYRIC HOVER · Windows 桌面歌词伴侣\n"] = new[] { "LyricHover LYRIC HOVER · Windows 桌面歌词伴侣\n", "LyricHover LYRIC HOVER · Windows 桌面歌詞伴侶\n", "LyricHover · desktop lyrics for Windows\n", "LyricHover · Windows 用デスクトップ歌詞\n" },
            ["Microsoft Store 已返回购买记录，但暂时无法验证 Pro 权益。"] = new[] { "Microsoft Store 已返回购买记录，但暂时无法验证 Pro 权益。", "Microsoft Store 已返回購買記錄，但暫時無法驗證 Pro 權益。", "Microsoft Store found your purchase, but Pro can't be verified yet.", "Microsoft Store で購入記録を確認しましたが、Pro をまだ認証できません。" },
            ["Microsoft Store 应用链接已复制，可以分享给朋友了。"] = new[] { "Microsoft Store 应用链接已复制，可以分享给朋友了。", "Microsoft Store 應用程式連結已複製，可以分享給朋友了。", "The Microsoft Store link is copied and ready to share.", "Microsoft Store のリンクをコピーしました。共有できます。" },
            ["Microsoft Store 暂时无法完成购买，请稍后重试。"] = new[] { "Microsoft Store 暂时无法完成购买，请稍后重试。", "Microsoft Store 暫時無法完成購買，請稍後重試。", "Microsoft Store couldn't complete the purchase. Try again later.", "Microsoft Store で購入を完了できません。後でもう一度お試しください。" },
            ["Pro 商品尚未在 Microsoft Store 中可用，请稍后重试。"] = new[] { "Pro 商品尚未在 Microsoft Store 中可用，请稍后重试。", "Pro 商品尚未在 Microsoft Store 中可用，請稍後重試。", "The Pro product isn't available in Microsoft Store yet. Try again later.", "Pro 商品はまだ Microsoft Store で利用できません。後でもう一度お試しください。" },
            ["QQ 音乐"] = new[] { "QQ 音乐", "QQ 音樂", "QQ Music", "QQ Music" },
            ["网易云音乐"] = new[] { "网易云音乐", "網易雲音樂", "NetEase Cloud Music", "NetEase Cloud Music" },
            ["酷狗音乐"] = new[] { "酷狗音乐", "酷狗音樂", "KuGou Music", "KuGou Music" },
            ["酷我音乐"] = new[] { "酷我音乐", "酷我音樂", "KuWo Music", "KuWo Music" },
            ["选择水平积木布局"] = new[] { "选择水平积木布局", "選擇水平積木佈局", "Select Horizontal blocks layout", "横並びブロックを選択" },
            ["选择自动折叠布局"] = new[] { "选择自动折叠布局", "選擇自動摺疊佈局", "Select Auto collapse layout", "自動折りたたみを選択" },
            ["确认永久刻印"] = new[] { "确认永久刻印", "確認永久刻印", "Confirm permanent engraving", "永久刻印を確認" },
            ["LyricHover Pro 支持者徽章"] = new[] { "LyricHover Pro 支持者徽章", "LyricHover Pro 支持者徽章", "LyricHover Pro supporter badge", "LyricHover Pro サポーターバッジ" },
            ["署名将永久写入徽章背面"] = new[] { "署名将永久写入徽章背面", "署名將永久寫入徽章背面", "Your name will be permanently engraved on the badge back", "署名はバッジの裏面に永久刻印されます" },
            ["它会与 Microsoft Store 获取日期一同保存，用于展示你的支持者身份。提交后不可修改。"] = new[] { "它会与 Microsoft Store 获取日期一同保存，用于展示你的支持者身份。提交后不可修改。", "它會與 Microsoft Store 取得日期一同儲存，用於展示您的支持者身分。送出後無法修改。", "It is saved with your Microsoft Store acquisition date to show your supporter identity. It cannot be changed after submission.", "Microsoft Store の取得日とともに保存され、サポーターであることを表示します。送信後は変更できません。" },
            ["确认刻印"] = new[] { "确认刻印", "確認刻印", "Confirm engraving", "刻印を確定" },
            ["拖拽旋转 · 滚轮放大 / 缩小"] = new[] { "拖拽旋转 · 滚轮放大 / 缩小", "拖曳旋轉 · 滾輪放大 / 縮小", "Drag to rotate · scroll to zoom", "ドラッグで回転 · ホイールで拡大・縮小" },
            ["可拖拽旋转的LyricHover Pro 支持者徽章"] = new[] { "可拖拽旋转的LyricHover Pro 支持者徽章", "可拖曳旋轉的 LyricHover Pro 支持者徽章", "Draggable LyricHover Pro supporter badge", "ドラッグで回転できる LyricHover Pro サポーターバッジ" },
            ["拖拽可旋转徽章；上下拖拽方向已反转。滚动鼠标滚轮可放大或缩小徽章。"] = new[] { "拖拽可旋转徽章；上下拖拽方向已反转。滚动鼠标滚轮可放大或缩小徽章。", "拖曳可旋轉徽章；上下拖曳方向已反轉。滾動滑鼠滾輪可放大或縮小徽章。", "Drag to rotate the badge; vertical drag is inverted. Scroll to zoom the badge.", "ドラッグでバッジを回転します。上下のドラッグ方向は反転しており、ホイールで拡大・縮小できます。" },
            ["LyricHover Pro 支持计划"] = new[] { "LyricHover Pro 支持计划", "LyricHover Pro 支持計畫", "LyricHover Pro support plan", "LyricHover Pro サポートプラン" },
            ["感谢你支持LyricHover，你已获得专属支持者徽章。"] = new[] { "感谢你支持LyricHover，你已获得专属支持者徽章。", "感謝您支持 LyricHover，您已獲得專屬支持者徽章。", "Thank you for supporting LyricHover. Your supporter badge is ready.", "LyricHover を応援いただきありがとうございます。サポーターバッジをご用意しました。" },
            ["找不到LyricHover Pro 支持者徽章模型资源。"] = new[] { "找不到LyricHover Pro 支持者徽章模型资源。", "找不到 LyricHover Pro 支持者徽章模型資源。", "The LyricHover Pro supporter badge model could not be found.", "LyricHover Pro サポーターバッジのモデルが見つかりません。" },
            ["酷狗"] = new[] { "酷狗", "酷狗", "KuGou Music", "KuGou Music" },
            ["网易云"] = new[] { "网易云", "網易雲", "NetEase Cloud Music", "NetEase Cloud Music" },
            ["当前 Microsoft 账号已经拥有 LYRIC HOVER Pro。"] = new[] { "当前 Microsoft 账号已经拥有 LYRIC HOVER Pro。", "目前的 Microsoft 帳戶已擁有 LYRIC HOVER Pro。", "This Microsoft account already owns LYRIC HOVER Pro.", "この Microsoft アカウントはすでに LYRIC HOVER Pro を所有しています。" },
            ["当前为上次成功验证的 Pro 状态，将在联网后自动更新。"] = new[] { "当前为上次成功验证的 Pro 状态，将在联网后自动更新。", "目前為上次成功驗證的 Pro 狀態，連網後將自動更新。", "Showing the last verified Pro status. It updates when you're online.", "前回確認した Pro 状態を表示しています。オンライン時に更新されます。" },
            ["购买已完成，但暂时无法验证 Pro 权益，请稍后重新打开此页面。"] = new[] { "购买已完成，但暂时无法验证 Pro 权益，请稍后重新打开此页面。", "購買已完成，但暫時無法驗證 Pro 權益，請稍後重新開啟此頁面。", "Purchase completed, but Pro can't be verified yet. Reopen this page later.", "購入は完了しましたが、Pro をまだ認証できません。後でこのページを開き直してください。" },
            ["徽章署名至少需要 2 个字符。"] = new[] { "徽章署名至少需要 2 个字符。", "徽章署名至少需要 2 個字元。", "Your badge name needs at least 2 characters.", "バッジ名は 2 文字以上で入力してください。" },
            ["请先完成 Microsoft Store Pro 权益验证，再提交徽章署名。"] = new[] { "请先完成 Microsoft Store Pro 权益验证，再提交徽章署名。", "請先完成 Microsoft Store Pro 權益驗證，再提交徽章署名。", "Verify your Microsoft Store Pro entitlement before submitting a badge name.", "バッジ名を送信する前に、Microsoft Store の Pro を認証してください。" },
            ["署名只支持中英文、数字、空格、连字符和下划线，长度为 2–18 个字符。"] = new[] { "署名只支持中英文、数字、空格、连字符和下划线，长度为 2–18 个字符。", "署名只支援中英文、數字、空格、連字號和底線，長度為 2–18 個字元。", "Use Chinese or English letters, numbers, spaces, hyphens, or underscores (2–18 characters).", "中国語・英字・数字・空白・ハイフン・アンダースコアを使えます（2～18 文字）。" },
            ["已打开 Microsoft Store，可在同一页面评分并撰写评价。"] = new[] { "已打开 Microsoft Store，可在同一页面评分并撰写评价。", "已開啟 Microsoft Store，可在同一頁面評分並撰寫評論。", "Microsoft Store is open. You can rate and review on the same page.", "Microsoft Store を開きました。同じページで評価とレビューを投稿できます。" },
            ["暂时无法打开 Microsoft Store，请稍后重试。"] = new[] { "暂时无法打开 Microsoft Store，请稍后重试。", "暫時無法開啟 Microsoft Store，請稍後重試。", "Microsoft Store can't be opened right now. Try again later.", "Microsoft Store を開けません。後でもう一度お試しください。" },
            ["暂时无法打开 Pro 购买窗口，请使用 Microsoft Store 安装版重试。"] = new[] { "暂时无法打开 Pro 购买窗口，请使用 Microsoft Store 安装版重试。", "暫時無法開啟 Pro 購買視窗，請使用 Microsoft Store 安裝版重試。", "The Pro purchase window can't be opened. Try the Microsoft Store installation.", "Pro の購入画面を開けません。Microsoft Store 版でお試しください。" },
            ["暂时无法读取 Pro 商品，请确认应用已从 Microsoft Store 安装。"] = new[] { "暂时无法读取 Pro 商品，请确认应用已从 Microsoft Store 安装。", "暫時無法讀取 Pro 商品，請確認應用程式已從 Microsoft Store 安裝。", "Pro details can't be loaded. Confirm that the app was installed from Microsoft Store.", "Pro 商品を読み込めません。Microsoft Store 版がインストールされているか確認してください。" },
            ["暂时无法写入剪贴板，请稍后重试。"] = new[] { "暂时无法写入剪贴板，请稍后重试。", "暫時無法寫入剪貼簿，請稍後重試。", "Couldn't copy to the clipboard. Try again later.", "クリップボードにコピーできません。後でもう一度お試しください。" },
            ["暂时无法验证 Pro 状态，请检查网络和 Microsoft Store 登录状态。"] = new[] { "暂时无法验证 Pro 状态，请检查网络和 Microsoft Store 登录状态。", "暫時無法驗證 Pro 狀態，請檢查網路和 Microsoft Store 登入狀態。", "Pro status can't be verified. Check your network and Microsoft Store sign-in.", "Pro 状態を認証できません。ネットワークと Microsoft Store のサインインを確認してください。" },
            ["正在连接 Microsoft Store…"] = new[] { "正在连接 Microsoft Store…", "正在連線至 Microsoft Store…", "Connecting to Microsoft Store…", "Microsoft Store に接続中…" },
            ["大丞子"] = new[] { "大丞子", "大丞子", "大丞子", "大丞子" },
            ["软件著作权"] = new[] { "软件著作权", "軟體著作權", "Software copyright", "ソフトウェア著作権" },
            ["申请中"] = new[] { "申请中", "申請中", "Pending", "申請中" }
        };

        private static readonly Dictionary<string, string> Canonical = BuildCanonicalLookup();

        public static AppLanguagePreference Preference { get; private set; } = AppLanguagePreference.System;

        public static AppLanguagePreference EffectiveLanguage => Resolve(Preference);

        public static void SetPreference(AppLanguagePreference preference)
        {
            Preference = preference;
        }

        public static IReadOnlyList<LanguageOption> CreateOptions()
        {
            return new[]
            {
                new LanguageOption(AppLanguagePreference.System, "跟随系统"),
                new LanguageOption(AppLanguagePreference.SimplifiedChinese, "简体中文"),
                new LanguageOption(AppLanguagePreference.TraditionalChinese, "繁體中文"),
                new LanguageOption(AppLanguagePreference.English, "English"),
                new LanguageOption(AppLanguagePreference.Japanese, "日本語")
            };
        }

        public static string Translate(string value)
        {
            if (string.IsNullOrEmpty(value) || !Canonical.TryGetValue(value, out var key) || !Text.TryGetValue(key, out var values))
            {
                return value;
            }

            return values[GetTranslationIndex(EffectiveLanguage)];
        }

        public static bool HasTranslation(string value)
        {
            return !string.IsNullOrEmpty(value) && Canonical.ContainsKey(value);
        }

        public static void ApplyTo(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.Title = Translate(window.Title);
            TranslateTree(window);
        }

        private static void TranslateTree(DependencyObject root)
        {
            if (root is FrameworkElement taggedElement &&
                string.Equals(taggedElement.Tag as string, "NativeLanguageOptions", StringComparison.Ordinal))
            {
                return;
            }

            if (root is TextBlock textBlock)
            {
                textBlock.Text = Translate(textBlock.Text);
                foreach (var inline in textBlock.Inlines)
                {
                    if (inline is Run run)
                    {
                        run.Text = Translate(run.Text);
                    }
                }
            }

            if (root is ContentControl contentControl && contentControl.Content is string content)
            {
                contentControl.Content = Translate(content);
            }

            if (root is FrameworkElement element && element.ToolTip is string toolTip)
            {
                element.ToolTip = Translate(toolTip);
            }

            if (root is FrameworkElement automationElement)
            {
                var automationName = System.Windows.Automation.AutomationProperties.GetName(automationElement);
                if (!string.IsNullOrEmpty(automationName))
                {
                    System.Windows.Automation.AutomationProperties.SetName(automationElement, Translate(automationName));
                }

                var automationHelpText = System.Windows.Automation.AutomationProperties.GetHelpText(automationElement);
                if (!string.IsNullOrEmpty(automationHelpText))
                {
                    System.Windows.Automation.AutomationProperties.SetHelpText(automationElement, Translate(automationHelpText));
                }
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                TranslateTree(VisualTreeHelper.GetChild(root, index));
            }
        }

        private static int GetTranslationIndex(AppLanguagePreference language)
        {
            switch (language)
            {
                case AppLanguagePreference.TraditionalChinese:
                    return 1;
                case AppLanguagePreference.English:
                    return 2;
                case AppLanguagePreference.Japanese:
                    return 3;
                default:
                    return 0;
            }
        }

        private static AppLanguagePreference Resolve(AppLanguagePreference preference)
        {
            if (preference != AppLanguagePreference.System)
            {
                return preference;
            }

            var name = CultureInfo.CurrentUICulture.Name;
            if (name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase))
            {
                return AppLanguagePreference.TraditionalChinese;
            }

            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return AppLanguagePreference.SimplifiedChinese;
            }

            return name.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
                ? AppLanguagePreference.Japanese
                : AppLanguagePreference.English;
        }

        private static Dictionary<string, string> BuildCanonicalLookup()
        {
            var lookup = new Dictionary<string, string>();
            foreach (var entry in Text)
            {
                foreach (var value in entry.Value)
                {
                    lookup[value] = entry.Key;
                }
            }

            return lookup;
        }
    }
}
