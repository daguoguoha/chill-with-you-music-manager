$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$map=@{netease='ChillNeteasePlaylist';kugou='ChillKuGouPlaylist';bilibili='ChillBilibiliPlaylist';ximalaya='ChillXimalayaPlaylist';saltplayer='ChillSaltPlayerPlaylist'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach($id in @('netease','kugou','bilibili','ximalaya','saltplayer')) {
    $stage=Join-Path ([IO.Path]::GetTempPath()) ('ChillCacheChecks-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $refs=@('/reference:netstandard.dll')
    foreach($name in @('NAudio.Core.dll','NAudio.Wasapi.dll','NLayer.dll')) {
        $file=Join-Path $package ('packages/netease/BepInEx/plugins/ChillNeteasePlaylist/'+$name)
        Copy-Item -LiteralPath $file -Destination $stage
        $refs+=('/reference:'+$file)
    }
    $test=Join-Path $stage 'CacheTests.cs'
    [IO.File]::WriteAllText($test,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CacheTests.cs')).Replace('PLATFORM_NAMESPACE',$map[$id])))
    $sources=@((Join-Path $package ('packages/'+$id+'/source/AudioDecoder.cs')),$test)
    if($id -eq 'saltplayer') {$sources+=Join-Path $package 'packages/saltplayer/source/Models.cs'}
    $dll=Join-Path $stage ('CacheChecks-'+$id+'.dll')
    & $compiler /nologo /target:library ('/out:'+$dll) @refs @sources
    if($LASTEXITCODE -ne 0){throw "Cache compilation: $id"}
    $assembly=[Reflection.Assembly]::LoadFrom($dll)
    $type=$assembly.GetType($map[$id]+'.CacheTests',$true)
    Write-Output ($id+': '+$type.GetMethod('Run').Invoke($null,@()))
}
