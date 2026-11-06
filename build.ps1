$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outExe = Join-Path $PSScriptRoot "GamepadToHotas.exe"
$oldExe = Join-Path $PSScriptRoot "GamepadToHotas.exe.old"
$srcFiles = Get-ChildItem (Join-Path $PSScriptRoot "src\*.cs") | ForEach-Object { $_.FullName }

if (Test-Path $oldExe) {
    Remove-Item $oldExe -Force -ErrorAction SilentlyContinue
}
if (Test-Path $outExe) {
    try {
        Remove-Item $outExe -Force -ErrorAction Stop
    } catch {
        Move-Item $outExe $oldExe -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Building GamepadToHotas.exe..."
& $csc /target:winexe /platform:x64 /optimize+ /out:$outExe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll $srcFiles

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded: $outExe" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
