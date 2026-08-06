from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image as PILImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    Image,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "output" / "pdf"
SOFTWARE_NAME = "桌面歌词岛软件"
VERSION = "V2.0.36"
OWNER = "么博丞"
SOURCE_PDF = OUTPUT_DIR / f"{SOFTWARE_NAME}{VERSION}-源程序鉴别材料.pdf"
MANUAL_PDF = OUTPUT_DIR / f"{SOFTWARE_NAME}{VERSION}-软件说明书.pdf"

FONT_YAHEI = Path(r"C:\Windows\Fonts\msyh.ttc")
FONT_YAHEI_BOLD = Path(r"C:\Windows\Fonts\msyhbd.ttc")
FONT_SIMSUN = Path(r"C:\Windows\Fonts\simsun.ttc")

INK = colors.HexColor("#152238")
BLUE = colors.HexColor("#2667C9")
LIGHT_BLUE = colors.HexColor("#EAF2FF")
MID_GRAY = colors.HexColor("#657083")
LIGHT_GRAY = colors.HexColor("#EEF1F5")
RULE = colors.HexColor("#CBD3DF")


@dataclass(frozen=True)
class SourceLine:
    path: str
    file_line: int
    global_line: int
    text: str


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("MSYH", str(FONT_YAHEI), subfontIndex=0))
    pdfmetrics.registerFont(TTFont("MSYH-Bold", str(FONT_YAHEI_BOLD), subfontIndex=0))
    pdfmetrics.registerFont(TTFont("SimSun", str(FONT_SIMSUN), subfontIndex=0))


def source_files() -> list[Path]:
    """Return a deterministic logical order for the application's own source."""
    explicit = [
        ROOT / "LyricsIsland.App" / "App.xaml",
        ROOT / "LyricsIsland.App" / "App.xaml.cs",
        ROOT / "LyricsIsland.App" / "AssemblyInfo.cs",
    ]
    groups = [
        ROOT / "LyricsIsland.Core" / "Media",
        ROOT / "LyricsIsland.Core" / "Layout",
        ROOT / "LyricsIsland.Core",
        ROOT / "LyricsIsland.App" / "Media",
        ROOT / "LyricsIsland.App" / "Modules",
        ROOT / "LyricsIsland.App" / "LayoutEditing",
    ]
    ordered: list[Path] = []
    seen: set[Path] = set()

    def add(path: Path) -> None:
        resolved = path.resolve()
        if path.is_file() and resolved not in seen and path.suffix.lower() in {".cs", ".xaml"}:
            ordered.append(path)
            seen.add(resolved)

    for path in explicit:
        add(path)
    for group in groups:
        for path in sorted(group.glob("*.cs")) + sorted(group.glob("*.xaml")):
            add(path)

    tail = [
        ROOT / "LyricsIsland.App" / "GlobalHotkeyService.cs",
        ROOT / "LyricsIsland.App" / "HotkeySettings.cs",
        ROOT / "LyricsIsland.App" / "LyricsSourcePreference.cs",
        ROOT / "LyricsIsland.App" / "OverlayPlacementSettings.cs",
        ROOT / "LyricsIsland.App" / "ScreenCatalog.cs",
        ROOT / "LyricsIsland.App" / "MainWindow.xaml",
        ROOT / "LyricsIsland.App" / "MainWindow.xaml.cs",
        ROOT / "LyricsIsland.App" / "PlacementSettingsWindow.xaml",
        ROOT / "LyricsIsland.App" / "PlacementSettingsWindow.xaml.cs",
    ]
    for path in tail:
        add(path)

    for project in (ROOT / "LyricsIsland.Core", ROOT / "LyricsIsland.App"):
        for path in sorted(project.rglob("*.cs")) + sorted(project.rglob("*.xaml")):
            if "bin" not in path.parts and "obj" not in path.parts:
                add(path)
    return ordered


def collect_source_lines(files: Sequence[Path]) -> list[SourceLine]:
    lines: list[SourceLine] = []
    global_line = 0
    for path in files:
        relative = path.relative_to(ROOT).as_posix()
        content = path.read_text(encoding="utf-8-sig").splitlines()
        for file_line, text in enumerate(content, start=1):
            # Blank rows are omitted so every numbered row in the deposited
            # material contains source text while file-level line references
            # remain traceable to the repository.
            if not text.strip():
                continue
            global_line += 1
            lines.append(SourceLine(relative, file_line, global_line, text.expandtabs(4)))
    return lines


def select_deposit_lines(all_lines: Sequence[SourceLine]) -> list[SourceLine]:
    required = 60 * 50
    if len(all_lines) < required:
        raise RuntimeError(f"Source has only {len(all_lines)} lines; at least {required} are required")
    return list(all_lines[:1500]) + list(all_lines[-1500:])


def page_file_label(page_lines: Sequence[SourceLine]) -> str:
    first = page_lines[0]
    last = page_lines[-1]
    if first.path == last.path:
        return f"文件：{first.path}（第 {first.file_line}-{last.file_line} 行）"
    return f"文件范围：{first.path} → {last.path}"


