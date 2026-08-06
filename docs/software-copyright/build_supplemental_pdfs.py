from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
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
SHORT_NAME = "歌词岛"
VERSION = "V2.0.36"
BUILD_VERSION = "2.0.36-Beta"
OWNER = "么博丞"


def count_source_lines() -> int:
    total = 0
    for project in (ROOT / "LyricsIsland.App", ROOT / "LyricsIsland.Core"):
        for path in list(project.rglob("*.cs")) + list(project.rglob("*.xaml")):
            if "bin" in path.parts or "obj" in path.parts:
                continue
            total += sum(
                1
                for line in path.read_text(encoding="utf-8-sig").splitlines()
                if line.strip()
            )
    return total


SOURCE_LINES = count_source_lines()

FONT_YAHEI = Path(r"C:\Windows\Fonts\msyh.ttc")
FONT_YAHEI_BOLD = Path(r"C:\Windows\Fonts\msyhbd.ttc")
INK = colors.HexColor("#152238")
BLUE = colors.HexColor("#2667C9")
MUTED = colors.HexColor("#657083")
LIGHT_BLUE = colors.HexColor("#EAF2FF")
LIGHT_GRAY = colors.HexColor("#F2F4F7")
RULE = colors.HexColor("#CBD3DF")


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("MSYH", str(FONT_YAHEI), subfontIndex=0))
    pdfmetrics.registerFont(TTFont("MSYH-Bold", str(FONT_YAHEI_BOLD), subfontIndex=0))


class BriefTemplate(BaseDocTemplate):
    def __init__(self, filename: str, running_title: str, **kwargs):
        super().__init__(filename, pagesize=A4, **kwargs)
        self.running_title = running_title
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
        self.addPageTemplates([PageTemplate(id="brief", frames=[frame], onPage=self._decorate)])

    def _decorate(self, c: canvas.Canvas, doc) -> None:
        width, height = A4
        c.saveState()
        c.setFont("MSYH", 7.5)
        c.setFillColor(MUTED)
        c.drawString(20 * mm, height - 13 * mm, self.running_title)
        c.drawRightString(width - 20 * mm, height - 13 * mm, f"第 {c.getPageNumber()} 页")
        c.setStrokeColor(RULE)
        c.setLineWidth(0.45)
        c.line(20 * mm, height - 16 * mm, width - 20 * mm, height - 16 * mm)
        c.line(20 * mm, 13 * mm, width - 20 * mm, 13 * mm)
        c.setFont("MSYH", 7.2)
        c.drawString(20 * mm, 9 * mm, f"著作权人：{OWNER}｜{SOFTWARE_NAME} {VERSION}")
        c.restoreState()


def styles() -> dict[str, ParagraphStyle]:
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "title",
            parent=base["Title"],
            fontName="MSYH-Bold",
            fontSize=23,
            leading=32,
            textColor=INK,
            alignment=TA_LEFT,
            spaceAfter=7,
        ),
        "subtitle": ParagraphStyle(
            "subtitle",
            parent=base["Normal"],
            fontName="MSYH",
            fontSize=11.5,
            leading=19,
            textColor=MUTED,
            spaceAfter=13,
        ),
        "h1": ParagraphStyle(
            "h1",
            parent=base["Heading1"],
            fontName="MSYH-Bold",
            fontSize=15,
            leading=22,
            textColor=BLUE,
            spaceBefore=10,
            spaceAfter=7,
            keepWithNext=True,
        ),
        "body": ParagraphStyle(
            "body",
            parent=base["BodyText"],
            fontName="MSYH",
            fontSize=9.7,
            leading=16.5,
            textColor=INK,
            spaceAfter=5,
        ),
        "small": ParagraphStyle(
            "small",
            parent=base["BodyText"],
            fontName="MSYH",
            fontSize=8.7,
            leading=14,
            textColor=INK,
            spaceAfter=0,
        ),
        "small_center": ParagraphStyle(
            "small_center",
            parent=base["BodyText"],
            fontName="MSYH",
            fontSize=8.7,
            leading=14,
            textColor=INK,
            alignment=TA_CENTER,
            spaceAfter=0,
        ),
        "note": ParagraphStyle(
            "note",
            parent=base["BodyText"],
            fontName="MSYH",
            fontSize=9.1,
            leading=15.5,
            textColor=INK,
            borderColor=colors.HexColor("#BCD2F5"),
            borderWidth=0.6,
            borderPadding=8,
            backColor=LIGHT_BLUE,
            spaceBefore=4,
            spaceAfter=7,
        ),
    }


