$ErrorActionPreference = 'Stop'
$pluginRoot = Join-Path $PSScriptRoot 'BepInEx\plugins\ChillQQMusicPlaylist'
$payloadHash = (Get-FileHash -LiteralPath (Join-Path $pluginRoot 'ChillQQMusicPlaylist.dll') -Algorithm SHA256).Hash
$nativeHash = (Get-FileHash -LiteralPath (Join-Path $pluginRoot 'ChillQQMusic.dll') -Algorithm SHA256).Hash
$installPath = Join-Path $PSScriptRoot 'Install.ps1'
$text = [IO.File]::ReadAllText($installPath)
$payloadPattern = "(?m)^.*主插件文件缺失或校验失败.*$"
$nativePattern = "(?m)^.*QQ 音乐桥接文件缺失或校验失败.*$"
$text = [regex]::Replace($text,$payloadPattern,[Text.RegularExpressions.MatchEvaluator]{param($match) [regex]::Replace($match.Value,"'[A-F0-9]{64}'",("'"+$payloadHash+"'"))})
$text = [regex]::Replace($text,$nativePattern,[Text.RegularExpressions.MatchEvaluator]{param($match) [regex]::Replace($match.Value,"'[A-F0-9]{64}'",("'"+$nativeHash+"'"))})
[IO.File]::WriteAllText($installPath,$text,(New-Object Text.UTF8Encoding($true)))
$dependenciesPath = Join-Path $PSScriptRoot 'dependencies.json'
$dependencies = Get-Content -LiteralPath $dependenciesPath -Raw | ConvertFrom-Json
$dependencies.ChillQQMusic.sha256 = $nativeHash.ToLowerInvariant()
$dependencies | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $dependenciesPath -Encoding UTF8
$records = foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -File -Recurse -Force | Sort-Object FullName) {
    $relative = $file.FullName.Substring($PSScriptRoot.Length+1).Replace('\','/')
    if ($relative -eq 'SHA256SUMS.txt' -or $relative.StartsWith('.git/')) { continue }
    (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'SHA256SUMS.txt'),($records -join "`n")+"`n",(New-Object Text.UTF8Encoding($false)))
Write-Output 'Updated installer DLL hashes, dependency manifest, and SHA256SUMS.txt.'