def draw_source_pdf(lines: Sequence[SourceLine]) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    width, height = A4
    c = canvas.Canvas(str(SOURCE_PDF), pagesize=A4, pageCompression=1)
    c.setTitle(f"{SOFTWARE_NAME}{VERSION}源程序鉴别材料")
    c.setAuthor(OWNER)
    c.setSubject("计算机软件著作权登记源程序鉴别材料")

    left = 13 * mm
    right = 13 * mm
    top_y = height - 17 * mm
    code_x = left + 15 * mm
    available_width = width - right - code_x
    first_global = lines[0].global_line
    last_global = lines[-1].global_line

    for page_index in range(60):
        start = page_index * 50
        page_lines = lines[start : start + 50]

        c.setFillColor(INK)
        c.setFont("MSYH-Bold", 9.3)
        c.drawString(left, top_y, f"{SOFTWARE_NAME} {VERSION}  源程序鉴别材料")
        c.setFont("MSYH", 8.2)
        c.setFillColor(MID_GRAY)
        page_text = f"第 {page_index + 1} 页 / 共 60 页"
        c.drawRightString(width - right, top_y, page_text)

        c.setStrokeColor(BLUE)
        c.setLineWidth(0.75)
        c.line(left, top_y - 4 * mm, width - right, top_y - 4 * mm)

        c.setFont("MSYH", 6.6)
        c.setFillColor(MID_GRAY)
        c.drawString(left, top_y - 8 * mm, page_file_label(page_lines))

        y = top_y - 13 * mm
        leading = 12.55
        for line in page_lines:
            c.setFillColor(colors.HexColor("#768195"))
            c.setFont("SimSun", 5.2)
            c.drawRightString(code_x - 2.0 * mm, y, f"{line.global_line:06d}")

            raw = line.text if line.text else " "
            base_size = 6.15
            measured = pdfmetrics.stringWidth(raw, "SimSun", base_size)
            font_size = base_size if measured <= available_width else max(4.0, base_size * available_width / measured)
            c.setFillColor(colors.HexColor("#10151D"))
            c.setFont("SimSun", font_size)
            c.drawString(code_x, y, raw)
            y -= leading

        c.setStrokeColor(RULE)
        c.setLineWidth(0.4)
        c.line(left, 13 * mm, width - right, 13 * mm)
        c.setFont("MSYH", 6.7)
        c.setFillColor(MID_GRAY)
        deposit = "前30页连续交存" if page_index < 30 else "后30页连续交存"
        c.drawString(left, 9.5 * mm, f"著作权人：{OWNER}｜独立开发｜{deposit}")
        c.drawRightString(
            width - right,
            9.5 * mm,
            f"全程序行号范围 {first_global}-{last_global}",
        )
        c.showPage()
    c.save()


class ManualDocTemplate(BaseDocTemplate):
    def __init__(self, filename: str, **kwargs):
        super().__init__(filename, pagesize=A4, **kwargs)
        width, height = A4
        frame = Frame(
            20 * mm,
            18 * mm,
            width - 40 * mm,
            height - 38 * mm,
            id="body",
            leftPadding=0,
            rightPadding=0,
            topPadding=10 * mm,
            bottomPadding=5 * mm,
        )
        self.addPageTemplates([PageTemplate(id="manual", frames=[frame], onPage=self._decorate_page)])

    def _decorate_page(self, c: canvas.Canvas, doc) -> None:
        width, height = A4
        page = c.getPageNumber()
        c.saveState()
        if page > 1:
            c.setFont("MSYH", 7.5)
            c.setFillColor(MID_GRAY)
            c.drawString(20 * mm, height - 13 * mm, f"{SOFTWARE_NAME} {VERSION}｜软件说明书")
            c.drawRightString(width - 20 * mm, height - 13 * mm, f"第 {page} 页")
            c.setStrokeColor(RULE)
            c.setLineWidth(0.45)
            c.line(20 * mm, height - 16 * mm, width - 20 * mm, height - 16 * mm)
        c.setStrokeColor(RULE)
        c.setLineWidth(0.35)
        c.line(20 * mm, 13 * mm, width - 20 * mm, 13 * mm)
        c.setFont("MSYH", 7.2)
        c.setFillColor(MID_GRAY)
        c.drawString(20 * mm, 9 * mm, f"著作权人：{OWNER}｜开发方式：独立开发")
        c.drawRightString(width - 20 * mm, 9 * mm, f"{SOFTWARE_NAME} {VERSION}")
        c.restoreState()


