$ErrorActionPreference = 'Stop'
$payloadHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'BepInEx\plugins\ChillBilibiliPlaylist\ChillBilibiliPlaylist.dll') -Algorithm SHA256).Hash
$path = Join-Path $PSScriptRoot 'Install.ps1'
$text = [IO.File]::ReadAllText($path)
$text = [regex]::Replace($text, '(?m)^.*主插件文件缺失或校验失败.*$', [Text.RegularExpressions.MatchEvaluator]{param($match) [regex]::Replace($match.Value,"'[A-F0-9]{64}'",("'"+$payloadHash+"'"))})
[IO.File]::WriteAllText($path,$text,(New-Object Text.UTF8Encoding($true)))
$records = foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -File -Recurse -Force | Sort-Object FullName) {
    $relative = $file.FullName.Substring($PSScriptRoot.Length+1).Replace('\','/')
    if($relative -ne 'SHA256SUMS.txt' -and -not $relative.StartsWith('.git/')) { (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative }
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'SHA256SUMS.txt'),($records -join "`n")+"`n",(New-Object Text.UTF8Encoding($false)))
