$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$map=@{netease='ChillNeteasePlaylist';kugou='ChillKuGouPlaylist';bilibili='ChillBilibiliPlaylist';ximalaya='ChillXimalayaPlaylist';saltplayer='ChillSaltPlayerPlaylist'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach($id in @('netease','kugou','bilibili','ximalaya','saltplayer')) {
    $stage=Join-Path ([IO.Path]::GetTempPath()) ('ChillEnv-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $source=Join-Path $package ('packages/'+$id+'/source')
    $refs=@('/reference:netstandard.dll')
    foreach($name in @('NAudio.Core.dll','NAudio.Wasapi.dll','NLayer.dll')) {
        $file=Join-Path $package ('packages/netease/BepInEx/plugins/ChillNeteasePlaylist/'+$name)
        Copy-Item -LiteralPath $file -Destination $stage
        $refs+=('/reference:'+$file)
    }
    $test=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EnvironmentTests.cs')).Replace('PLATFORM_NAMESPACE',$map[$id])
    $test=$test.Replace('DIAGNOSTIC_ID',$(if($id -eq 'netease'){'123'}else{'"fixture"'}))
    $sources=@((Join-Path $source 'AudioDecoder.cs'),(Join-Path $source 'PlaybackFailure.cs'))
    if($id -eq 'netease') {$sources+=Join-Path $source 'SongRequestPolicy.cs'}
    if($id -eq 'saltplayer') {
        $test=$test.Replace('DECODE_EXPRESSION','AudioDecoder.DecodeFile(source,canceled)')
        $sources+=Join-Path $source 'Models.cs'
    } else {$test=$test.Replace('DECODE_EXPRESSION','AudioDecoder.Decode(File.ReadAllBytes(source),"mp3",canceled)')}
    $file=Join-Path $stage 'EnvironmentTests.cs';[IO.File]::WriteAllText($file,$test)
    $exe=Join-Path $stage 'EnvironmentTests.exe'
    & $compiler /nologo /target:exe ('/out:'+$exe) @refs @sources $file
    if($LASTEXITCODE -ne 0){throw "Environment compilation: $id"}
    & $exe
    if($LASTEXITCODE -ne 0){throw "Environment tests: $id"}
    $decoder=Join-Path $stage 'NLayer.dll';$hold=Join-Path $stage 'NLayer.dll.test-disabled'
    Move-Item -LiteralPath $decoder -Destination $hold
    try {
        & $exe missing-decoder
        if($LASTEXITCODE -ne 0){throw "Missing dependency tests: $id"}
    } finally {Move-Item -LiteralPath $hold -Destination $decoder}
    Write-Output "PASS platform environment $id"
}
