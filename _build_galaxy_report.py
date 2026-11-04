from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from docx import Document
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_TAB_ALIGNMENT, WD_TAB_LEADER
from docx.enum.section import WD_SECTION
from docx.shared import Cm, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn


ROOT = Path(r"D:\GAME\GalaxyStrike")
WORK = ROOT / "_report_qa_images"
SOURCE_MEDIA = WORK
OUTPUT = Path(r"F:\PTIT\NĂM 3\ltgcb\Bao_cao_Game_Galaxy_Strike_Viet_lai.docx")
LOGO = SOURCE_MEDIA / "image2.png"
GAMEPLAY_IMAGE = SOURCE_MEDIA / "image5.png"
FLOW_IMAGE = WORK / "game_flow.png"
WORK.mkdir(exist_ok=True)


def set_run_font(run, name="Times New Roman", size=13, bold=None, italic=None, color="000000"):
    run.font.name = name
    r_pr = run._element.get_or_add_rPr()
    r_fonts = r_pr.rFonts
    if r_fonts is None:
        r_fonts = OxmlElement("w:rFonts")
        r_pr.insert(0, r_fonts)
    r_fonts.set(qn("w:ascii"), name)
    r_fonts.set(qn("w:hAnsi"), name)
    r_fonts.set(qn("w:eastAsia"), name)
    run.font.size = Pt(size)
    run.font.color.rgb = RGBColor.from_string(color)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic


def set_style_font(style, size=13, bold=False, color="000000"):
    style.font.name = "Times New Roman"
    r_pr = style._element.get_or_add_rPr()
    r_fonts = r_pr.rFonts
    if r_fonts is None:
        r_fonts = OxmlElement("w:rFonts")
        r_pr.insert(0, r_fonts)
    r_fonts.set(qn("w:ascii"), "Times New Roman")
    r_fonts.set(qn("w:hAnsi"), "Times New Roman")
    r_fonts.set(qn("w:eastAsia"), "Times New Roman")
    style.font.size = Pt(size)
    style.font.bold = bold
    style.font.color.rgb = RGBColor.from_string(color)


def add_page_field(paragraph):
    run = paragraph.add_run()
    set_run_font(run, size=10.5)
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instruction = OxmlElement("w:instrText")
    instruction.set(qn("xml:space"), "preserve")
    instruction.text = " PAGE "
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    text = OxmlElement("w:t")
    text.text = "1"
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    for item in (begin, instruction, separate, text, end):
        run._r.append(item)


def set_page_number_start(section, start=1):
    pg_num = section._sectPr.find(qn("w:pgNumType"))
    if pg_num is None:
        pg_num = OxmlElement("w:pgNumType")
        section._sectPr.append(pg_num)
    pg_num.set(qn("w:start"), str(start))


