from pathlib import Path
from PIL import Image, ImageDraw
folder=Path(r"D:\GAME\GalaxyStrike\_report_qa_final")
files=sorted(folder.glob('page-*.png'),key=lambda p:int(p.stem.split('-')[1]))
thumb_w,thumb_h=260,368
cols=4
rows=(len(files)+cols-1)//cols
sheet=Image.new('RGB',(cols*thumb_w,rows*(thumb_h+30)),(220,220,220))
d=ImageDraw.Draw(sheet)
for i,p in enumerate(files):
 im=Image.open(p).convert('RGB'); im.thumbnail((thumb_w-12,thumb_h-10))
 x=(i%cols)*thumb_w+(thumb_w-im.width)//2; y=(i//cols)*(thumb_h+30)+5
 sheet.paste(im,(x,y)); d.text(((i%cols)*thumb_w+8,(i//cols)*(thumb_h+30)+thumb_h+5),f'Page {i+1}',fill='black')
sheet.save(folder/'contact.jpg',quality=92)
print(len(files),sheet.size)