def p(text: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(text, style)


def table(data: list[list[str]], widths_mm: list[float], s: dict[str, ParagraphStyle],
          header: bool = False, label_column: bool = False) -> Table:
    wrapped: list[list[Paragraph]] = []
    for row_index, row in enumerate(data):
        wrapped_row: list[Paragraph] = []
        for column_index, value in enumerate(row):
            style = s["small_center"] if header or column_index == len(row) - 1 and len(row) > 2 else s["small"]
            wrapped_row.append(p(value, style))
        wrapped.append(wrapped_row)
    result = Table(wrapped, colWidths=[value * mm for value in widths_mm], repeatRows=1 if header else 0)
    commands = [
        ("GRID", (0, 0), (-1, -1), 0.45, RULE),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]
    if header:
        commands.extend([
            ("BACKGROUND", (0, 0), (-1, 0), LIGHT_BLUE),
            ("TEXTCOLOR", (0, 0), (-1, 0), BLUE),
        ])
    if label_column:
        commands.append(("BACKGROUND", (0, 0), (0, -1), LIGHT_GRAY))
    result.setStyle(TableStyle(commands))
    return result


def build_application_brief() -> Path:
    s = styles()
    story = [
        p("计算机软件著作权登记申报填报底稿", s["title"]),
        p(f"{SOFTWARE_NAME} {VERSION}（对应当前构建 {BUILD_VERSION}）", s["subtitle"]),
        p("本文件用于在中国版权保护中心登记系统填报前核对，不是系统生成的正式申请表。", s["note"]),
        p("1. 建议登记信息", s["h1"]),
        table([
            ["软件全称", SOFTWARE_NAME],
            ["软件简称", SHORT_NAME],
            ["申报版本号", VERSION],
            ["当前构建标识", BUILD_VERSION],
            ["软件分类", "应用软件"],
            ["开发方式", "独立开发"],
            ["权利取得方式", "原始取得"],
            ["权利范围", "全部权利"],
            ["申请人／著作权人", OWNER],
            ["开发语言", "C#、XAML"],
            ["源程序量", f"当前自有源程序约 {SOURCE_LINES:,} 非空行"],
        ], [43, 127], s, label_column=True),
        Spacer(1, 4 * mm),
        p(
            f"<b>版本一致性：</b>正式申请表、源程序、说明书和补充材料统一填写"
            f"“{SOFTWARE_NAME} {VERSION}”。“{BUILD_VERSION}”只说明当前构建对应关系。",
            s["note"],
        ),
        p("2. 开发、运行与功能信息", s["h1"]),
        table([
            ["开发硬件环境", "x64 计算机；建议 8 GB 及以上内存；常规磁盘空间"],
            ["开发软件环境", "Windows 10/11、Visual Studio、.NET SDK、Windows 10 SDK"],
            ["运行硬件环境", "x64 处理器；建议 4 GB 及以上内存；支持桌面显示器"],
            ["运行软件环境", "Windows 10/11 x64；Microsoft Windows Desktop Runtime 3.1"],
            ["主要技术", "WPF、Windows SMTC、异步网络请求、本地 JSON 配置与文件缓存"],
            ["开发目的", "提供不依赖单一音乐客户端的桌面同步歌词、播放信息与轻量控制能力"],
            ["面向领域", "桌面工具、数字音乐辅助、媒体信息展示"],
            ["主要功能", "读取系统媒体会话；自动选择或锁定播放器；多源匹配同步歌词及已有翻译；显示顶部歌词岛、封面、歌曲信息、控制与进度；支持模块拖放、快捷键、多显示器、鼠标避让和缓存"],
            ["技术特点", "统一媒体快照、时间轴可靠性判断与单调时钟补偿、多歌词源候选匹配、真实悬浮窗口跨窗口拖放、草稿保存和取消回滚、按能力降级"],
        ], [43, 127], s, label_column=True),
        PageBreak(),
        p("3. 必须由申请人确认的事实", s["h1"]),
        table([
            ["项目", "需要填写或确认", "状态"],
            ["身份信息", "身份证号、证件有效期、手机号、邮箱、通信地址、邮编", "待本人填写"],
            ["完成日期", "软件实际开发完成日期，须与事实和证据一致", "待本人确认"],
            ["发表状态", "按 V2.0.36 是否已向公众提供下载或访问的事实选择", "待本人确认"],
            ["发表信息", "若已发表，填写真实首次发表日期和首次发表地点", "条件填写"],
            ["权属事实", "确认是否独立开发；如为合作、委托或职务开发须按事实调整", "待本人确认"],
            ["签章", "按系统生成文件要求签名，姓名与身份证一致", "提交前完成"],
        ], [28, 112, 30], s, header=True),
        Spacer(1, 4 * mm),
        p(
            "<b>不要代填：</b>开发完成日期、发表状态和权属事实无法仅从仓库可靠推定，"
            "本底稿故意不替申请人作法律事实判断。",
            s["note"],
        ),
        p("4. 已准备的材料", s["h1"]),
        table([
            ["材料", "内容与规格", "建议"],
            ["源程序鉴别材料", "60 页；每页 50 行；前、后各连续 30 页；A4；页眉含全称与版本", "上传 PDF"],
            ["软件说明书", "软件概述、环境、架构、功能、操作、异常处理和权利声明", "上传 PDF"],
            ["填报底稿", "登记字段建议值、待确认事实和提交检查项", "内部核对"],
            ["新增版本说明", "V2.0.36 的主要功能与技术变化", "按需上传"],
            ["身份证明", "申请人身份证明或系统要求的实名材料", "本人准备"],
            ["申请确认文件", "登记系统生成后按要求签名或盖章", "提交前完成"],
        ], [35, 105, 30], s, header=True),
        p("5. 提交前一致性检查", s["h1"]),
        table([
            ["检查项", "通过标准", "结果"],
            ["名称", f"全部材料均为“{SOFTWARE_NAME}”", "□"],
            ["版本", f"全部申报材料均为“{VERSION}”", "□"],
            ["权利人", f"全部材料均为“{OWNER}”且签名一致", "□"],
            ["页数", "源程序 60 页；说明书不足 60 页时提交全部", "□"],
            ["页眉页码", "名称、版本和连续页码清晰可读", "□"],
            ["隐私", "源码样本不含密钥、令牌、账号或真实用户数据", "□"],
            ["事实字段", "完成日期和发表信息已按事实填写", "□"],
            ["文件可读性", "PDF 无裁切、无空白页、无重复页", "□"],
        ], [34, 120, 16], s, header=True),
        Spacer(1, 5 * mm),
        p("6. 办理入口与规则依据", s["h1"]),
        p(
            "办理入口为中国版权保护中心官网。<br/>"
            "网址：https://www.ccopyright.com.cn/<br/>"
            "注册个人账号并完成实名认证后，进入软件登记业务，按系统字段录入本底稿中已确认的内容。",
            s["body"],
        ),
        table([
            ["步骤", "操作与核对重点"],
            ["1", "新建计算机软件著作权登记申请，先保存草稿，不急于提交。"],
            ["2", f"统一填写软件全称“{SOFTWARE_NAME}”、简称“{SHORT_NAME}”和版本“{VERSION}”。"],
            ["3", "按真实情况填写开发完成日期、发表状态及首次发表信息。"],
            ["4", "上传源程序、说明书、身份证明以及系统要求的确认签章文件。"],
            ["5", "提交后保存流水号，定期查看受理、补正或审查通知。"],
        ], [22, 148], s, header=True),
        Spacer(1, 5 * mm),
        p(
            "规则基础为国家版权局《计算机软件著作权登记办法》第九至十七条。"
            "其中规定申请表、程序和文档鉴别材料及相关证明文件的基本构成，"
            "并规定 A4 纸张、程序和文档前后各连续 30 页等要求。",
            s["body"],
        ),
        p(
            "<b>安全提示：</b>不向非官方人员发送登记系统密码、短信验证码或未经处理的身份证原图；"
            "不把第三方“包过”承诺当作官方要求。登记系统与上传格式可能调整，"
            "最终以提交当日中国版权保护中心系统提示为准。",
            s["note"],
        ),
    ]
    path = OUTPUT_DIR / f"{SOFTWARE_NAME}{VERSION}-申报信息填报底稿.pdf"
    doc = BriefTemplate(
        str(path),
        f"{SOFTWARE_NAME} {VERSION}｜软著申报填报底稿",
        leftMargin=20 * mm,
        rightMargin=20 * mm,
        topMargin=20 * mm,
        bottomMargin=18 * mm,
        title=f"{SOFTWARE_NAME}{VERSION}申报信息填报底稿",
        author=OWNER,
    )
    doc.build(story)
    return path


def build_version_statement() -> Path:
    s = styles()
    changes = [
        ["序号", "功能或技术变化", "具体说明"],
        ["1", "多播放器媒体会话", "读取 Windows SMTC，自动跟随最近活跃会话或锁定指定播放器。"],
        ["2", "模块化歌词岛", "将封面、歌词、歌曲信息、播放控制、进度和分割线拆分为可组合模块。"],
        ["3", "真实界面布局编辑", "从设置窗口将模块拖到真实歌词岛，支持吸附、重排、保存和取消回滚。"],
        ["4", "歌词匹配与翻译", "多个歌词来源候选检索与匹配，显示同步歌词和歌词库已有逐行翻译。"],
        ["5", "时间轴补偿", "判断时间轴可靠性；缺失或停滞时使用单调时钟估算并在恢复后校准。"],
        ["6", "播放控制与进度", "按媒体会话实际能力显示控制和进度，对不支持项目独立降级。"],
        ["7", "多显示器与桌面交互", "支持目标显示器、停靠位置、自动收起、悬停展开、点击穿透和鼠标避让。"],
        ["8", "设置、缓存与单实例", "保存主题、歌词、播放器、布局、屏幕和快捷键，提供 LRU 缓存和单实例复用。"],
    ]
    story = [
        p("新增版本说明", s["title"]),
        p(f"{SOFTWARE_NAME} {VERSION}", s["subtitle"]),
        p("本文件在登记系统或补正通知要求说明高版本软件新增功能时使用。", s["note"]),
        p("一、软件基本信息", s["h1"]),
        table([
            ["软件全称", SOFTWARE_NAME],
            ["软件简称", SHORT_NAME],
            ["申报版本号", VERSION],
            ["对应构建", BUILD_VERSION],
            ["著作权人", OWNER],
            ["开发方式", "独立开发"],
            ["权利取得方式", "原始取得"],
        ], [43, 127], s, label_column=True),
        p("二、本版本主要新增与完善内容", s["h1"]),
        table(changes, [14, 45, 111], s, header=True),
        PageBreak(),
        p("三、技术实现变化", s["h1"]),
        table([
            ["媒体层", "统一媒体会话快照、播放器画像、会话选择策略和控制能力模型。"],
            ["歌词层", "组合多个歌词客户端，清洗曲目信息、评分候选、解析同步歌词及已有翻译并缓存。"],
            ["时间轴层", "保存可信采样，使用单调时钟补偿，处理暂停、恢复、跳转、切歌和异步过期结果。"],
            ["界面层", "WPF 模块宿主、布局投影和交互状态控制器驱动顶部歌词岛及跨窗口拖放编辑。"],
            ["数据层", "设置与歌词数据写入 %LOCALAPPDATA%\\LyricsIsland，并提供旧目录迁移和异常回退。"],
        ], [43, 127], s, label_column=True),
        p("四、与申报材料的对应关系", s["h1"]),
        p(
            f"源程序鉴别材料和软件说明书均从当前 {BUILD_VERSION} 工作树生成，"
            f"对外统一登记为 {SOFTWARE_NAME} {VERSION}。源程序样本只包含 LyricsIsland.App "
            "和 LyricsIsland.Core 的自有 C#/XAML 代码，不包含第三方依赖库源代码、测试代码和网站代码。",
            s["body"],
        ),
        p(
            "<b>提交条件：</b>若本次属于该软件首次登记且系统未要求新增版本说明，可不单独上传本文件；"
            "若此前已有版本登记，或系统／补正通知要求说明高版本变化，则按实际情况核对后提交。",
            s["note"],
        ),
        Spacer(1, 15 * mm),
        p(f"著作权人（签名）：{OWNER}", s["body"]),
        p("日期：_______年____月____日", s["body"]),
    ]
    path = OUTPUT_DIR / f"{SOFTWARE_NAME}{VERSION}-新增版本说明.pdf"
    doc = BriefTemplate(
        str(path),
        f"{SOFTWARE_NAME} {VERSION}｜新增版本说明",
        leftMargin=20 * mm,
        rightMargin=20 * mm,
        topMargin=20 * mm,
        bottomMargin=18 * mm,
        title=f"{SOFTWARE_NAME}{VERSION}新增版本说明",
        author=OWNER,
    )
    doc.build(story)
    return path


def main() -> None:
    register_fonts()
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for output in (build_application_brief(), build_version_statement()):
        print(output)


if __name__ == "__main__":
    main()
