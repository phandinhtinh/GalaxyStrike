from pathlib import Path
from PIL import Image,ImageDraw
folder=Path(r"D:\GAME\GalaxyStrike\_report_qa_final")
files=sorted(folder.glob('finalqa-*.png'),key=lambda p:int(p.stem.split('-')[1]))
w,h,cols=270,382,4
rows=(len(files)+cols-1)//cols
sheet=Image.new('RGB',(w*cols,(h+26)*rows),(220,220,220)); d=ImageDraw.Draw(sheet)
for i,p in enumerate(files):
 im=Image.open(p).convert('RGB'); im.thumbnail((w-10,h-8))
 x=(i%cols)*w+(w-im.width)//2;y=(i//cols)*(h+26)+4
 sheet.paste(im,(x,y));d.text(((i%cols)*w+8,(i//cols)*(h+26)+h),f'Page {i+1}',fill='black')
sheet.save(folder/'final_contact.jpg',quality=92)
print('pages',len(files),'sheet',sheet.size)
