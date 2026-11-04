from pypdf import PdfReader
p=r"D:\GAME\GalaxyStrike\_report_qa_final\final_word.pdf"
r=PdfReader(p)
for i,page in enumerate(r.pages,1):
 t=(page.extract_text() or '').replace('\r','')
 lines=[x.strip() for x in t.splitlines() if x.strip()]
 print(f"PAGE {i}: "+" | ".join(lines[:3])[:170])
 if i>=4:
  print('BODY PAGE LABEL',next((x for x in lines if x.startswith('Trang ')),''))
