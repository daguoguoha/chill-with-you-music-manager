param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
# Read metadata only: do not load Unity or start/modify the game.
Add-Type -Path (Join-Path $GameDirectory 'BepInEx\core\Mono.Cecil.dll')
$file=Join-Path $GameDirectory 'Chill With You_Data\Managed\Assembly-CSharp.dll'
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($file)
try {
    $service=@($assembly.MainModule.Types | Where-Object FullName -eq MusicService)[0]
    $required=@('PlayArugumentMusic','PlayMusicInPlaylist','PlayNextMusic','PlayBackMusic','Stop','Dispose','Pause','SetMusicProgress','set_PlayingMusic')
    foreach($name in $required) {
        $methods=@($service.Methods | Where-Object Name -eq $name)
        if($methods.Count -ne 1){throw ('播放器接口不唯一或缺失 '+$name)}
    }
    $callbacks=@($service.Methods | Where-Object {$_.Name -match '^<Play(ArugumentMusic|MusicInPlaylist|NextMusic)>b__' -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -eq 'System.Void'})
    if($callbacks.Count -ne 3){throw '自然完成回调不匹配'}
    foreach($callback in $callbacks) {
        if(-not @($callback.Body.Instructions | Where-Object {$_.Operand -and $_.Operand.ToString() -match 'PlayArugumentMusic|PlayNextMusic|SkipCurrentMusic'}).Count){throw '自然完成回调不负责下一首或循环'}
    }
    $result=[pscustomobject]@{Passed=$true;GameAssemblySHA256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash;RequiredMethods=$required;CompletionCallbacks=@($callbacks.Name);Scope='Metadata inspection only; no Harmony or Unity runtime execution'}
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'bindings-result.json') -Encoding UTF8
    Write-Output 'PASS 游戏元数据接口与自然完成回调匹配'
} finally {$assembly.Dispose()}
