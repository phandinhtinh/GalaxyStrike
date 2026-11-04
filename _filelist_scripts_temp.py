from pathlib import Path
root=Path(r"D:\GAME\GalaxyStrike")
for i,p in enumerate(sorted((root/'Assets/Scripts').rglob('*.cs'))):
 print(f'{p.relative_to(root)}')