def manual_styles() -> dict[str, ParagraphStyle]:
    sample = getSampleStyleSheet()
    return {
        "cover_kicker": ParagraphStyle(
            "cover_kicker",
            parent=sample["Normal"],
            fontName="MSYH-Bold",
            fontSize=10,
            leading=16,
            textColor=BLUE,
            alignment=TA_CENTER,
            spaceAfter=10,
        ),
        "cover_title": ParagraphStyle(
            "cover_title",
            parent=sample["Title"],
            fontName="MSYH-Bold",
            fontSize=28,
            leading=38,
            textColor=INK,
            alignment=TA_CENTER,
            spaceAfter=8,
        ),
        "cover_subtitle": ParagraphStyle(
            "cover_subtitle",
            parent=sample["Normal"],
            fontName="MSYH",
            fontSize=14,
            leading=23,
            textColor=MID_GRAY,
            alignment=TA_CENTER,
            spaceAfter=12,
        ),
        "h1": ParagraphStyle(
            "h1",
            parent=sample["Heading1"],
            fontName="MSYH-Bold",
            fontSize=16,
            leading=24,
            textColor=BLUE,
            spaceBefore=0,
            spaceAfter=10,
            keepWithNext=True,
        ),
        "h2": ParagraphStyle(
            "h2",
            parent=sample["Heading2"],
            fontName="MSYH-Bold",
            fontSize=12,
            leading=19,
            textColor=INK,
            spaceBefore=8,
            spaceAfter=5,
            keepWithNext=True,
        ),
        "body": ParagraphStyle(
            "body",
            parent=sample["BodyText"],
            fontName="MSYH",
            fontSize=10.2,
            leading=18,
            textColor=colors.HexColor("#202833"),
            alignment=TA_LEFT,
            firstLineIndent=20.4,
            spaceAfter=5,
        ),
        "item": ParagraphStyle(
            "item",
            parent=sample["BodyText"],
            fontName="MSYH",
            fontSize=9.9,
            leading=17,
            textColor=colors.HexColor("#202833"),
            leftIndent=6 * mm,
            firstLineIndent=-6 * mm,
            spaceAfter=4,
        ),
        "caption": ParagraphStyle(
            "caption",
            parent=sample["Normal"],
            fontName="MSYH",
            fontSize=8.2,
            leading=13,
            textColor=MID_GRAY,
            alignment=TA_CENTER,
            spaceBefore=4,
            spaceAfter=8,
        ),
        "toc": ParagraphStyle(
            "toc",
            parent=sample["Normal"],
            fontName="MSYH",
            fontSize=10.2,
            leading=20,
            textColor=INK,
            leftIndent=5 * mm,
            spaceAfter=1,
        ),
        "note": ParagraphStyle(
            "note",
            parent=sample["BodyText"],
            fontName="MSYH",
            fontSize=9.6,
            leading=17,
            textColor=INK,
            leftIndent=6 * mm,
            rightIndent=6 * mm,
            spaceBefore=4,
            spaceAfter=7,
            borderColor=colors.HexColor("#BCD2F5"),
            borderWidth=0.6,
            borderPadding=8,
            backColor=LIGHT_BLUE,
        ),
    }


def fit_image(path: Path, max_width: float, max_height: float) -> Image:
    with PILImage.open(path) as im:
        width, height = im.size
    scale = min(max_width / width, max_height / height)
    image = Image(str(path), width=width * scale, height=height * scale)
    image.hAlign = "CENTER"
    return image


