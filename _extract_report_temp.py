from docx import Document
from pathlib import Path
p=Path(r"F:\PTIT\NĂM 3\ltgcb\Bao_cao_Game_Galaxy_Strike.docx")
d=Document(p)
out=[]
out.append(f"PARAGRAPHS={len(d.paragraphs)} TABLES={len(d.tables)} INLINE_SHAPES={len(d.inline_shapes)} SECTIONS={len(d.sections)}")
for i,s in enumerate(d.sections):
 out.append(f"SECTION {i+1}: size={s.page_width/914400:.2f}x{s.page_height/914400:.2f}in margins={s.top_margin/914400:.2f},{s.bottom_margin/914400:.2f},{s.left_margin/914400:.2f},{s.right_margin/914400:.2f}in")
for i,p in enumerate(d.paragraphs):
 t=p.text.strip()
 if t:
  out.append(f"P{i:04d} [{p.style.name}] {t}")
for ti,t in enumerate(d.tables):
 out.append(f"TABLE {ti+1} {len(t.rows)}x{len(t.columns)}")
 for row in t.rows:
  out.append(" | ".join(c.text.replace("\n"," / ") for c in row.cells))
Path(r"D:\GAME\GalaxyStrike\_report_original_extracted.txt").write_text("\n".join(out),encoding="utf-8")
print("\n".join(out[:100]))
print("... extraction saved ...")
