$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$map=@{netease='ChillNeteasePlaylist';kugou='ChillKuGouPlaylist';bilibili='ChillBilibiliPlaylist';ximalaya='ChillXimalayaPlaylist'}
foreach($id in @('netease','kugou','bilibili','ximalaya')) {
    $stage=Join-Path ([IO.Path]::GetTempPath()) ('ChillQrPoll-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $test=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'QrPollingTests.cs')).Replace('PLATFORM_NAMESPACE',$map[$id])
    $source=Join-Path $root ('packages/'+$id+'/source')
    $support=if($id -eq 'netease'){'QrRequest.cs'}elseif($id -eq 'ximalaya'){'NetworkRequestException.cs'}else{'RequestIssue.cs'}
    $transient=if($id -eq 'netease'){'new QrRequestException("SECRET",true)'}elseif($id -eq 'ximalaya'){'new NetworkRequestException("SECRET",true)'}else{'RequestIssue.Network("SECRET","/qr",true)'}
    $permanent=$transient.Replace(',true)',',false)')
    $cases=Join-Path $stage 'Cases.cs';[IO.File]::WriteAllText($cases,$test.Replace('TRANSIENT_ERROR',$transient).Replace('PERMANENT_ERROR',$permanent))
    $exe=Join-Path $stage 'Tests.exe'
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe ('/out:'+$exe) (Join-Path $source 'QrPolling.cs') (Join-Path $source 'QrPollingPolicy.cs') (Join-Path $source $support) $cases
    if($LASTEXITCODE -ne 0){throw "QR polling compilation: $id"}
    & $exe
    if($LASTEXITCODE -ne 0){throw "QR polling tests: $id"}
    Write-Output "PASS QR polling $id"
}
