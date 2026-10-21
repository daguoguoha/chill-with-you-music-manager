"""Offline regression. Supply a .NET Framework Newtonsoft.Json.dll (NuGet lib/net45)."""
from pathlib import Path
import argparse, subprocess, tempfile, shutil, os, json

parser=argparse.ArgumentParser()
parser.add_argument('--json', required=True, type=Path)
args=parser.parse_args()
root=Path(__file__).resolve().parent.parent
compiler=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
stage=Path(tempfile.mkdtemp(prefix='ChillCore-'))
shutil.copy2(args.json,stage/'Newtonsoft.Json.dll')
results=[]
mapping={'netease':'ChillNeteasePlaylist','kugou':'ChillKuGouPlaylist','bilibili':'ChillBilibiliPlaylist','ximalaya':'ChillXimalayaPlaylist','saltplayer':'ChillSaltPlayerPlaylist'}
def run(name, main, sources, test_source, fixture=False, references=()):
    directory=stage/name;directory.mkdir()
    shutil.copy2(args.json,directory/'Newtonsoft.Json.dll')
    test=directory/'Cases.cs';test.write_text(test_source,encoding='utf-8-sig')
    entry=directory/'Entry.cs';entry.write_text((root/'tests/SafeTestEntry.cs').read_text().replace('TEST_MAIN_TYPE',main),encoding='utf-8-sig')
    for reference in references:shutil.copy2(reference,directory/reference.name)
    exe=directory/'Tests.exe'
    command=[str(compiler),'/nologo','/target:exe','/main:SafeTestEntry','/out:'+str(exe),'/reference:'+str(directory/'Newtonsoft.Json.dll'),'/reference:System.Security.dll',str(test),str(entry)]+[str(p) for p in sources]
    command += ['/reference:'+str(reference) for reference in references]
    build=subprocess.run(command,capture_output=True,timeout=60)
    if build.returncode: raise RuntimeError(name+' compilation failed: '+build.stdout.decode(errors='replace'))
    tested=subprocess.run([str(exe)]+([str(directory/'fixture')] if fixture else []),capture_output=True,timeout=30)
    output=tested.stdout.decode(errors='replace')
    if tested.returncode:raise RuntimeError(name+' failed: '+output)
    results.append({'case':name,'passed':True,'result':output.strip().splitlines()[-1]})
    print('PASS '+name+' '+results[-1]['result'])

for provider,namespace in mapping.items():
    source=root/'packages'/provider/'source'
    for case,files in [('LyricTests',['LyricDocument.cs','LyricSource.cs']),('SessionStoreTests',['PlaybackSessionStore.cs','LyricPalette.cs'])]:
        test=(root/'tests/lyrics'/(case+'.cs')).read_text(encoding='utf-8-sig').replace('ChillNeteasePlaylist',namespace)
        run(provider+'-'+case,case,[source/name for name in files],test)
for case,main,files in [('IntegrationTests','IntegrationTestMain',['LyricDocument.cs','LyricIntegration.cs']),('SessionIntegrationTests','SessionIntegrationMain',['PlaybackSessionStore.cs','LatestWorkQueue.cs','PlaybackSessionIntegration.cs'])]:
    run(case,main,[root/'packages/netease/source'/name for name in files],(root/'tests/lyrics'/(case+'.cs')).read_text(encoding='utf-8-sig'))
sources=[]
for provider in ['bilibili','ximalaya','saltplayer']:
    names=['Models.cs','Bridge.cs','FavoritesBridge.cs','FavoriteOperation.cs']
    if provider!='saltplayer':names+=['NetworkErrors.cs']
    if provider=='bilibili':names+=['RequestIssue.cs','DeviceCookies.cs']
    if provider=='ximalaya':names+=['ListeningHttp.cs','ListeningReport.cs','NetworkRequestException.cs']
    sources += [root/'packages'/provider/'source'/name for name in names]
qr=root/'packages/netease/BepInEx/plugins/ChillNeteasePlaylist/QRCoder.dll'
# The fixture uses no real account and never submits platform writes.
favorite=(root/'tests/favorites/FourHeartTests.cs').read_text(encoding='utf-8-sig')
run('favorites','FourHeartTests',sources,favorite,True,[qr])
sources=[root/'packages'/p/'source/RecommendationPolicy.cs' for p in ['netease','kugou']]
sources += [root/'packages/kugou/source'/name for name in ['Bridge.cs','Models.cs','NetworkErrors.cs','RequestIssue.cs','RecommendationsBridge.cs']]
test=(root/'tests/recommendations/RecommendationTests.cs').read_text(encoding='utf-8-sig')+'\nclass RecommendationMain { static void Main(){ System.Console.WriteLine(RecommendationTests.Run()); } }\n'
run('recommendations','RecommendationMain',sources,test)
(stage/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS '+str(len(results))+' offline core regression groups; '+str(stage/'results.json'))
