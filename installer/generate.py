from pathlib import Path
import hashlib, uuid, xml.etree.ElementTree as E
r=Path(__file__).resolve().parents[1];payload=r/'portable'
ns='http://wixtoolset.org/schemas/v4/wxs';ui='http://wixtoolset.org/schemas/v4/wxs/ui'
E.register_namespace('',ns);E.register_namespace('ui',ui)
def add(parent,tag,**a):return E.SubElement(parent,'{'+ns+'}'+tag,{k:str(v) for k,v in a.items()})
w=E.Element('{'+ns+'}Wix');pkg=add(w,'Package',Name='Afterglow',Manufacturer='Afterglow contributors',Version='$(var.ProductVersion)',UpgradeCode='018E9D01-E203-4E8F-8F95-CA7B687199AA',Scope='perUser',InstallerVersion='500',Compressed='yes')
add(pkg,'MajorUpgrade',Schedule='afterInstallInitialize',DowngradeErrorMessage='A newer version of Afterglow is already installed.')
add(pkg,'MediaTemplate',EmbedCab='yes',CompressionLevel='high')
add(pkg,'Property',Id='ARPPRODUCTICON',Value='AfterglowIcon')
saved=add(pkg,'Property',Id='SAVEDINSTALLFOLDER',Secure='yes');add(saved,'RegistrySearch',Id='RememberInstallLocation',Root='HKCU',Key=r'Software\Afterglow\Installer',Name='InstallPath',Type='raw',Bitness='always64')
add(pkg,'SetProperty',Id='INSTALLFOLDER',Value='[SAVEDINSTALLFOLDER]',Before='CostFinalize',Sequence='both',Condition='SAVEDINSTALLFOLDER AND NOT INSTALLFOLDER')
add(pkg,'Icon',Id='AfterglowIcon',SourceFile='$(var.ProjectDir)/src/Afterglow/afterglow.ico')
build=add(pkg,'Property',Id='WINDOWSBUILD');add(build,'RegistrySearch',Id='WindowsBuild',Root='HKLM',Key=r'SOFTWARE\Microsoft\Windows NT\CurrentVersion',Name='CurrentBuildNumber',Type='raw',Bitness='always64')
add(pkg,'Launch',Condition='Installed OR (VersionNT64 AND WINDOWSBUILD >= 17763)',Message='Afterglow requires 64-bit Windows 10 version 1809 or later.')
local=add(pkg,'StandardDirectory',Id='LocalAppDataFolder');programs=add(local,'Directory',Id='UserPrograms',Name='Programs');dest=add(programs,'Directory',Id='INSTALLFOLDER',Name='Afterglow')
menu=add(pkg,'StandardDirectory',Id='ProgramMenuFolder');add(menu,'Directory',Id='AfterglowMenu',Name='Afterglow');add(pkg,'StandardDirectory',Id='DesktopFolder')
core=add(pkg,'Feature',Id='Core',Title='Afterglow',Level='1');desktop=add(pkg,'Feature',Id='DesktopShortcutFeature',Title='Desktop shortcut',Level='1')
dirs={'':dest}
for f in sorted(payload.rglob('*')):
 if not f.is_file() or f.suffix in {'.pdb','.log'}:continue
 rel=f.relative_to(payload);parent=dest;current=Path()
 for part in rel.parts[:-1]:
  current=current/part;s=current.as_posix()
  if s not in dirs:dirs[s]=add(parent,'Directory',Id='D'+hashlib.sha256(s.encode()).hexdigest()[:18],Name=part)
  parent=dirs[s]
 ident='C'+hashlib.sha256(rel.as_posix().encode()).hexdigest()[:18]
 cmp=add(parent,'Component',Id=ident,Guid=str(uuid.uuid5(uuid.UUID('018E9D01-E203-4E8F-8F95-CA7B687199AA'),rel.as_posix())),Bitness='always64')
 add(cmp,'File',Id='AfterglowExe' if rel.as_posix()=='Afterglow.exe' else 'F'+ident[1:],Source='$(var.PayloadDir)/'+rel.as_posix(),Name=f.name)
 add(cmp,'RegistryValue',Root='HKCU',Key=r'Software\Afterglow\Installer',Name=ident,Value='[INSTALLFOLDER]',Type='string',KeyPath='yes');add(core,'ComponentRef',Id=ident)
