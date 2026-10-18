param([Parameter(Mandatory=$true)][string]$OldPackage,[Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$sandbox=Join-Path ([IO.Path]::GetTempPath()) ('mmu-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$game=Join-Path $sandbox '旧合集升级 中文路径'
New-Item -ItemType Directory -Path (Join-Path $game 'Chill With You_Data\Managed') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'Chill With You.exe'),'fixture only, not a game')
$shell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$oldCatalog=Get-Content (Join-Path $OldPackage 'catalog.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$catalog=Get-Content (Join-Path $Package 'catalog.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$results=New-Object 'Collections.Generic.List[object]'
function Assert($ok,$message){if(-not $ok){throw $message}}
function Hash($p){return (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function Call($at,$action,$id){
    $raw=& $shell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $at 'Manager.ps1') -Action $action -GameDirectory $game -Id $id
    $r=$raw | ConvertFrom-Json
    Assert $r.Ok $r.Message
    return $r
}
$sentinels=@{}
try {
    foreach($p in $oldCatalog.Plugins){$null=Call $OldPackage Install $p.Id}
    $null=Call $OldPackage Disable qq
    foreach($relative in @('BepInEx\plugins\ChillNeteasePlaylist\account\fixture.txt','BepInEx\plugins\ChillQQMusicPlaylist\account\fixture.txt','BepInEx\config\local.chill.netease.cfg','BepInEx\config\ChillAudioProgress\progress.json','BepInEx\config\ChillLyrics\netease\fixture.lrc')){
        $path=Join-Path $game $relative
        New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
        [IO.File]::WriteAllText($path,'synthetic preservation marker; no credentials')
        $sentinels[$path]=Hash $path
    }
    foreach($id in @('netease','qq','kugou','bilibili','ximalaya','saltplayer')){
        $p=@($catalog.Plugins | Where-Object Id -eq $id)[0]
        $old=@($oldCatalog.Plugins | Where-Object Id -eq $id)[0]
        $before=@{}
        foreach($rel in @($old.Targets.Path)+@($old.Primary+'.disabled',$old.Manifest)){
            $path=Join-Path $game $rel
            $before[$rel]=if(Test-Path -LiteralPath $path){Hash $path}else{$null}
        }
        $null=Call $Package Install $id
        foreach($target in $p.Targets){Assert ((Hash (Join-Path $game $target.Path)) -eq $target.Hash) ('upgrade hash: '+$target.Path)}
        $record=Get-Content -LiteralPath (Join-Path $game $p.Manifest) -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert ($record.Version -eq $p.Version) 'upgrade version mismatch'
        if($id -ne 'qq'){
            Assert (Test-Path -LiteralPath (Join-Path $game 'BepInEx\plugins\ChillQQMusicPlaylist\ChillQQMusicPlaylist.dll.disabled')) 'another provider was enabled'
            Assert (-not(Test-Path -LiteralPath (Join-Path $game 'BepInEx\plugins\ChillQQMusicPlaylist\ChillQQMusicPlaylist.dll'))) 'another provider enabled DLL appeared'
        }
        $null=Call $Package Undo $id
        foreach($rel in $before.Keys){
            $path=Join-Path $game $rel
            if($null -eq $before[$rel]){Assert (-not(Test-Path -LiteralPath $path)) ('undo unexpected file '+$rel)}
            else{Assert ((Hash $path) -eq $before[$rel]) ('undo mismatch '+$rel)}
        }
        foreach($path in $sentinels.Keys){Assert ((Hash $path) -eq $sentinels[$path]) 'account/config/progress marker changed'}
        $results.Add([pscustomobject]@{Provider=$id;Passed=$true;Upgrade=$p.Version;Restored=$old.Version;MarkersPreserved=$true;OtherDisabledProviderPreserved=$true})
        Write-Output ('PASS '+$id+' upgrade and undo')
    }
    foreach($id in @('progress')){
        $oldRoot=Join-Path $OldPackage ('packages\'+$id)
        $newRoot=Join-Path $Package ('packages\'+$id)
        foreach($f in Get-ChildItem -LiteralPath $oldRoot -File -Recurse){
            $rel=$f.FullName.Substring($oldRoot.Length).TrimStart('\')
            Assert ((Hash $f.FullName) -eq (Hash (Join-Path $newRoot $rel))) ('unchanged package mismatch '+$id+' '+$rel)
        }
    }
} finally {
    [pscustomobject]@{OldVersion=$oldCatalog.ManagerVersion;Version=$catalog.ManagerVersion;Sandbox=$sandbox;Results=$results.ToArray();Passed=$results.Count;Expected=6;ProgressUnchanged=($results.Count -eq 6)} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'upgrade-result.json') -Encoding UTF8
}