def set_cell_shading(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_margins(cell, top=100, start=120, bottom=100, end=120):
    tc = cell._tc
    tc_pr = tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for edge, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = tc_mar.find(qn(f"w:{edge}"))
        if node is None:
            node = OxmlElement(f"w:{edge}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_table_borders(table, color="D9D9D9", size="5"):
    tbl_pr = table._tbl.tblPr
    borders = tbl_pr.find(qn("w:tblBorders"))
    if borders is None:
        borders = OxmlElement("w:tblBorders")
        tbl_pr.append(borders)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        tag = qn(f"w:{edge}")
        element = borders.find(tag)
        if element is None:
            element = OxmlElement(f"w:{edge}")
            borders.append(element)
        element.set(qn("w:val"), "single")
        element.set(qn("w:sz"), size)
        element.set(qn("w:space"), "0")
        element.set(qn("w:color"), color)


def mark_repeat_table_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    tbl_header = OxmlElement("w:tblHeader")
    tbl_header.set(qn("w:val"), "true")
    tr_pr.append(tbl_header)


def mark_row_no_split(row):
    tr_pr = row._tr.get_or_add_trPr()
    cant_split = OxmlElement("w:cantSplit")
    tr_pr.append(cant_split)


def set_paragraph_language(paragraph):
    for run in paragraph.runs:
        r_pr = run._element.get_or_add_rPr()
        lang = r_pr.find(qn("w:lang"))
        if lang is None:
            lang = OxmlElement("w:lang")
            r_pr.append(lang)
        lang.set(qn("w:val"), "vi-VN")


def configure_section(section):
    section.page_width = Cm(21)
    section.page_height = Cm(29.7)
    section.top_margin = Cm(2.2)
    section.bottom_margin = Cm(2.0)
    section.left_margin = Cm(3.0)
    section.right_margin = Cm(2.0)
    section.header_distance = Cm(1.0)
    section.footer_distance = Cm(1.0)


def add_para(doc, text, bold_lead=None, italic=False, keep=False):
    p = doc.add_paragraph(style="Normal")
    if keep:
        p.paragraph_format.keep_with_next = True
    if bold_lead and text.startswith(bold_lead):
        r = p.add_run(bold_lead)
        set_run_font(r, bold=True)
        r = p.add_run(text[len(bold_lead):])
        set_run_font(r, italic=italic)
    else:
        r = p.add_run(text)
        set_run_font(r, italic=italic)
    set_paragraph_language(p)
    return p


def add_bullet(doc, text, level=0):
    p = doc.add_paragraph(style="Normal")
    p.paragraph_format.left_indent = Cm(0.85 + level * 0.45)
    p.paragraph_format.first_line_indent = Cm(-0.35)
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.line_spacing = 1.25
    r = p.add_run("• ")
    set_run_font(r, size=12.5)
    r = p.add_run(text)
    set_run_font(r, size=12.5)
    set_paragraph_language(p)
    return p


def add_numbered(doc, text):
    p = doc.add_paragraph(style="List Number")
    p.paragraph_format.left_indent = Cm(0.85)
    p.paragraph_format.first_line_indent = Cm(-0.45)
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.line_spacing = 1.25
    r = p.add_run(text)
    set_run_font(r, size=12.5)
    set_paragraph_language(p)
    return p


def add_heading(doc, text, level=1):
    p = doc.add_paragraph(style=f"Heading {level}")
    p.paragraph_format.keep_with_next = True
    p.paragraph_format.first_line_indent = Cm(0)
    r = p.add_run(text)
    sizes = {1: 16, 2: 14, 3: 13}
    set_run_font(r, size=sizes[level], bold=True)
    set_paragraph_language(p)
    return p


def add_caption(doc, text):
    p = doc.add_paragraph(style="Caption")
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.space_after = Pt(10)
    p.paragraph_format.keep_together = True
    r = p.add_run(text)
    set_run_font(r, size=10.5, italic=True)
    set_paragraph_language(p)
    return p


def add_table(doc, headers, rows, widths=None, font_size=10.5):
    table = doc.add_table(rows=1, cols=len(headers))
    table.autofit = False
    table.alignment = WD_ALIGN_PARAGRAPH.CENTER
    if widths:
        for col, width in zip(table.columns, widths):
            col.width = Cm(width)
    set_table_borders(table)
    header = table.rows[0]
    mark_repeat_table_header(header)
    mark_row_no_split(header)
    for i, value in enumerate(headers):
        cell = header.cells[i]
        if widths:
            cell.width = Cm(widths[i])
        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        set_cell_shading(cell, "D9EAF7")
        set_cell_margins(cell)
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.space_after = Pt(0)
        p.paragraph_format.line_spacing = 1.1
        r = p.add_run(value)
        set_run_font(r, size=font_size, bold=True)
    for row_values in rows:
        row = table.add_row()
        mark_row_no_split(row)
        for i, value in enumerate(row_values):
            cell = row.cells[i]
            if widths:
                cell.width = Cm(widths[i])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            set_cell_margins(cell)
            p = cell.paragraphs[0]
            p.paragraph_format.first_line_indent = Cm(0)
            p.paragraph_format.space_after = Pt(0)
            p.paragraph_format.line_spacing = 1.1
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT
            r = p.add_run(str(value))
            set_run_font(r, size=font_size)
            set_paragraph_language(p)
    spacer = doc.add_paragraph()
    spacer.paragraph_format.first_line_indent = Cm(0)
    spacer.paragraph_format.space_after = Pt(2)
    spacer.paragraph_format.space_before = Pt(2)
    spacer.paragraph_format.line_spacing = 0.6
    return table


def add_figure(doc, image_path, width_cm, alt):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_before = Pt(3)
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.keep_with_next = True
    shape = p.add_run().add_picture(str(image_path), width=Cm(width_cm))
    shape._inline.docPr.set("descr", alt)
    return p


def draw_flowchart(path):
    width, height = 1840, 510
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    regular_path = Path(r"C:\Windows\Fonts\arial.ttf")
    bold_path = Path(r"C:\Windows\Fonts\arialbd.ttf")
    font = ImageFont.truetype(str(regular_path), 35)
    small = ImageFont.truetype(str(regular_path), 29)
    note_font = ImageFont.truetype(str(regular_path), 25)
    title_font = ImageFont.truetype(str(bold_path), 37)
    draw.text((40, 25), "LUỒNG XỬ LÝ CHÍNH", font=title_font, fill="#17365D")
    labels = [
        "LoadingScene\nNạp HomeScene và\nchọn Campaign/Challenge",
        "GameScene\nKhởi tạo Level,\nPlayer và HUD",
        "Vòng chiến đấu\nDi chuyển, bắn,\nEnemySpawner tạo Wave",
        "GameManager\nVictory / Game Over\nNext Level / Home",
    ]
    box_w, box_h, gap = 395, 230, 38
    x0, y0 = 35, 105
    for i, label in enumerate(labels):
        x = x0 + i * (box_w + gap)
        draw.rounded_rectangle((x, y0, x + box_w, y0 + box_h), radius=18,
                               fill="#EEF5FC", outline="#315B86", width=4)
        lines = label.split("\n")
        line_heights = [draw.textbbox((0, 0), line, font=font)[3] for line in lines]
        total_h = sum(line_heights) + (len(lines) - 1) * 12
        yy = y0 + (box_h - total_h) // 2
        for j, line in enumerate(lines):
            f = font if j == 0 else small
            bbox = draw.textbbox((0, 0), line, font=f)
            tw = bbox[2] - bbox[0]
            draw.text((x + (box_w - tw) // 2, yy), line, font=f,
                      fill="#17365D" if j == 0 else "#1F2937")
            yy += (bbox[3] - bbox[1]) + 12
        if i < len(labels) - 1:
            start = (x + box_w + 4, y0 + box_h // 2)
            end = (x + box_w + gap - 6, y0 + box_h // 2)
            draw.line((start, end), fill="#315B86", width=5)
            ex, ey = end
            draw.polygon([(ex, ey), (ex - 13, ey - 9), (ex - 13, ey + 9)], fill="#315B86")
    draw.text((43, 380), "Các Wave lặp lại đến khi hết Enemy. Nếu Player hết HP, GameManager mở giao diện Game Over.",
              font=note_font, fill="#374151")
    image.save(path)


def add_toc_line(doc, label, page="00", level=0):
    p = doc.add_paragraph()
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.left_indent = Cm(0.55 if level else 0)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.line_spacing = 1.15
    p.paragraph_format.tab_stops.add_tab_stop(Cm(15.8 - (0.55 if level else 0)), WD_TAB_ALIGNMENT.RIGHT, WD_TAB_LEADER.DOTS)
    r = p.add_run(label)
    set_run_font(r, size=12.5, bold=(level == 0))
    r = p.add_run("\t" + page)
    set_run_font(r, size=12.5)
    return p


def new_chapter(doc, title, first=False):
    heading = add_heading(doc, title, 1)
    if not first:
        heading.paragraph_format.page_break_before = True
    return heading


if not LOGO.exists() or not GAMEPLAY_IMAGE.exists():
    raise FileNotFoundError("Thiếu hình logo hoặc ảnh gameplay đã trích từ báo cáo gốc.")
draw_flowchart(FLOW_IMAGE)

doc = Document()
doc.core_properties.title = "Báo cáo phát triển trò chơi Galaxy Strike"
doc.core_properties.subject = "Báo cáo môn Thiết kế cơ chế và màn chơi"
doc.core_properties.author = ""
doc.core_properties.last_modified_by = ""

normal = doc.styles["Normal"]
set_style_font(normal, 13)
normal.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
normal.paragraph_format.first_line_indent = Cm(1.0)
normal.paragraph_format.line_spacing = 1.5
normal.paragraph_format.space_after = Pt(6)
for style_name, size in (("Heading 1", 16), ("Heading 2", 14), ("Heading 3", 13)):
    style = doc.styles[style_name]
    set_style_font(style, size, True)
    style.paragraph_format.keep_with_next = True
    style.paragraph_format.first_line_indent = Cm(0)
    style.paragraph_format.space_before = Pt(11 if style_name == "Heading 1" else 8)
    style.paragraph_format.space_after = Pt(6)
    style.paragraph_format.line_spacing = 1.15
caption_style = doc.styles["Caption"]
set_style_font(caption_style, 10.5, False)
caption_style.paragraph_format.first_line_indent = Cm(0)
caption_style.paragraph_format.line_spacing = 1.1
for list_name in ("List Bullet", "List Number"):
    style = doc.styles[list_name]
    set_style_font(style, 12.5, False)

section1 = doc.sections[0]
configure_section(section1)

# Bìa
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.first_line_indent = Cm(0)
p.paragraph_format.space_after = Pt(2)
p.add_run().add_picture(str(LOGO), height=Cm(2.4))
for text, size, bold in [
    ("HỌC VIỆN CÔNG NGHỆ BƯU CHÍNH VIỄN THÔNG", 14, True),
    ("CHƯƠNG TRÌNH THIẾT KẾ VÀ PHÁT TRIỂN GAME", 12, True),
    ("THIẾT KẾ CƠ CHẾ VÀ MÀN CHƠI", 12, False),
]:
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_after = Pt(5)
    r = p.add_run(text)
    set_run_font(r, size=size, bold=bold)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.first_line_indent = Cm(0)
p.paragraph_format.space_before = Pt(34)
p.paragraph_format.space_after = Pt(8)
r = p.add_run("BÁO CÁO ĐỀ TÀI")
set_run_font(r, size=17, bold=True)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.first_line_indent = Cm(0)
p.paragraph_format.space_after = Pt(7)
r = p.add_run("PHÁT TRIỂN TRÒ CHƠI GALAXY STRIKE")
set_run_font(r, size=21, bold=True)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.first_line_indent = Cm(0)
p.paragraph_format.space_after = Pt(24)
r = p.add_run("Game bắn phi thuyền 2D bằng Unity và C#")
set_run_font(r, size=14, italic=True)
for label, value in [
    ("Giảng viên hướng dẫn", "Trần Duy Hiếu"),
    ("Lớp", "D24CQGA04-B"),
    ("Sinh viên thực hiện", "Phan Đình Tĩnh"),
]:
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.space_after = Pt(6)
    r = p.add_run(f"{label}: {value}")
    set_run_font(r, size=13)
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.first_line_indent = Cm(0)
p.paragraph_format.space_before = Pt(36)
r = p.add_run("Hà Nội, 2026")
set_run_font(r, size=13)

# Tóm tắt và mục lục, không đánh số trang
section2 = doc.add_section(WD_SECTION.NEW_PAGE)
configure_section(section2)
add_heading(doc, "TÓM TẮT ĐỀ TÀI", 1)
add_para(doc, "Galaxy Strike là trò chơi bắn phi thuyền 2D theo màn hình dọc. Người chơi điều khiển tàu chiến, né đạn và tiêu diệt các đợt kẻ địch. Trò chơi được xây dựng bằng Unity và C#, có các Scene riêng cho tải dữ liệu, màn hình chính và chiến đấu.")
add_para(doc, "Báo cáo trình bày yêu cầu, thiết kế, cấu trúc mã nguồn và các chức năng chính của dự án. Nội dung được đối chiếu với 68 tệp C#, ba Scene, prefab, ScriptableObject và cấu hình Unity trong thư mục dự án. Phần đánh giá phân biệt chức năng thể hiện trong mã nguồn với những nội dung còn cần xác nhận khi chạy trong Editor hoặc trên thiết bị.")
p = doc.add_paragraph()
p.paragraph_format.first_line_indent = Cm(0)
p.paragraph_format.space_before = Pt(4)
p.paragraph_format.space_after = Pt(12)
r = p.add_run("Từ khóa: ")
set_run_font(r, size=12, bold=True)
r = p.add_run("Unity, C#, game 2D, lập trình hướng đối tượng, quản lý Scene, xử lý va chạm.")
set_run_font(r, size=12, italic=True)
doc.add_page_break()
add_heading(doc, "MỤC LỤC", 1)
toc_entries = [
    ("CHƯƠNG 1. TỔNG QUAN ĐỀ TÀI", "1", 0),
    ("CHƯƠNG 2. PHÂN TÍCH YÊU CẦU VÀ GAMEPLAY", "2", 0),
    ("CHƯƠNG 3. THIẾT KẾ HỆ THỐNG", "3", 0),
    ("CHƯƠNG 4. HIỆN THỰC CÁC CHỨC NĂNG", "5", 0),
    ("CHƯƠNG 5. GIAO DIỆN VÀ TÀI NGUYÊN", "7", 0),
    ("CHƯƠNG 6. KIỂM TRA VÀ ĐÁNH GIÁ", "8", 0),
    ("CHƯƠNG 7. KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN", "9", 0),
    ("TÀI LIỆU THAM KHẢO", "10", 0),
]
for entry in toc_entries:
    add_toc_line(doc, *entry)

# Nội dung chính, bắt đầu đánh số trang từ 1
section3 = doc.add_section(WD_SECTION.NEW_PAGE)
configure_section(section3)
section3.footer.is_linked_to_previous = False
section3.different_first_page_header_footer = False
set_page_number_start(section3, 1)
footer_p = section3.footer.paragraphs[0]
footer_p.alignment = WD_ALIGN_PARAGRAPH.RIGHT
footer_p.paragraph_format.first_line_indent = Cm(0)
footer_p.paragraph_format.space_after = Pt(0)
footer_p.add_run("Trang ")
set_run_font(footer_p.runs[-1], size=10.5)
add_page_field(footer_p)

# Chương 1
new_chapter(doc, "CHƯƠNG 1. TỔNG QUAN ĐỀ TÀI", first=True)
add_heading(doc, "1.1. Bối cảnh và lý do chọn đề tài", 2)
add_para(doc, "Trò chơi bắn phi thuyền là một dạng game arcade có luật chơi dễ hiểu: di chuyển, tấn công và tránh va chạm. Đề tài Galaxy Strike được lựa chọn để vận dụng các kiến thức lập trình cơ bản vào một sản phẩm có hình ảnh, điều khiển, xử lý sự kiện và dữ liệu tiến trình.")
add_para(doc, "Trong dự án, mỗi nhân vật và hệ thống được biểu diễn bằng GameObject cùng các component. Các script C# điều khiển hành vi của đối tượng, còn Scene, Prefab và tài nguyên đồ họa được quản lý trong Unity Editor.")
add_heading(doc, "1.2. Mục tiêu", 2)
for item in [
    "Tổ chức một trò chơi 2D có màn hình chính, màn chơi và giao diện tải Scene.",
    "Lập trình điều khiển phi thuyền, bắn đạn, nhận sát thương và xử lý kết thúc màn.",
    "Tạo kẻ địch theo Wave, nhiều kiểu di chuyển và các cấp độ thử thách.",
    "Thực hành class, object, phương thức, danh sách, điều kiện, vòng lặp, sự kiện và va chạm 2D.",
    "Lưu tiến trình và tổ chức mã nguồn theo từng nhóm chức năng để thuận tiện bảo trì.",
]:
    add_bullet(doc, item)
add_heading(doc, "1.3. Phạm vi báo cáo", 2)
add_para(doc, "Báo cáo mô tả cấu trúc hiện có trong thư mục GalaxyStrike, tập trung vào ba Scene trong Build Settings, các script gameplay và giao diện, dữ liệu ScriptableObject, prefab, package và cấu hình dự án. Đây là phần mô tả kỹ thuật của project; các chức năng phụ thuộc tài khoản cửa hàng, dịch vụ trực tuyến hoặc thiết bị thật được nêu cùng điều kiện cần để xác nhận.")
add_heading(doc, "1.4. Công nghệ và môi trường", 2)
add_table(doc, ["Hạng mục", "Thông tin ghi nhận"], [
    ("Công cụ", "Unity và C#; mã nguồn sử dụng GameObject, component, prefab, ScriptableObject và UI Canvas."),
    ("Thể loại", "Game bắn phi thuyền 2D; có xử lý va chạm 2D và giao diện TextMeshPro."),
    ("Scene", "LoadingScene, HomeScene và GameScene đều được bật trong Build Settings."),
    ("Thiết bị", "Thiết kế hướng đến điện thoại; cấu hình cũng có thao tác chuột và bàn phím cho máy tính."),
], widths=[4.0, 12.0], font_size=10.5)

# Chương 2
new_chapter(doc, "CHƯƠNG 2. PHÂN TÍCH YÊU CẦU VÀ GAMEPLAY")
add_heading(doc, "2.1. Người dùng và mục đích", 2)
add_para(doc, "Người dùng chính là người chơi điều khiển một phi thuyền. Mục tiêu trong mỗi màn là sống sót, tiêu diệt đủ số kẻ địch và nhận phần thưởng. Người chơi có thể chọn Campaign để đi theo tiến trình hoặc Challenge để vào một màn được chọn ngẫu nhiên.")
add_heading(doc, "2.2. Yêu cầu chức năng", 2)
add_table(doc, ["Nhóm", "Yêu cầu chính"], [
    ("Điều khiển", "Di chuyển Player trong vùng chơi, bắn đạn và tránh va chạm với Enemy hoặc EnemyBullet."),
    ("Chiến đấu", "Sinh Enemy theo Wave; xử lý HP, sát thương, điểm, tiền thưởng và điều kiện thắng/thua."),
    ("Tiến trình", "Tạo dữ liệu màn chơi, chuyển màn tiếp theo, chơi lại và lưu mốc chiến dịch."),
    ("Màn hình chính", "Điều hướng đến Home, Shop, Upgrade, Map, Leaderboard, Collection, Backpack và nhiệm vụ."),
    ("Dịch vụ", "Lưu dữ liệu, điều chỉnh âm thanh; có mã nguồn cho IAP và Play Games với chế độ dự phòng khi dịch vụ chưa sẵn sàng."),
], widths=[3.4, 12.6])
add_heading(doc, "2.3. Vòng lặp chơi", 2)
add_para(doc, "Vòng chơi bắt đầu từ màn hình chính. Khi người chơi chọn chế độ và bắt đầu, GameScene khởi tạo Level, Player và HUD. Trong lúc chơi, Player di chuyển và tự động bắn; EnemySpawner tạo các Wave theo dữ liệu của LevelManager. Mỗi kẻ địch bị hạ làm thay đổi số lượng mục tiêu, điểm và phần thưởng. GameManager mở bảng Game Over khi Player hết máu hoặc bảng Victory khi số Enemy đã hạ đạt mục tiêu.")
add_heading(doc, "2.4. Điều khiển", 2)
add_bullet(doc, "Trên máy tính, PlayerController đọc thao tác nhấn và kéo chuột để di chuyển tàu.")
add_bullet(doc, "Các trục Horizontal và Vertical cho phép di chuyển bằng phím mũi tên hoặc WASD theo cấu hình Input Manager.")
add_bullet(doc, "Vị trí Player được giới hạn trong vùng chơi bằng các giá trị min/max theo trục tọa độ.")
add_para(doc, "Trong mã PlayerController được rà soát, nhánh nhập liệu trực tiếp dùng chuột và trục bàn phím. Cách nhận thao tác cảm ứng trên thiết bị cần được kiểm tra trong Unity và trên điện thoại trước khi kết luận hỗ trợ đầy đủ.")

# Chương 3
new_chapter(doc, "CHƯƠNG 3. THIẾT KẾ HỆ THỐNG")
add_heading(doc, "3.1. Tổ chức Scene", 2)
add_table(doc, ["Scene", "Vai trò", "Thành phần tiêu biểu"], [
    ("LoadingScene", "Hiển thị tiến trình khi tải Scene đích.", "LoadingSceneController, thanh tiến trình và thông báo tải."),
    ("HomeScene", "Màn hình chính, chọn chế độ và mở các khu vực chức năng.", "HomeSceneManager, ShopManager, UpgradePanelUI, BackpackUIController, LeaderboardPanelUI, TaskPopupController."),
    ("GameScene", "Màn chiến đấu và hiển thị kết quả.", "GameManager, LevelManager, EnemySpawner, PlayerController, HUD_Panel, PanelManager, VictoryPanelUI và GameOverPanelUI."),
], widths=[3.0, 5.4, 7.6])
add_heading(doc, "3.2. Luồng xử lý tổng quát", 2)
add_figure(doc, FLOW_IMAGE, 15.7, "Sơ đồ luồng từ tải Scene, chọn chế độ, chiến đấu, sinh đợt địch đến xử lý kết quả màn chơi.")
add_caption(doc, "Sơ đồ 1. Luồng xử lý chính của trò chơi")
add_para(doc, "GameBootstrap khởi tạo một số dịch vụ dùng chung trước khi nạp Scene. LoadingSceneController hỗ trợ chuyển Scene bất đồng bộ. Trong GameScene, EnemySpawner theo dõi số Enemy còn hoạt động để chuyển sang Wave kế tiếp; GameManager cập nhật trạng thái trận và gọi giao diện kết quả.")
add_heading(doc, "3.3. Cấu trúc mã nguồn", 2)
add_table(doc, ["Thư mục", "Nội dung"], [
    ("00_Core và 01_Data", "GameManager, LevelManager, dịch vụ dùng chung, model, enum và ScriptableObject."),
    ("02_Patterns", "Các thành phần Command, Factory, Object Pool, Singleton và State Machine."),
    ("03_Gameplay", "Player, Enemy, EnemySpawner, đạn, vật phẩm nhặt được và hiệu ứng nền."),
    ("04_UI và 90_Editor", "Giao diện màn chơi, màn hình chính, cửa hàng, nhiệm vụ, bảng xếp hạng, cài đặt và công cụ Editor."),
], widths=[3.1, 12.9])
add_heading(doc, "3.4. Trách nhiệm các lớp chính", 2)
add_table(doc, ["Lớp", "Trách nhiệm"], [
    ("PlayerController / Enemy", "Xử lý điều khiển, bắn, HP, di chuyển, tấn công, sát thương và phần thưởng."),
    ("EnemySpawner / LevelManager", "Sinh dữ liệu màn và Wave, tạo Enemy, theo dõi đợt địch."),
    ("GameManager / HUD_Panel", "Cập nhật điểm, HP, tiến trình màn, trạng thái thắng/thua và giao diện."),
    ("SaveManager / AudioManager", "Lưu dữ liệu người chơi và quản lý thiết lập âm thanh."),
    ("HomeSceneManager / ShopManager", "Điều hướng màn hình chính, chế độ chơi, cửa hàng và nâng cấp."),
    ("IapPurchaseService / LeaderboardService", "Kết nối giao dịch và bảng xếp hạng khi được cấu hình; có nhánh dự phòng."),
], widths=[4.2, 11.8])
add_heading(doc, "3.5. Dữ liệu và khái niệm tin học được vận dụng", 2)
add_para(doc, "SaveData, ShipDataSO và ItemDataSO tách dữ liệu người chơi, tàu và vật phẩm khỏi phần xử lý. WaveConfig lưu thông số một đợt Enemy; LevelData gom các Wave của một màn. List<T> chứa nhiều đối tượng cùng loại.")
add_bullet(doc, "Lớp và đối tượng: mỗi script mô tả dữ liệu và hành vi của một thành phần; Unity tạo các instance trên GameObject.")
add_bullet(doc, "Đóng gói và kế thừa: trường private có thể chỉnh trong Inspector bằng SerializeField; component thường kế thừa MonoBehaviour.")
add_bullet(doc, "Điều kiện, vòng lặp, danh sách và enum: dùng để kiểm tra trạng thái, sinh nhiều Enemy và chọn kiểu Wave hoặc đạn.")
add_bullet(doc, "Sự kiện và giao diện: dùng callback để báo thay đổi dữ liệu, Enemy bị hạ hoặc kết quả giao dịch.")
add_bullet(doc, "Lưu trữ: SaveManager chuyển SaveData thành JSON, mã hóa chuỗi và ghi vào PlayerPrefs. Project cũng có lớp Factory, Object Pool, Command và State Machine.")

# Chương 4
new_chapter(doc, "CHƯƠNG 4. HIỆN THỰC CÁC CHỨC NĂNG")
add_heading(doc, "4.1. Điều khiển Player và bắn đạn", 2)
add_para(doc, "PlayerController nhận đầu vào, tính vị trí mới rồi giới hạn vị trí đó trong vùng chơi. Khi Player còn hoạt động, hệ thống kiểm tra thời gian giữa các lần bắn và tạo đạn từ điểm bắn được gắn trong prefab. Cấp độ đạn có thể thay đổi khi nhặt vật phẩm Power-Up; các viên đạn gọi TakeDamage trên Enemy khi collider gặp mục tiêu có tag phù hợp.")
add_para(doc, "Khi Player nhận sát thương, hệ thống giảm HP, cập nhật HUD và có thể cấp thời gian bất tử ngắn để tránh nhận nhiều lần sát thương liên tiếp. Khi HP về 0, GameManager chuyển sang trạng thái Game Over.")
add_heading(doc, "4.2. Kẻ địch, đạn và va chạm", 2)
add_para(doc, "Enemy là thành phần dùng chung cho nhiều prefab. Hành vi được cấu hình theo kiểu di chuyển và kiểu bắn; mã nguồn có các mẫu bắn thẳng, nhắm mục tiêu, tỏa hình quạt, vòng tròn và xoắn ốc. EnemyBullet di chuyển theo hướng đã chọn và được thu hồi sau thời gian sống. HomingProjectile tìm Enemy gần nhất trong bán kính cấu hình rồi điều chỉnh hướng bay.")
add_para(doc, "Khi Enemy bị hạ, lớp Enemy thông báo sự kiện, cập nhật số lượng mục tiêu và cho phép xử lý điểm, tiền hoặc vật phẩm rơi. BulletPool lưu và tái sử dụng một số prefab đạn để giảm số lần tạo và hủy GameObject liên tục.")
add_heading(doc, "4.3. Wave và tiến trình màn chơi", 2)
add_para(doc, "LevelManager sinh dữ liệu cho tối đa 50 màn theo cấu hình mặc định. Cứ 10 màn tạo một khu vực; màn chia hết cho 5 nhưng không chia hết cho 10 được đánh dấu là Mini-Boss, còn màn chia hết cho 10 là Boss chính. Các Wave thường kết hợp kiểu đội hình, hướng xuất hiện, số lượng, tốc độ và mẫu đạn khác nhau. Các Wave đặc biệt được tạo riêng cho mốc Mini-Boss và Boss.")
add_para(doc, "Trong Campaign, tiến trình màn được cập nhật khi hoàn thành. Trong Challenge, cấp độ được chọn ngẫu nhiên trong giới hạn cấu hình và không ghi đè tiến trình chiến dịch. HomeSceneManager yêu cầu tiêu thụ năng lượng khi bắt đầu Campaign; nhánh Challenge bỏ qua bước tiêu thụ này.")
add_heading(doc, "4.4. Quản lý trận đấu và phần thưởng", 2)
add_para(doc, "GameManager giữ điểm, tiền, số Enemy đã hạ và HP tổng của màn. Mỗi lần Enemy bị tiêu diệt, bộ đếm được cập nhật. Khi đạt số lượng mục tiêu, GameManager lưu thành tích, cấp thưởng theo LevelData và hiển thị VictoryPanelUI. Khi Player hết máu, GameOverPanelUI hiển thị lựa chọn chơi lại hoặc quay về màn hình chính.")
add_heading(doc, "4.5. Lưu dữ liệu và dịch vụ", 2)
add_para(doc, "SaveManager là dịch vụ dùng chung giữa các Scene. SaveData chứa tiền, đá quý, năng lượng, tàu được mở khóa, cấp nâng cấp, tổng số Enemy đã hạ, điểm cao và tiến trình. Mã nguồn tuần tự hóa dữ liệu thành JSON, mã hóa chuỗi lưu cục bộ và có bước chuyển đổi một số dữ liệu PlayerPrefs cũ.")
add_para(doc, "AudioManager lưu và áp dụng âm lượng nhạc nền, hiệu ứng. IapPurchaseService có đường xử lý Unity IAP và đường mô phỏng khi cửa hàng chưa sẵn sàng; chế độ mô phỏng không được xem là giao dịch mua thật. LeaderboardService có nhánh offline; việc gửi điểm trực tuyến cần cấu hình Google Play Games và ID bảng xếp hạng hợp lệ.")
add_heading(doc, "4.6. Cửa hàng và nâng cấp", 2)
add_para(doc, "Màn hình chính có các tab Shop, Upgrade, Map và Leaderboard cùng giao diện Collection, Backpack và nhiệm vụ. SaveManager cung cấp thao tác mua tàu/vật phẩm, trừ tiền và nâng cấp các chỉ số tàu. Catalog ScriptableObject giúp lưu danh sách gói IAP; việc hiển thị gói không tự xác nhận sản phẩm đã được đăng ký trên cửa hàng thật.")

# Chương 5
new_chapter(doc, "CHƯƠNG 5. GIAO DIỆN VÀ TÀI NGUYÊN")
add_heading(doc, "5.1. Màn hình chiến đấu", 2)
add_para(doc, "Màn hình chiến đấu bố trí Player ở vùng dưới, Enemy ở phía trên và các chỉ số trận đấu trên HUD. HUD_Panel cập nhật Level, HP của Player và tỷ lệ HP tổng của đợt địch. Hai panel riêng trình bày trạng thái Game Over và kết quả Victory.")
add_figure(doc, GAMEPLAY_IMAGE, 4.9, "Ảnh chụp màn hình chiến đấu Galaxy Strike trong báo cáo nguồn.")
add_caption(doc, "Hình 1. Màn hình chiến đấu của Galaxy Strike")
add_heading(doc, "5.2. Màn hình chính và các tài nguyên", 2)
add_para(doc, "HomeScene chứa giao diện chọn Campaign hoặc Challenge, hiển thị tàu và tài nguyên, cùng các màn hình Shop, Upgrade, Map, Leaderboard, Collection, Backpack và nhiệm vụ. LoadingScene hiển thị tiến trình chuyển Scene. GameScene chứa nền cuộn, Player, EnemySpawner, HUD, Settings, Game Over và Victory.")
add_para(doc, "Dự án có prefab tàu địch, đạn, vật phẩm Power-Up, giao diện Settings và thẻ Shop. Các ScriptableObject gồm dữ liệu người chơi, dữ liệu tàu, vật phẩm và catalog IAP. Một số prefab Enemy có bản nằm ở cả Assets/Prefabs và Assets/Resources/Prefabs; cần xác định bản chuẩn khi chỉnh sửa để tránh cập nhật nhầm tài nguyên không được nạp.")

# Chương 6
new_chapter(doc, "CHƯƠNG 6. KIỂM TRA VÀ ĐÁNH GIÁ")
add_heading(doc, "6.1. Phương pháp rà soát", 2)
add_para(doc, "Phần đánh giá trong báo cáo được thực hiện bằng cách đọc mã nguồn C#, cấu hình project, Scene, prefab và ScriptableObject. Không có tệp kiểm thử tự động trong project. Các ca ở bảng dưới là kịch bản cần chạy trong Unity Editor và trên thiết bị; báo cáo không gán kết quả đạt khi chưa có lần chạy kiểm thử tương ứng.")
add_heading(doc, "6.2. Kịch bản kiểm thử đề xuất", 2)
add_table(doc, ["Ca kiểm thử", "Thao tác", "Kết quả cần quan sát"], [
    ("Bắt đầu và di chuyển", "Chọn Campaign, nhấn Play rồi kéo Player đến cạnh vùng chơi.", "GameScene được mở; Player không vượt khỏi vùng đã cấu hình."),
    ("Đạn, va chạm và Wave", "Cho hai loại đạn chạm mục tiêu; tiêu diệt hết Enemy trong Wave.", "HP cập nhật và Wave kế tiếp xuất hiện sau độ trễ."),
    ("Thắng và thua", "Hoàn thành mục tiêu; sau đó thử để Player hết HP.", "Mỗi trạng thái hiển thị đúng panel kết quả."),
    ("Lưu tiến trình", "Kiếm tiền, nâng cấp, thoát và mở lại trò chơi.", "Dữ liệu lưu được đọc lại và hiển thị nhất quán."),
    ("Mua và bảng xếp hạng", "Thử trên môi trường có cấu hình cửa hàng/tài khoản.", "Chỉ báo thành công khi dịch vụ thật phản hồi; chế độ mô phỏng phải được nhận diện riêng."),
], widths=[3.0, 5.4, 7.6], font_size=10.0)
add_heading(doc, "6.3. Các điểm cần xác nhận trước khi bàn giao", 2)
for item in [
    "Thống nhất phiên bản Unity: thông tin AGENTS.md là 2022.3.62f2, còn ProjectSettings/ProjectVersion.txt là 6000.3.27f1.",
    "Kiểm tra thao tác cảm ứng, hướng màn hình và luồng chơi trên điện thoại; mã điều khiển hiện đọc chuột và trục bàn phím.",
    "Đối chiếu ID sản phẩm IAP, cấu hình Google Play Games và ID bảng xếp hạng với dịch vụ thật.",
    "Rà lại các prefab trùng giữa Assets/Prefabs và Assets/Resources/Prefabs để xác định nơi chỉnh sửa chính.",
]:
    add_bullet(doc, item)
add_heading(doc, "6.4. Nhận xét", 2)
add_para(doc, "Project phân chia dữ liệu, gameplay, dịch vụ và giao diện; có hệ thống tiến trình, cửa hàng, nâng cấp, nhiệm vụ và bảng xếp hạng. Các chức năng trực tuyến và đầu vào thiết bị cần được xác nhận bằng chạy thử.")

# Chương 7
new_chapter(doc, "CHƯƠNG 7. KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN")
add_heading(doc, "7.1. Kết luận", 2)
add_para(doc, "Galaxy Strike là project game bắn phi thuyền 2D được tổ chức bằng Unity và C#. Mã nguồn hiện có các thành phần cho điều khiển Player, bắn đạn, sinh Enemy theo Wave, quản lý Level, giao diện thắng/thua và lưu tiến trình. Dự án cũng cung cấp các Scene và nhóm giao diện hỗ trợ màn hình chính, nâng cấp, cửa hàng, nhiệm vụ và bảng xếp hạng.")
add_para(doc, "Quá trình xây dựng project giúp vận dụng kiến thức tin học cơ sở vào sản phẩm trực quan: chia bài toán thành chức năng nhỏ, biểu diễn dữ liệu bằng kiểu và lớp, dùng điều kiện/vòng lặp để xử lý logic, dùng sự kiện để phối hợp thành phần và dùng cấu trúc lưu trữ để giữ tiến trình người chơi.")
add_heading(doc, "7.2. Hướng phát triển", 2)
for item in [
    "Đối chiếu phiên bản Unity và cấu hình màn hình trước khi tạo bản build chính thức.",
    "Hoàn thiện kiểm thử thủ công trên Editor và Android; ghi lại kết quả, ảnh chụp và cấu hình thiết bị.",
    "Xác nhận cấu hình IAP, Play Games, leaderboard và trải nghiệm ngoại tuyến.",
    "Chuẩn hóa vị trí prefab để tránh trùng bản trong thư mục Resources và thư mục thông thường.",
    "Bổ sung kiểm thử cho chuyển Level, điều kiện kết thúc Wave, lưu/đọc dữ liệu và trạng thái UI.",
]:
    add_bullet(doc, item)

references_heading = add_heading(doc, "TÀI LIỆU THAM KHẢO", 1)
references_heading.paragraph_format.page_break_before = True
for item in [
    "Project GalaxyStrike: Assets/Scenes và Assets/Scripts, nguồn mô tả cách tổ chức Scene và chức năng trong báo cáo.",
    "Project GalaxyStrike: Assets/Prefabs, Assets/Resources/Prefabs và Assets/ScriptableObject, dữ liệu prefab và cấu hình trò chơi.",
    "Project GalaxyStrike: Packages/manifest.json, ProjectSettings/EditorBuildSettings.asset, ProjectSettings/ProjectVersion.txt và ProjectSettings/ProjectSettings.asset.",
    "Tài liệu Unity User Manual và Unity Scripting API, dùng để đối chiếu khái niệm Scene, GameObject, component, prefab và vòng đời MonoBehaviour.",
]:
    add_numbered(doc, item)

# Áp dụng ngôn ngữ và giữ tiêu đề với nội dung kế tiếp.
for p in doc.paragraphs:
    set_paragraph_language(p)
    p.paragraph_format.widow_control = True

doc.save(OUTPUT)
print(f"Created: {OUTPUT}")
print(f"Paragraphs: {len(doc.paragraphs)}; tables: {len(doc.tables)}; figures: {len(doc.inline_shapes)}")

