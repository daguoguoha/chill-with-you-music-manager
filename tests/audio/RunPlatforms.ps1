param([string]$AudioDirectory, [Parameter(Mandatory=$true)][string]$GameDirectory, [string[]]$Platforms=@('netease','kugou','bilibili','ximalaya','saltplayer'))
$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$map=@{netease='ChillNeteasePlaylist';kugou='ChillKuGouPlaylist';bilibili='ChillBilibiliPlaylist';ximalaya='ChillXimalayaPlaylist';saltplayer='ChillSaltPlayerPlaylist'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach($id in $Platforms) {
    $ns=$map[$id]
    $stage=Join-Path ([IO.Path]::GetTempPath()) ('ChillPlatform-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $source=Join-Path $package ('packages/'+$id+'/source')
    $shuffle=Join-Path $stage 'ShuffleTests.cs'
    [IO.File]::WriteAllText($shuffle,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ShuffleTests.cs')).Replace('PLATFORM_NAMESPACE',$ns)))
    & $compiler /nologo /target:exe ('/out:'+(Join-Path $stage 'Shuffle.exe')) (Join-Path $source 'ShufflePolicy.cs') $shuffle
    if($LASTEXITCODE -ne 0){throw "Shuffle compilation: $id"}
    & (Join-Path $stage 'Shuffle.exe')
    if($LASTEXITCODE -ne 0){throw "Shuffle tests: $id"}
    $binding=Join-Path $stage 'ShuffleBindingTests.cs'
    [IO.File]::WriteAllText($binding,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ShuffleBindingTests.cs')).Replace('PLATFORM_NAMESPACE',$ns)))
    $harmony=Join-Path $GameDirectory 'BepInEx/core/0Harmony.dll'
    Get-ChildItem -LiteralPath (Join-Path $GameDirectory 'BepInEx/core') -Filter '*.dll' | Copy-Item -Destination $stage
    & $compiler /nologo /target:exe ('/out:'+(Join-Path $stage 'Binding.exe')) ('/reference:'+$harmony) (Join-Path $source 'NativeShuffle.cs') $binding
    if($LASTEXITCODE -ne 0){throw "Native binding compilation: $id"}
    & (Join-Path $stage 'Binding.exe')
    if($LASTEXITCODE -ne 0){throw "Native binding tests: $id"}
    $navigation=Join-Path $stage 'ShufflePlaybackTests.cs'
    [IO.File]::WriteAllText($navigation,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ShufflePlaybackTests.cs')).Replace('PLATFORM_NAMESPACE',$ns)))
    & $compiler /nologo /target:exe ('/out:'+(Join-Path $stage 'Navigation.exe')) ('/reference:'+$harmony) (Join-Path $source 'NativeShufflePlayback.cs') (Join-Path $source 'ShufflePolicy.cs') $navigation
    if($LASTEXITCODE -ne 0){throw "Navigation compilation: $id"}
    & (Join-Path $stage 'Navigation.exe')
    if($LASTEXITCODE -ne 0){throw "Navigation tests: $id"}
    $latest=Join-Path $stage 'LatestWorkTests.cs'
    [IO.File]::WriteAllText($latest,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'LatestWorkTests.cs')).Replace('PLATFORM_NAMESPACE',$ns)))
    & $compiler /nologo /target:exe ('/out:'+(Join-Path $stage 'LatestWork.exe')) (Join-Path $source 'LatestWorkQueue.cs') $latest
    if($LASTEXITCODE -ne 0){throw "Background save compilation: $id"}
    & (Join-Path $stage 'LatestWork.exe')
    if($LASTEXITCODE -ne 0){throw "Background save tests: $id"}
    $refs=@('/reference:netstandard.dll')
    foreach($name in @('NAudio.Core.dll','NAudio.Wasapi.dll','NLayer.dll')) {
        $file=Join-Path $package ('packages/netease/BepInEx/plugins/ChillNeteasePlaylist/'+$name)
        Copy-Item -LiteralPath $file -Destination $stage
        $refs+=('/reference:'+$file)
    }
    $test=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PlaybackCompatibilityTests.cs')).Replace('ChillNeteasePlaylist',$ns)
    $sources=@((Join-Path $source 'AudioDecoder.cs'))
    if($id -eq 'saltplayer') {
        $test=$test.Replace('AudioDecoder.Decode(', 'DecodeLocal(')
        $test=$test.Replace('    static int passed;', @'
    static int passed;
    static DecodedAudio DecodeLocal(byte[] bytes, string format, Func<bool> canceled)
    {
        string file=Path.Combine(Path.GetTempPath(), "ChillTestSource-"+Guid.NewGuid().ToString("N")+"."+format);
        File.WriteAllBytes(file, bytes);
        try { return AudioDecoder.DecodeFile(file, canceled); }
        finally { Check(File.Exists(file), "local source file preserved"); File.Delete(file); }
    }
'@)
        $test=$test.Replace('            bool canceled = false;', @'
            string wav=Path.Combine(Path.GetTempPath(), "ChillPrecision-"+Guid.NewGuid().ToString("N")+".wav");
            try
            {
                using(var writer=new WaveFileWriter(wav,new WaveFormat(48000,24,1)))
                { byte[] bytes=new byte[4800*3]; for(int i=0;i<4800;i++)bytes[i*3]=1; writer.Write(bytes,0,bytes.Length); }
                using(var pcm=AudioDecoder.DecodeFile(wav,delegate{return false;}))
                { Check(pcm.SampleRate==48000 && pcm.Channels==1,"local WAV native format"); Check(pcm.Samples!=null && pcm.Samples.Any(x=>x>0 && x<1f/32768),"local 24-bit WAV precision through full decoder"); }
                Check(File.Exists(wav),"local WAV preserved");
            } finally { File.Delete(wav); }
            bool canceled = false;
'@)
        $test=$test.Replace('using System.Diagnostics;', 'using System.Diagnostics;'+[Environment]::NewLine+'using System.Linq;')
        $sources+=Join-Path $source 'Models.cs' 
    }
    $testFile=Join-Path $stage 'PlaybackTests.cs';[IO.File]::WriteAllText($testFile,$test)
    & $compiler /nologo /target:exe ('/out:'+(Join-Path $stage 'Playback.exe')) @refs @sources $testFile
    if($LASTEXITCODE -ne 0){throw "Decoder compilation: $id"}
    & (Join-Path $stage 'Playback.exe') $AudioDirectory
    if($LASTEXITCODE -ne 0){throw "Decoder tests: $id"}
    Write-Output "PASS platform $id"
}
