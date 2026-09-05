from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile

from docx import Document
from pypdf import PdfReader


ROOT = Path(r"D:\AppleMusicDesktopLyrics\output\software-copyright-correction-v2.0.36")
DOCX = ROOT / "working" / "桌面歌词岛软件V2.0.36-软件说明书.docx"
PDF = ROOT / "桌面歌词岛软件V2.0.36-软件说明书.pdf"


def digest(path: Path) -> str:
    return sha256(path.read_bytes()).hexdigest().upper()


with ZipFile(DOCX) as package:
    names = package.namelist()
    image_count = sum(name.startswith("word/media/") for name in names)
    document_xml = package.read("word/document.xml").decode("utf-8")
    alt_count = document_xml.count("descr=")

reader = PdfReader(str(PDF))
page_text = [(page.extract_text() or "").strip() for page in reader.pages]
pdf_text = "\n".join(page_text)
doc = Document(DOCX)
paragraph_text = [paragraph.text.strip() for paragraph in doc.paragraphs]
docx_text = "\n".join(paragraph_text)
required = [
    "桌面歌词岛软件",
    "V2.0.36",
    "v2.0.36 Beta",
    "13,108",
    "21,523",
    "439f35a7c2ffec426b7f11bf3fd57288ea4d1a0a",
    "附录 B. 主要功能与申请表对应关系",
]
forbidden = [
    "完整补正稿",
    "含完整运行界面、操作流程和运行数据",
    "整体替换原提交的软件说明书",
    "文档不足 60 页",
    "提交时应上传全部页面",
]
required_sections = [f"{index}. " for index in range(1, 17)]
figure_captions = [text for text in paragraph_text if text.startswith("图 ")]

print(f"DOCX bytes={DOCX.stat().st_size} sha256={digest(DOCX)}")
print(f"DOCX images={image_count} alt_descriptions={alt_count}")
print(f"PDF bytes={PDF.stat().st_size} sha256={digest(PDF)} pages={len(reader.pages)}")
print(f"PDF nonempty_pages={sum(bool(text) for text in page_text)}")
for token in required:
    print(f"required[{token}]={token in pdf_text}")
print(f"sections_1_to_16={all(any(text.startswith(prefix) for text in paragraph_text) for prefix in required_sections)}")
print(f"figure_captions={figure_captions}")
print(f"supplement_only_wording={'共同构成' in docx_text}")
for token in forbidden:
    print(f"forbidden[{token}]={token in pdf_text}")

assert image_count == 9
assert alt_count >= 9
assert len(reader.pages) == 27
assert all(page_text)
assert all(abs(float(page.mediabox.width) - 595.2) < 1 and abs(float(page.mediabox.height) - 841.92) < 1 for page in reader.pages)
assert all(token in pdf_text for token in required)
assert all(any(text.startswith(prefix) for text in paragraph_text) for prefix in required_sections)
assert len(figure_captions) == 9
assert [caption.split()[1] for caption in figure_captions] == [str(index) for index in range(1, 10)]
assert "共同构成" not in docx_text
assert all(token not in pdf_text for token in forbidden)
