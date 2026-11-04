from pathlib import Path
import re
root=Path(r"D:\GAME\GalaxyStrike")
guid_to_script={}
for p in (root/'Assets/Scripts').rglob('*.cs'):
 m=re.search(r'guid: ([0-9a-f]{32})',Path(str(p)+'.meta').read_text(errors='ignore')) if Path(str(p)+'.meta').exists() else None
 if m: guid_to_script[m.group(1)]=str(p.relative_to(root))
for scene in (root/'Assets/Scenes').glob('*.unity'):
 text=scene.read_text(encoding='utf-8-sig',errors='replace')
 docs=re.split(r'(?m)(?=^--- !u!\d+ &\d+)',text)
 go={}; scripts=[]
 for d in docs:
  h=re.match(r'--- !u!(\d+) &(\d+)',d)
  if not h: continue
  typ,oid=h.groups()
  if typ=='1':
   n=re.search(r'(?m)^  m_Name: (.*)$',d)
   go[oid]=n.group(1).strip() if n else ''
  elif typ=='114':
   game=re.search(r'm_GameObject: \{fileID: (\d+)\}',d)
   guid=re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]{32})',d)
   if game and guid: scripts.append((game.group(1),guid_to_script.get(guid.group(1),'external')))
 print('\nSCENE',scene.name,'GAMEOBJECTS',len(go),'SCRIPT_COMPONENTS',len(scripts))
 for gid,name in go.items():
  attached=[s for g,s in scripts if g==gid]
  if attached: print(f'{name}: {", ".join(attached)}')
print('\nProject version:',(root/'ProjectSettings/ProjectVersion.txt').read_text().strip())
print('User project metadata:',(root/'AGENTS.md').read_text(encoding='utf-8-sig'))
print('Tests files:',[str(p.relative_to(root)) for p in root.rglob('*Test*.cs') if all(x not in p.parts for x in ['Library','obj','.vs'])])
