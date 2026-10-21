$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$map=@{netease='ChillNeteasePlaylist';kugou='ChillKuGouPlaylist';bilibili='ChillBilibiliPlaylist';ximalaya='ChillXimalayaPlaylist';saltplayer='ChillSaltPlayerPlaylist'}
foreach($id in @('netease','kugou','bilibili','ximalaya','saltplayer')) {
    $stage=Join-Path ([IO.Path]::GetTempPath()) ('ChillFocus-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $cases=Join-Path $stage 'Cases.cs'
    [IO.File]::WriteAllText($cases,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PlaylistFocusTests.cs')).Replace('PLATFORM_NAMESPACE',$map[$id])))
    $exe=Join-Path $stage 'Tests.exe'
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe ('/out:'+$exe) (Join-Path $root ('packages/'+$id+'/source/PlaylistFocusPolicy.cs')) $cases
    if($LASTEXITCODE -ne 0){throw "Focus compilation: $id"}
    & $exe
    if($LASTEXITCODE -ne 0){throw "Focus test: $id"}
    Write-Output "PASS native playlist focus $id"
}
