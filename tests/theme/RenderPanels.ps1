param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$Package=[IO.Path]::GetFullPath($Package)
$template=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PreviewPlugin.cs.template') -Raw -Encoding UTF8
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$results=@()
foreach($id in @('netease','qq','kugou','bilibili','ximalaya','saltplayer'))
{
    $source=Join-Path $Package ('packages\'+$id+'\source');$ui=Join-Path $source 'PlayerUI.cs'
    $namespace=[regex]::Match([IO.File]::ReadAllText($ui),'namespace (\w+)').Groups[1].Value
    $states=if($id -eq 'saltplayer'){'"library","error","import"'}else{'"library","login","error","import"'}
    $salt=if($id -eq 'saltplayer'){'int nativeMenuVersion;void RegisterNativePlaylist(){}void Run(Func<object> f,Action<object> c){}'}else{''}
    $stub=$template.Replace('@NAMESPACE@',$namespace).Replace('@SALT_IMPORT@',$salt).Replace('@STATES@',$states)
    if($id -eq 'saltplayer'){$stub+=[Environment]::NewLine+'namespace '+$namespace+' {static class Bridge{public static string LastImportDetails="";public static ImportResult Import(string a,string b){return null;}public static List<Playlist> Lists(){return null;}} static class FilePicker{public static string Select(bool a){return null;}}}'}
    $driver=Join-Path $PSScriptRoot ('Preview-'+$id+'.cs');[IO.File]::WriteAllText($driver,$stub,(New-Object Text.UTF8Encoding($true)))
    $sources=@($driver,(Join-Path $PSScriptRoot 'PreviewUnity.cs'),$ui,(Join-Path $source 'Models.cs'),(Join-Path $source 'GameTheme.cs'))
    if($id -eq 'saltplayer'){$sources+=Join-Path $source 'LocalImport.cs'}
    $exe=Join-Path $PSScriptRoot ('Preview-'+$id+'.exe')
    & $compiler /nologo /nowarn:0649,0414 /target:exe /platform:x64 ('/out:'+$exe) /reference:System.Drawing.dll @sources
    if($LASTEXITCODE -ne 0){throw ('预览编译失败 '+$id)}
    $out=Join-Path $PSScriptRoot ('preview-'+$id);& $exe $out
    if($LASTEXITCODE -ne 0){throw ('预览生成失败 '+$id)}
    $results+=[pscustomobject]@{Plugin=$id;Views=@(Get-ChildItem -LiteralPath $out -Filter '*.png' | Select-Object -ExpandProperty BaseName);Scope='Production PlayerUI and GameTheme with a GDI test adapter; synthetic account and playlist; no Unity runtime or network'}
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'render-results.json') -Encoding UTF8
