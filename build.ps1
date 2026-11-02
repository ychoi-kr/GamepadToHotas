$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outExe = Join-Path $PSScriptRoot "GamepadToHotas.exe"
$srcFiles = Get-ChildItem (Join-Path $PSScriptRoot "src\*.cs") | ForEach-Object { $_.FullName }

Write-Host "Building GamepadToHotas.exe..."
& $csc /target:winexe /platform:x64 /optimize+ /out:$outExe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll $srcFiles

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded: $outExe" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