cleanup=add(pkg,'Component',Id='DirectoryCleanup',Guid=str(uuid.uuid5(uuid.UUID('018E9D01-E203-4E8F-8F95-CA7B687199AA'),'DirectoryCleanup')),Directory='INSTALLFOLDER')
add(cleanup,'RegistryValue',Root='HKCU',Key=r'Software\Afterglow\Installer',Name='Directories',Value='1',Type='integer',KeyPath='yes')
for i,directory in enumerate(dirs.values()):add(cleanup,'RemoveFolder',Id='CleanDirectory'+str(i),Directory=directory.attrib['Id'],On='uninstall')
add(cleanup,'RegistryValue',Root='HKCU',Key=r'Software\Afterglow\Installer',Name='InstallPath',Value='[INSTALLFOLDER]',Type='string')
add(core,'ComponentRef',Id='DirectoryCleanup')
for component,directory,name,feature in [('MenuShortcut','AfterglowMenu','Afterglow',core),('DesktopShortcut','DesktopFolder','Afterglow Studio',desktop)]:
 cmp=add(pkg,'Component',Id=component,Guid='*',Directory=directory)
 add(cmp,'Shortcut',Id=component+'Link',Name=name,Target='[#AfterglowExe]',WorkingDirectory='INSTALLFOLDER',Icon='AfterglowIcon',Description='YouTube Music wallpapers and synced lyrics')
 add(cmp,'RegistryValue',Root='HKCU',Key=r'Software\Afterglow\Installer',Name=component,Value='1',Type='integer',KeyPath='yes')
 if directory=='AfterglowMenu':add(cmp,'RemoveFolder',Id='RemoveMenu',On='uninstall')
 add(feature,'ComponentRef',Id=component)
add(pkg,'CustomAction',Id='CleanupAfterglow',FileRef='AfterglowExe',ExeCommand='--installer-cleanup',Execute='immediate',Return='ignore',Impersonate='yes')
add(pkg,'CustomAction',Id='ExitForUpgrade',FileRef='AfterglowExe',ExeCommand='--request-exit',Execute='immediate',Return='ignore',Impersonate='yes')
seq=add(pkg,'InstallExecuteSequence');add(seq,'Custom',Action='CleanupAfterglow',Before='RemoveFiles',Condition='REMOVE="ALL" AND NOT UPGRADINGPRODUCTCODE');add(seq,'Custom',Action='ExitForUpgrade',Before='CleanupAfterglow',Condition='REMOVE="ALL" AND UPGRADINGPRODUCTCODE')
E.SubElement(pkg,'{'+ui+'}WixUI',{'Id':'WixUI_InstallDir','InstallDirectory':'INSTALLFOLDER'})
add(pkg,'Property',Id='WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT',Value='Open Afterglow and set up your browser')
add(pkg,'Property',Id='WIXUI_EXITDIALOGOPTIONALCHECKBOX',Value='1')
add(pkg,'CustomAction',Id='LaunchAfterglow',FileRef='AfterglowExe',ExeCommand='',Execute='immediate',Return='asyncNoWait',Impersonate='yes')
launchUi=add(pkg,'UI');add(launchUi,'Publish',Dialog='ExitDialog',Control='Finish',Event='DoAction',Value='LaunchAfterglow',Order='1',Condition='WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 AND NOT Installed')

add(pkg,'WixVariable',Id='WixUIBannerBmp',Value='$(var.ProjectDir)/installer/banner.bmp')
add(pkg,'WixVariable',Id='WixUIDialogBmp',Value='$(var.ProjectDir)/installer/dialog.bmp')
add(pkg,'WixVariable',Id='WixUILicenseRtf',Value='$(var.ProjectDir)/installer/license.rtf')
E.indent(w);E.ElementTree(w).write(r/'installer/Afterglow.wxs',encoding='utf-8',xml_declaration=True)
license=(r/'LICENSE').read_text(encoding='utf-8').replace('\\','\\\\').replace('{','\\{').replace('}','\\}').replace('\n','\\par\n')
(r/'installer/license.rtf').write_text('{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Segoe UI;}}\\fs20 '+license+'}',encoding='utf-8')
