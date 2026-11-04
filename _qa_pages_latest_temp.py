from pypdf import PdfReader
p=r"D:\GAME\GalaxyStrike\_report_qa_final\final_word.pdf"
r=PdfReader(p)
for i,page in enumerate(r.pages,1):
 t=(page.extract_text() or '').replace('\r','')
 lines=[x.strip() for x in t.splitlines() if x.strip()]
 if i==3: print('TOC', ' | '.join(lines[:11]))
 for h in ['CHƯƠNG 1.','CHƯƠNG 2.','CHƯƠNG 3.','CHƯƠNG 4.','CHƯƠNG 5.','CHƯƠNG 6.','CHƯƠNG 7.','TÀI LIỆU THAM KHẢO']:
  if i>3 and h in t: print('BODY',h,'=',next((x for x in lines if x.startswith('Trang ')),''))
 print(f"PAGE {i}: "+" | ".join(lines[:3])[:160])
