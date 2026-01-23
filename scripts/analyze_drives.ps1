# NexusFS Drive Analysis Script
# Analyzes all drives and finds large folders for cleanup

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "       NexusFS Drive Analysis" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get drive info
Write-Host "DRIVE SPACE SUMMARY:" -ForegroundColor Yellow
Write-Host "--------------------"
Get-PSDrive -PSProvider FileSystem | ForEach-Object {
    $used = [math]::Round($_.Used/1GB, 2)
    $free = [math]::Round($_.Free/1GB, 2)
    $total = $used + $free
    $pct = if ($total -gt 0) { [math]::Round(($used/$total)*100, 1) } else { 0 }
    Write-Host "$($_.Name): " -NoNewline -ForegroundColor White
    Write-Host "$used GB used" -NoNewline -ForegroundColor $(if ($pct -gt 90) { "Red" } elseif ($pct -gt 70) { "Yellow" } else { "Green" })
    Write-Host " / $total GB ($pct%)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "LARGE FOLDERS IN C:\Users\Admin:" -ForegroundColor Yellow
Write-Host "--------------------------------"

$folders = @()

# Hidden folders (like .lmstudio, .ollama, .cache)
Get-ChildItem "C:\Users\Admin" -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
    $path = $_.FullName
    try {
        $size = (Get-ChildItem $path -Recurse -Force -ErrorAction SilentlyContinue |
                 Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
        if ($size -gt 100MB) {
            $folders += [PSCustomObject]@{
                Path = $_.Name
                SizeGB = [math]::Round($size/1GB, 2)
                SizeMB = [math]::Round($size/1MB, 0)
                Type = if ($_.Name.StartsWith('.')) { "Hidden" } else { "Visible" }
            }
        }
    } catch {}
}

$folders | Sort-Object SizeGB -Descending | Select-Object -First 30 | Format-Table -AutoSize

Write-Host ""
Write-Host "SPACE HOGS (>10GB):" -ForegroundColor Red
Write-Host "-------------------"
$folders | Where-Object { $_.SizeGB -gt 10 } | Sort-Object SizeGB -Descending | ForEach-Object {
    Write-Host "$($_.Path): " -NoNewline -ForegroundColor White
    Write-Host "$($_.SizeGB) GB" -ForegroundColor Red
}

Write-Host ""
Write-Host "RECOMMENDATIONS:" -ForegroundColor Green
Write-Host "----------------"

$lmstudio = $folders | Where-Object { $_.Path -eq ".lmstudio" }
if ($lmstudio -and $lmstudio.SizeGB -gt 50) {
    Write-Host "- .lmstudio ($($lmstudio.SizeGB) GB): Consider moving models to G:\ or other large drive" -ForegroundColor Yellow
}

$ollama = $folders | Where-Object { $_.Path -eq ".ollama" }
if ($ollama -and $ollama.SizeGB -gt 10) {
    Write-Host "- .ollama ($($ollama.SizeGB) GB): Consider OLLAMA_MODELS env var to relocate" -ForegroundColor Yellow
}

$cache = $folders | Where-Object { $_.Path -eq ".cache" }
if ($cache -and $cache.SizeGB -gt 5) {
    Write-Host "- .cache ($($cache.SizeGB) GB): Contains pip/huggingface cache, can be cleaned or moved" -ForegroundColor Yellow
}

$downloads = $folders | Where-Object { $_.Path -eq "Downloads" }
if ($downloads -and $downloads.SizeGB -gt 50) {
    Write-Host "- Downloads ($($downloads.SizeGB) GB): High priority for cleanup/organization" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Run complete!" -ForegroundColor Green
