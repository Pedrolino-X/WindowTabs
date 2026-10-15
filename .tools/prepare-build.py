from pathlib import Path
import re
root=Path(__file__).resolve().parent.parent
project=(root/'WtProgram/WtProgram.fsproj').read_text(encoding='utf-8-sig')
for name in ['Shared/DarkMode.fs','ManagerViewService/Views/ProgramView.fs','TabStripDecorator.fs','ManagerViewService/Views/BehaviorView.fs']:
 src=root/'WtProgram'/name
 s=src.read_text(encoding='utf-8-sig').replace('member _.','member __.').replace('override _.','override __.')
 if name=='Shared/DarkMode.fs':s=s.replace('type private COMBOBOXINFO','type COMBOBOXINFO')
 if name=='TabStripDecorator.fs':
  for label in ['Left','Right','Up','Down']:s=s.replace('then Localization.getString("'+label+'")','then yield Localization.getString("'+label+'")')
 if name.endswith('BehaviorView.fs'):
  s=re.sub(r'setValue\(([^,\n]+), value\)',r'setValue(\1, box value)',s)
  s=re.sub(r'setValue\(([^,\n]+), ("[^"]*")\)',r'setValue(\1, box \2)',s)
 dest=root/'.tools/compat'/src.name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(s,encoding='utf-8-sig')
 for form in [name,name.replace('/','\\')]:project=project.replace('Include="'+form+'"','Include="..\\.tools\\compat\\'+src.name+'"')
(root/'WtProgram/WtProgram.compat.fsproj').write_text(project,encoding='utf-8-sig')
