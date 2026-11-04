from pypdf import PdfReader
r=PdfReader(r"D:\GAME\GalaxyStrike\_report_qa_final\final_word.pdf")
for i,p in enumerate(r.pages,1):
 t=p.extract_text() or ''; lines=[x.strip() for x in t.splitlines() if x.strip()]
 print(f"{i}: "+" | ".join(lines[:3])[:160])
 if i==3: print('TOC:', ' | '.join(lines[:10]))
