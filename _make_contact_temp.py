from pathlib import Path
from zipfile import ZipFile
from PIL import Image, ImageOps, ImageDraw, ImageFont
root=Path(r"D:\GAME\GalaxyStrike")
out=root/'_report_qa_images'; out.mkdir(exist_ok=True)
with ZipFile(r"F:\PTIT\NĂM 3\ltgcb\Bao_cao_Game_Galaxy_Strike.docx") as z:
 for n in z.namelist():
  if n.startswith('word/media/'):
   p=out/Path(n).name; p.write_bytes(z.read(n)); print('DOCX image',p.name,p.stat().st_size)
files=list((root/'Documentation_Assets').glob('*.png'))+list((root/'Assets/Screenshots').glob('*.png'))+list(out.glob('*.png'))
selected=[]
for key in ['ptit_logo','playfield_space','player_hero','enemy_types_grid','boosters_diagram','level_01_preview','difficulty_curve_50_levels','panel_setting','icon_setting','screenshot-20261028-215522']:
 for p in files:
  if key.lower() in p.name.lower() and p not in selected: selected.append(p)
thumbs=[]
for p in selected:
 im=Image.open(p).convert('RGB'); im.thumbnail((360,220))
 canvas=Image.new('RGB',(390,275),'white'); canvas.paste(im,((390-im.width)//2,12))
 d=ImageDraw.Draw(canvas); d.text((10,240),p.name,fill='black')
 thumbs.append(canvas)
sheet=Image.new('RGB',(390*2,275*((len(thumbs)+1)//2)),(225,225,225))
for i,im in enumerate(thumbs): sheet.paste(im,((i%2)*390,(i//2)*275))
sheet.save(out/'contact_sheet.jpg',quality=90)
print('CONTACT',out/'contact_sheet.jpg',len(selected))
