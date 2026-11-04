from pypdf import PdfReader
p=r"D:\GAME\GalaxyStrike\_report_qa_final\final_word.pdf"
r=PdfReader(p)
for i,page in enumerate(r.pages,1):
 t=(page.extract_text() or '').replace('\r','')
 lines=[x.strip() for x in t.splitlines() if x.strip()]
 print(f"PAGE {i}: "+" | ".join(lines[:5])[:280])
 for h in ["CHƯƠNG 1.","CHƯƠNG 2.","CHƯƠNG 3.","CHƯƠNG 4.","CHƯƠNG 5.","CHƯƠNG 6.","CHƯƠNG 7.","TÀI LIỆU THAM KHẢO"]:
  if h in t:
   print('  HAS',h)
