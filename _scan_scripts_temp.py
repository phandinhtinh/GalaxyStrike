from pathlib import Path
import re, collections
root=Path(r"D:\GAME\GalaxyStrike")
for f in sorted((root/'Assets/Scripts').rglob('*.cs')):
 s=f.read_text(encoding='utf-8-sig',errors='replace')
 classes=re.findall(r'^\s*(?:public|internal|private|protected)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(?:class|interface|enum|struct)\s+\w+',s,re.M)
 fields=re.findall(r'^\s*\[(?:SerializeField|Header|Tooltip|Range|Min)[^\]]*\]\s*\n\s*(?:public|private|protected)\s+[^;=]+(?:=\s*[^;]+)?;',s,re.M)
 methods=re.findall(r'^\s*(?:public|private|protected|internal)\s+(?:static\s+|async\s+|virtual\s+|override\s+|IEnumerator\s+)*[\w<>\[\],.?]+\s+\w+\s*\([^;\n]*\)',s,re.M)
 print(f"{f.relative_to(root)} | {len(s.splitlines())} lines | classes={'; '.join(classes)}")
 print('  methods:', '; '.join(m.strip() for m in methods[:16]))