def p(text: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(text, style)


def add_section(story: list, styles: dict[str, ParagraphStyle], title: str, paragraphs: Iterable[str], items: Iterable[str] = ()) -> None:
    story.append(p(title, styles["h1"]))
    for paragraph in paragraphs:
        story.append(p(paragraph, styles["body"]))
    for index, item in enumerate(items, start=1):
        story.append(p(f"（{index}）{item}", styles["item"]))


def add_figure(story: list, styles: dict[str, ParagraphStyle], path: Path, caption: str, width_mm: float = 112, height_mm: float = 67) -> None:
    if not path.exists():
        return
    story.append(
        KeepTogether(
            [
                Spacer(1, 3 * mm),
                fit_image(path, width_mm * mm, height_mm * mm),
                p(caption, styles["caption"]),
            ]
        )
    )


def add_key_value_table(story: list, styles: dict[str, ParagraphStyle], rows: Sequence[tuple[str, str]]) -> None:
    data = [[p(label, styles["item"]), p(value, styles["body"])] for label, value in rows]
    table = Table(data, colWidths=[38 * mm, 117 * mm], hAlign="CENTER")
    table.setStyle(
        TableStyle(
            [
                ("GRID", (0, 0), (-1, -1), 0.45, RULE),
                ("BACKGROUND", (0, 0), (0, -1), LIGHT_GRAY),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    story.append(table)


def build_manual_story() -> list:
    styles = manual_styles()
    story: list = []

    story.extend(
        [
            Spacer(1, 42 * mm),
            p("计算机软件著作权登记鉴别材料", styles["cover_kicker"]),
            p(SOFTWARE_NAME, styles["cover_title"]),
            p(f"{VERSION} 软件说明书", styles["cover_subtitle"]),
            Spacer(1, 16 * mm),
            Table(
                [
                    [p("软件简称", styles["item"]), p("歌词岛", styles["body"])],
                    [p("著作权人", styles["item"]), p(OWNER, styles["body"])],
                    [p("开发方式", styles["item"]), p("独立开发", styles["body"])],
                    [p("权利取得", styles["item"]), p("原始取得", styles["body"])],
                    [p("文档性质", styles["item"]), p("用户操作与软件设计说明", styles["body"])],
                ],
                colWidths=[38 * mm, 92 * mm],
                hAlign="CENTER",
                style=TableStyle(
                    [
                        ("GRID", (0, 0), (-1, -1), 0.45, RULE),
                        ("BACKGROUND", (0, 0), (0, -1), LIGHT_GRAY),
                        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                        ("LEFTPADDING", (0, 0), (-1, -1), 8),
                        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
                        ("TOPPADDING", (0, 0), (-1, -1), 7),
                        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
                    ]
                ),
            ),
            Spacer(1, 26 * mm),
            p(f"本说明书描述软件 {VERSION} 的运行环境、核心功能、用户操作流程、模块化布局、歌词同步、播放器兼容和异常降级机制。", styles["note"]),
            PageBreak(),
            p("目录", styles["h1"]),
        ]
    )
    toc_items = [
        "1. 文档目的与软件概述",
        "2. 运行环境与安装启动",
        "3. 总体架构与数据流",
        "4. 多播放器识别与选择",
        "5. 歌词检索、匹配与同步",
        "6. 桌面歌词岛显示与交互",
        "7. 模块系统与布局模式",
        "8. 真实歌词岛布局编辑",
        "9. 播放控制与时间轴补偿",
        "10. 偏好设置与全局快捷键",
        "11. 屏幕定位与鼠标避让",
        "12. 缓存、数据与隐私",
        "13. 异常处理与功能降级",
        "14. 标准使用流程",
        "15. 常见问题与维护",
        "16. 版本范围与权利声明",
    ]
    for item in toc_items:
        story.append(p(item, styles["toc"]))
    story.extend(
        [
            Spacer(1, 8 * mm),
            p("说明：软件通过 Windows 系统媒体会话读取当前媒体状态，因此兼容对象以播放器是否正确发布系统媒体会话为准。文中列举的播放器用于说明已配置的识别范围，不构成对第三方品牌的从属或授权关系。", styles["note"]),
            PageBreak(),
        ]
    )

    add_section(
        story,
        styles,
        "1. 文档目的与软件概述",
        [
            "桌面歌词岛软件是一款运行于 Windows 桌面环境的媒体辅助应用。软件从系统媒体会话读取当前播放曲目的标题、歌手、专辑、播放状态、进度和封面信息，再从多个歌词来源检索同步歌词，并以屏幕顶部悬浮歌词岛的形式持续显示。",
            "本软件不依赖单一音乐平台的私有播放接口。只要播放器能够向 Windows 系统媒体会话发布可用的播放信息，软件就可以按照统一流程识别会话、选择当前播放器、取得媒体元数据并进入歌词匹配管线。",
            f"{VERSION} 在基础歌词显示功能上提供播放器自动选择与锁定、时间轴可靠性判断、内部计时补偿、模块化界面、实时布局编辑、播放控制、封面与歌曲信息模块以及多显示器定位等能力。",
            "软件的目标是在不遮挡主要工作内容的前提下，让歌词、播放信息和常用控制停留在屏幕顶部，并在没有播放任务时自动收起，以降低桌面占用。",
        ],
        [
            "支持 Apple Music、QQ音乐、网易云音乐、酷狗音乐、酷我音乐、Spotify 以及其他兼容 Windows 系统媒体会话的播放器。",
            "支持 LRCLIB、QQ音乐、酷狗音乐、网易云音乐等歌词来源，并通过候选匹配策略减少错配。",
            "支持单行、多行、翻译、长句滚动、歌词偏移、缓存和无歌词降级显示。",
            "支持专辑封面、歌词、播放控制、歌曲信息、播放进度和分割线等可组合模块。",
        ],
    )
    add_figure(story, styles, ROOT / "视觉宣传" / "zh" / "2.png", "图 1  软件在 Windows 工作场景中的顶部歌词岛显示效果", 108, 64)
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "2. 运行环境与安装启动",
        [
            "软件采用 C# 与 WPF 开发，目标运行框架为 .NET Core 3.1，并通过 Microsoft Windows SDK Contracts 调用系统媒体会话接口。软件为 Windows 图形桌面程序，发布后以可执行文件方式运行。",
            "建议运行环境为 Windows 10 1903 或更高版本、Windows 11，使用具备正常图形桌面会话的 x64 处理器设备。建议至少具备 4 GB 内存和可用网络连接，以便访问在线歌词来源。",
            "首次运行时，用户启动软件可执行文件。软件完成单实例检查、设置加载、托盘图标初始化、媒体会话管理器初始化和歌词缓存初始化后，进入后台等待播放状态。重复启动不会创建多个独立实例。",
            "当用户在兼容播放器中开始播放音乐，软件监听到有效媒体会话并获得曲目信息后，歌词岛从屏幕顶部滑入；暂停或停止达到设定条件后，歌词岛自动收回屏幕边缘。",
        ],
        [
            "启动前确认播放器能够在 Windows 媒体控制界面显示当前曲目。",
            "若系统缺少运行时，应安装与发布包匹配的 .NET Desktop Runtime，或使用自包含发布版本。",
            "软件通过右键歌词岛或托盘入口打开偏好设置，退出操作通过托盘菜单完成。",
            "软件在普通用户权限下工作，不要求修改播放器文件，也不注入第三方播放器进程。",
        ],
    )
    add_key_value_table(
        story,
        styles,
        [
            ("开发语言", "C#、XAML"),
            ("界面技术", "Windows Presentation Foundation（WPF）"),
            ("运行框架", ".NET Core 3.1"),
            ("系统接口", "Windows System Media Transport Controls（SMTC）"),
            ("网络用途", "访问多个歌词来源并下载同步歌词数据"),
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "3. 总体架构与数据流",
        [
            "软件采用应用界面层、媒体会话层、歌词服务层、时间轴协调层、布局与交互层、缓存与设置层相互分离的结构。界面层不直接保存第三方播放器对象，而是消费统一的媒体快照。",
            "媒体会话服务持续枚举系统中的播放会话，监听媒体属性、播放状态和时间轴变化，并将来源标识、歌曲元数据、控制能力和更新时间封装为不可变快照。会话选择策略再从多个候选中确定当前活动播放器。",
            "歌词服务层对标题与歌手进行清洗，依次调用配置的歌词来源，解析同步时间标签和翻译信息，生成候选结果并执行匹配。被选中的歌词会写入本地缓存，后续播放同一曲目时优先复用。",
            "布局层根据当前布局模式与模块配置生成真实歌词岛。媒体状态发生变化时，各模块从统一渲染状态更新自身内容；某个字段缺失只影响依赖该字段的模块，不会使整体界面失效。",
        ],
        [
            "输入：系统媒体会话、用户设置、网络歌词响应和全局快捷键。",
            "处理：会话选择、元数据清洗、歌词匹配、时间轴补偿、布局投影和交互状态控制。",
            "输出：顶部歌词岛、播放控制指令、歌词缓存以及持久化布局设置。",
            "边界：软件不读取音乐文件本体，不保存第三方账户密码，不执行机器翻译。",
        ],
    )
    add_figure(story, styles, ROOT / "docs" / "patent" / "generated-assets" / "fig1-system-architecture.png", "图 2  软件总体架构与主要数据流", 118, 67)
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "4. 多播放器识别与选择",
        [
            "软件通过 GlobalSystemMediaTransportControlsSessionManager 获取系统媒体会话集合，不将应用固定为某一个音乐客户端。每个会话至少包含来源应用标识、媒体属性、播放状态和可用控制能力。",
            "播放器配置目录根据来源应用标识中的特征字符串识别 Apple Music、QQ音乐、网易云音乐、酷狗音乐、酷我音乐和 Spotify；不能归入已知配置的会话使用通用播放器配置。",
            "自动模式优先选择正在播放且最近发生变化的会话，其次参考 Windows 当前会话，再考虑最近暂停但仍具有有效媒体属性的会话。该策略可以处理多个播放器同时运行的场景。",
            "用户也可以在设置页锁定某个已发现播放器。锁定播放器暂时退出时，软件保留锁定偏好并回退到自动选择；该播放器重新发布会话后，可恢复到锁定目标。",
        ],
        [
            "打开偏好设置并进入播放器选择区域。",
            "选择“自动”以跟随最近活动的媒体会话，或选择已检测到的播放器名称。",
            "开始播放并观察歌曲标题、歌手、封面和歌词是否随会话更新。",
            "如播放器未被识别，先确认其系统媒体控制卡片能显示歌曲信息，再重新启动软件。",
        ],
    )
    story.append(p("兼容性说明：播放器版本更新可能改变其系统媒体会话行为。软件优先使用运行时实际能力，而不是仅依据静态播放器名称判断控制、封面或时间轴是否可用。", styles["note"]))
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "5. 歌词检索、匹配与同步",
        [
            "曲目变化后，软件从媒体快照构造曲目标识，对标题、歌手和专辑字段进行清洗，移除常见版本附注、无关标记和播放器附加文本，再生成适合检索的查询条件。",
            "软件支持多个歌词来源。用户可以指定首选来源，但首选来源返回空结果或候选不匹配时，组合歌词客户端会继续尝试其他来源，避免单一服务故障导致歌词功能完全不可用。",
            "候选匹配器综合比较标题、歌手、专辑和时长，降低同名歌曲、现场版、伴奏版和重制版之间的错配概率。歌词包解析器负责处理原文、翻译、逐行时间标签和缺失字段。",
            "同步显示以当前媒体位置为基础，再叠加用户配置的歌词偏移。播放进度可靠时直接跟随播放器；时间轴停止更新或缺失时，软件使用单调时钟估算位置，并在恢复可信时间轴后完成校准。",
        ],
        [
            "当前句、下一句和翻译可以按设置组合显示。",
            "长歌词不直接以省略号截断，而是结合当前歌词持续时间执行横向滚动。",
            "没有翻译时只显示原文；开启翻译并不会调用机器翻译服务。",
            "未找到歌词时，歌词模块显示状态提示，封面、歌曲信息、进度和控制仍可正常工作。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "6. 桌面歌词岛显示与交互",
        [
            "歌词岛定位在用户选择的显示器顶部，可根据顶边、左侧或右侧停靠策略计算可见位置和收起位置。播放开始时，岛体以动画滑入；暂停或无有效会话时按可见性策略延迟收起。",
            "岛体采用始终置顶窗口显示，背景、圆角轮廓、内容宽度和模块间距由布局模型计算。运行状态下可启用点击穿透，使用户仍可操作歌词岛下方的窗口内容。",
            "鼠标靠近时，软件根据探测区域和光晕几何计算局部透明度，让鼠标附近的歌词岛背景和文字变淡。该效果仅改变显示，不修改播放或歌词状态。",
            "用户右键歌词岛可以打开偏好设置。设置窗口进入布局编辑状态时，真实歌词岛固定展开并暂时关闭自动收起与点击穿透，以便拖放模块和调整顺序。",
        ],
        [
            "播放状态：显示用户选定布局并持续刷新歌词。",
            "暂停状态：保留短暂交互时间，随后收起但允许顶部感应区唤回。",
            "无会话状态：岛体完全移出主要可见区域，减少桌面占用。",
            "编辑状态：固定展开、显示插入位置并允许跨窗口拖放。",
        ],
    )
    add_figure(story, styles, ROOT / "视觉宣传" / "zh" / "1.png", "图 3  顶部吸附、播放滑入与空闲收起的界面示意", 108, 64)
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "7. 模块系统与布局模式",
        [
            f"{VERSION} 将歌词岛内容拆分为独立模块实例。每个实例保存类型、标识、顺序和配置。渲染器根据布局模式将实例映射为具体视图。",
            "当前模块包括专辑封面、歌词、播放控制、歌曲信息、播放进度和分割线。分割线允许重复添加，其他模块是否允许重复由布局规则判断。缺少媒体字段时，模块显示占位或禁用状态。",
            "A 模式为横向积木布局，模块按用户顺序排列在一行中。软件根据模块首选宽度和屏幕可用宽度计算岛体尺寸，并对可伸缩模块进行适配。",
            "C 模式保存收起布局和展开布局。鼠标进入有效区域并满足停留时间后展开，离开且没有拖动或菜单交互时延迟收起。A 与 C 的设置彼此独立，切换模式不会覆盖另一模式。",
        ],
        [
            "专辑封面模块：显示系统媒体会话缩略图或中性占位。",
            "歌词模块：显示当前句、下一句和可选翻译。",
            "播放控制模块：按运行时能力显示上一曲、播放/暂停和下一曲。",
            "歌曲信息与进度模块：显示标题、歌手、专辑、位置、时长和估算标记。",
            "分割线模块：用于构造视觉分组，可配置透明度和两侧间距。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "8. 真实歌词岛布局编辑",
        [
            "用户在偏好设置中进入模块布局页面后，屏幕顶部正在运行的真实歌词岛进入编辑模式。设置窗口提供模块工具箱，但不创建与真实界面分离的静态预览。",
            "用户从工具箱按下模块并拖向歌词岛时，应用创建包含操作标识、模块类型和来源信息的拖放载荷。岛体接收拖动事件后计算最近插入位置，显示拖动幽灵和发光插槽。",
            "当指针进入目标插入点附近时，布局编辑器采用吸附阈值确定目标；跨过现有模块中线时，插入位置从该模块之前切换到之后，其他模块即时让位并展示草稿效果。",
            "编辑过程只修改深拷贝草稿。选择保存后，草稿一次性替换对应布局并持久化；选择取消、关闭设置窗口或放弃操作时恢复进入编辑模式前的布局。",
        ],
        [
            "进入“模块布局”，确认真实歌词岛已固定展开。",
            "从工具箱将所需模块拖到岛体的目标位置，观察插入槽和实时让位。",
            "拖动岛内现有模块改变顺序；选择分割线后调整透明度和左右间距。",
            "点击“保存布局”提交全部变化，或点击“取消”完整回滚。",
        ],
    )
    add_figure(story, styles, ROOT / "docs" / "patent" / "generated-assets" / "fig2-edit-flow.png", "图 4  布局编辑会话的保存、取消和关闭回滚流程", 105, 69)
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "9. 播放控制与时间轴补偿",
        [
            "媒体快照包含上一曲、播放、暂停和下一曲能力。播放控制模块只对播放器实际声明支持的命令启用按钮；不支持的按钮以低透明度显示，防止向会话发送无效指令。",
            "当用户点击控制按钮，播放意图协调器记录本次操作并请求媒体会话服务执行命令。命令期间会话消失或播放器拒绝操作时，软件返回失败状态并重新选择会话，不让异常终止界面刷新。",
            "时间轴协调器保存最后一次可信位置、采样时间和播放状态。真实位置连续且范围合理时直接使用；播放过程中位置长期不动、缺失或为零时，转为内部单调时钟估算。",
            "暂停时估算位置冻结，恢复后从冻结点继续。新的可信时间轴到达时，小偏差平滑校准，大偏差立即跳转，以减少手动跳转歌曲后歌词长期错位。",
        ],
        [
            "进度模块在估算状态显示约等标记，不把估算值伪装为精确时间。",
            "切歌时取消旧歌词异步结果写回，防止上一首歌词覆盖新曲目。",
            "用户可以通过快捷键在播放器时间轴不稳定时手动校准歌词。",
            "控制能力、时间轴、封面和歌词分别降级，避免单点缺失导致整体不可用。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "10. 偏好设置与全局快捷键",
        [
            "偏好设置用于管理主题、歌词显示、播放器选择、歌词来源、缓存容量、目标显示器、停靠位置、模块布局、鼠标避让和全局快捷键。设置变更由应用读取并写入用户本地配置。",
            "主题可选择浅色、深色或跟随系统。歌词显示可选择单行或多行、是否显示翻译以及默认时间偏移。播放器区域可选择自动模式或锁定已检测到的目标播放器。",
            "全局快捷键由系统热键服务注册，即使歌词岛不是当前焦点，用户也可以调整同步偏移。注册冲突时软件保留稳定状态并提示用户重新设置，不覆盖其他程序已占用的组合键。",
            "默认组合键为 Ctrl+Alt+Left、Ctrl+Alt+Right 和 Ctrl+Alt+Down，分别用于调整提前量、调整延后量和重置偏移。歌词岛获得焦点后还可使用方向键进行较小步长微调。",
        ],
        [
            "Ctrl+Alt+Left：将歌词显示相对播放位置向一个方向调整 500 毫秒。",
            "Ctrl+Alt+Right：向相反方向调整 500 毫秒。",
            "Ctrl+Alt+Down：恢复用户配置的基准偏移。",
            "焦点状态下 Left、Right、Up、Down：以 200 毫秒步长微调；R 键重置。",
        ],
    )
    story.append(p("操作提示：不同用户对“提前”和“延后”的感知方向可能不同，实际以歌词是否更早或更晚出现为判断依据。调整后可播放节奏明显的歌曲进行验证。", styles["note"]))
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "11. 屏幕定位与鼠标避让",
        [
            "软件枚举系统显示器并为每个显示器保存工作区边界、缩放和标识。用户可以指定目标显示器，并选择顶部居中、靠左或靠右等停靠方式。",
            "位置计算器根据岛体实际尺寸、显示器工作区和停靠边缘生成可见坐标及收起坐标。设置改变、分辨率改变或模块布局改变时，软件重新计算位置，避免岛体超出有效屏幕范围。",
            "鼠标避让功能使用指针与岛体几何关系计算局部透明区域。用户可以调整探测范围、光晕尺寸、长宽比和透明度强度，使岛体在可读性和下方窗口可操作性之间取得平衡。",
            "进入布局编辑时，软件暂时关闭点击穿透和避让动画，保证拖放命中；保存或取消后恢复运行状态策略。多显示器环境下，编辑窗口与歌词岛可以位于不同显示器。",
        ],
        [
            "选择目标显示器后播放歌曲，确认岛体出现在预期屏幕顶部。",
            "修改停靠方向并检查岛体在不同宽度布局下没有越界。",
            "开启鼠标避让，移动指针靠近歌词岛，观察局部透明度变化。",
            "如高 DPI 下位置不准确，重新打开设置触发屏幕目录刷新。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "12. 缓存、数据与隐私",
        [
            "歌词缓存按曲目标识保存已取得的歌词包，采用容量限制和最近最少使用策略清理旧条目。默认缓存目录位于当前 Windows 用户的本地应用数据目录，不写入音乐客户端安装目录。",
            "软件保存的设置包括主题、歌词显示方式、偏移、目标显示器、停靠位置、播放器选择、模块列表和模块配置。布局编辑使用草稿提交，避免意外关闭写入半完成数据。",
            "软件通过系统媒体会话读取播放器公开给 Windows 的当前曲目信息，不需要用户提供第三方音乐账户、密码或登录令牌。软件不会扫描整个音乐库，也不上传用户的本地文件内容。",
            "为检索歌词，软件会向歌词服务发送经过清洗的歌曲标题、歌手、专辑或时长等必要查询信息。网络请求仅用于取得歌词候选，不用于生成用户画像。",
        ],
        [
            "缓存目录示例：%LOCALAPPDATA%\\LyricsIsland\\lyrics。",
            "用户可以在设置中限制缓存容量，或退出软件后清理缓存文件。",
            "源程序鉴别材料只包含本项目自有代码，不包含第三方依赖库源代码。",
            "发布时应保留第三方组件许可文本，但第三方组件不改变本软件自有代码的著作权归属。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "13. 异常处理与功能降级",
        [
            "系统媒体会话管理器初始化失败时，软件保留托盘与设置入口并进行有限重试；错误不会直接传播到界面定时刷新循环。会话在读取或控制期间消失时，服务返回稳定失败状态并重新枚举候选。",
            "播放器没有封面时，封面模块显示来源图标或中性占位；没有时间轴时使用内部计时；没有控制能力时只禁用对应按钮；没有歌词时仅歌词模块显示提示。",
            "歌词来源网络失败、响应格式异常或候选不匹配时，组合客户端继续尝试其他来源。缓存文件损坏时可以忽略异常条目并重新检索，不阻止媒体信息显示。",
            "布局中出现当前版本未知的模块类型时，渲染器跳过无法识别的实例并保留其他有效模块。布局宽度超过屏幕上限时，编辑界面提示用户调整，不在运行状态中静默裁掉模块。",
        ],
        [
            "无媒体会话：歌词岛收起并等待新的会话事件。",
            "无歌词：显示搜索失败或无歌词状态，其他模块继续刷新。",
            "时间轴不可靠：使用估算并允许快捷键校准。",
            "控制命令失败：保持应用运行并更新会话选择。",
            "设置解析失败：采用默认值，避免应用因单个字段无法启动。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "14. 标准使用流程",
        [
            "本节给出从首次启动到日常使用的标准流程。用户完成基础设置后，软件可以在后台自动跟随正在播放的兼容媒体会话，不需要为每首歌曲手动切换播放器或歌词来源。",
            "首次使用时应先验证系统媒体会话是否正常：在目标播放器播放歌曲并打开 Windows 媒体控制界面，如果能够看到歌曲标题和播放按钮，说明播放器已向系统发布基础会话。",
            "随后启动桌面歌词岛软件，等待歌词岛滑入并显示歌曲信息。若歌词未匹配，可在偏好设置中切换首选歌词来源，或检查歌曲标题是否包含影响检索的特殊附注。",
            "需要自定义外观时，进入模块布局页面，把模块拖到真实歌词岛并保存。完成后返回播放场景，检查岛体宽度、歌词滚动、播放控制和收起动画是否符合个人使用习惯。",
        ],
        [
            "启动目标音乐客户端并播放任意具有清晰标题和歌手信息的歌曲。",
            "启动桌面歌词岛软件，确认自动选中当前播放会话。",
            "打开偏好设置，选择目标显示器、歌词行数、翻译、歌词来源和播放器模式。",
            "进入布局编辑，配置封面、歌词、控制、歌曲信息、进度和分割线模块。",
            "保存布局并返回桌面，使用快捷键校准歌词同步。",
            "暂停播放并观察延迟收起；恢复播放后确认歌词岛重新滑入。",
        ],
    )
    add_figure(story, styles, ROOT / "docs" / "patent" / "generated-assets" / "fig3-drag-insertion.png", "图 5  模块拖动、插入目标与吸附位置示意", 108, 67)
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "15. 常见问题与维护",
        [
            "如果歌词岛没有出现，先确认播放器正在播放、Windows 媒体控制界面能显示曲目信息，再检查软件是否已在托盘运行。必要时退出并重新启动播放器与本软件。",
            "如果歌曲信息正确但歌词错配，可切换歌词来源、清理该曲目的缓存或检查播放器标题中是否包含现场版、伴奏、重制等附注。候选匹配依赖元数据质量。",
            "如果歌词逐渐不同步，播放器可能没有持续提供可信时间轴。用户可以使用全局快捷键校准，并观察进度模块是否显示估算标记。切歌或手动跳转后软件会尝试重新校准。",
            "如果播放控制按钮不可用，说明当前会话没有向 Windows 声明对应能力。该情况由播放器实现决定，不代表歌词显示功能异常。",
        ],
        [
            "界面位置异常：重新选择显示器并保存，确认系统缩放变化后已重新打开设置。",
            "布局未保存：确认点击了“保存布局”；关闭设置窗口按取消处理。",
            "快捷键无效：检查组合键是否被其他程序占用，并在设置中重新绑定。",
            "网络歌词失败：检查网络、稍后重试或更换首选歌词来源。",
            "程序重复启动：单实例保护会将后续启动请求交给现有实例。",
        ],
    )
    story.append(PageBreak())

    add_section(
        story,
        styles,
        "16. 版本范围与权利声明",
        [
            f"本说明书对应申报软件全称“{SOFTWARE_NAME}”，版本号“{VERSION}”，软件简称“歌词岛”。申报材料中的名称和版本应与中国版权保护中心申请表保持完全一致。",
            f"本软件由{OWNER}独立开发，著作权取得方式为原始取得，申请登记的权利范围为全部权利。源程序鉴别材料从当前软件自有源代码的确定顺序中截取前连续30页和后连续30页。",
            "软件使用 Windows 平台公开系统接口和第三方依赖组件完成运行环境适配。申请登记的对象是著作权人自行编写的软件程序、结构组织、界面表达和配套文档，不主张第三方平台、品牌、接口规范或依赖组件的著作权。",
            "本说明书中的播放器名称仅用于说明兼容场景。软件并非由所列第三方音乐平台开发、授权或背书，正式产品发布时应继续避免在软件全称中使用他人商标。",
        ],
        [
            f"申请人／著作权人：{OWNER}。",
            "开发方式：独立开发。",
            "权利取得方式：原始取得。",
            "权利范围：全部权利。",
            f"软件全称：{SOFTWARE_NAME}。",
            f"版本号：{VERSION}。",
        ],
    )
    story.append(Spacer(1, 8 * mm))
    story.append(p("文档结束", styles["cover_kicker"]))

    # Keep deliberate breaks after the cover and contents only.  Subsequent
    # sections flow continuously so the deposited documentation has normal
    # text density instead of one short section per mostly blank page.
    compacted: list = []
    page_breaks = 0
    for flowable in story:
        if isinstance(flowable, PageBreak):
            page_breaks += 1
            if page_breaks <= 2:
                compacted.append(flowable)
            else:
                compacted.append(Spacer(1, 7 * mm))
        else:
            compacted.append(flowable)
    return compacted


def draw_manual_pdf() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    doc = ManualDocTemplate(
        str(MANUAL_PDF),
        title=f"{SOFTWARE_NAME}{VERSION}软件说明书",
        author=OWNER,
        subject="计算机软件著作权登记文档鉴别材料",
        leftMargin=20 * mm,
        rightMargin=20 * mm,
        topMargin=20 * mm,
        bottomMargin=18 * mm,
    )
    doc.build(build_manual_story())


def main() -> None:
    register_fonts()
    files = source_files()
    all_lines = collect_source_lines(files)
    selected = select_deposit_lines(all_lines)
    draw_source_pdf(selected)
    draw_manual_pdf()
    print(f"source_files={len(files)} total_lines={len(all_lines)} selected_lines={len(selected)}")
    print(SOURCE_PDF)
    print(MANUAL_PDF)


if __name__ == "__main__":
    main()
