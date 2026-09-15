param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "nupkgs"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Building & Packing AuditTrail NuGet   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

dotnet pack src/AuditTrail/AuditTrail.csproj -c $Configuration -o $OutputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Packaging succeeded! Packages output:" -ForegroundColor Green
    Get-ChildItem -Path $OutputDir -Filter *.nupkg | ForEach-Object {
        Write-Host "   📦 $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor Yellow
        Write-Host "      Path: $($_.FullName)" -ForegroundColor DarkGray
    }
} else {
    Write-Host "`n❌ Packaging failed!" -ForegroundColor Red
    exit 1
}
