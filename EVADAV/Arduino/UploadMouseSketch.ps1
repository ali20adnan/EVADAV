# Flash EVADAV Leonardo + USB Host Shield firmware (PING/TEST + passthrough + dx,dy).
param([switch]$NoPause)
$ErrorActionPreference = "Stop"
function Wait-Exit($code = 0) {
    if (-not $NoPause) { Read-Host "Press Enter to exit" | Out-Null }
    exit $code
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sketch = Join-Path $root "arduino\leonardo_hostshield"
$fqbn = "arduino:avr:leonardo"
$cli = Join-Path $env:USERPROFILE ".arduino-cli\arduino-cli.exe"
if (-not (Test-Path $cli)) { $cli = "arduino-cli" }

Write-Host ""
Write-Host "=== 1. Stop apps that lock the COM port ===" -ForegroundColor Cyan
Get-Process EVADAV, EVADAV, Aimmy2, AimbotCsWeb, ColorSyncHost -ErrorAction SilentlyContinue |
    ForEach-Object {
        Write-Host ("  stopping {0} PID {1}" -f $_.Name, $_.Id)
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "=== 2. Sketch ===" -ForegroundColor Cyan
$srcIno = Join-Path $sketch "leonardo_hostshield.ino"
if (-not (Test-Path $srcIno)) {
    Write-Host "Sketch not found: $srcIno" -ForegroundColor Red
    Wait-Exit 1
}
Write-Host "  using $srcIno"

Write-Host ""
Write-Host "=== 3. Compile ===" -ForegroundColor Cyan
& $cli compile --fqbn $fqbn $sketch
if ($LASTEXITCODE -ne 0) {
    Write-Host "Compile failed. Install Arduino CLI and USB Host Shield Library 2.0." -ForegroundColor Red
    Wait-Exit 1
}

Write-Host ""
Write-Host "=== 4. Find Arduino Leonardo ===" -ForegroundColor Cyan
& $cli board list
$port = $null
$dev = Get-PnpDevice -PresentOnly | Where-Object {
    $_.Class -eq 'Ports' -and $_.InstanceId -match 'VID_2341|VID_2A03'
} | Select-Object -First 1
if ($dev -and $dev.FriendlyName -match '(COM\d+)') {
    $port = $Matches[1]
}
if (-not $port) {
    $listed = & $cli board list 2>$null | Select-String 'leonardo'
    if ($listed -and "$listed" -match '(COM\d+)') { $port = $Matches[1] }
}
if (-not $port) {
    $sp = [System.IO.Ports.SerialPort]::GetPortNames() | Where-Object { $_ -ne 'COM1' }
    if ($sp.Count -eq 1) { $port = $sp[0] }
}
if (-not $port) {
    Write-Host "Arduino not found. Plug Leonardo USB into the PC." -ForegroundColor Red
    Wait-Exit 1
}
Write-Host "Found Leonardo on $port" -ForegroundColor Green

Write-Host ""
Write-Host "=== 5. Upload to $port ===" -ForegroundColor Cyan
& $cli upload -p $port --fqbn $fqbn $sketch
if ($LASTEXITCODE -ne 0) {
    Write-Host "Upload failed. Close Serial Monitor / EVADAV and try again." -ForegroundColor Red
    Wait-Exit 1
}

Write-Host ""
Write-Host "=== 6. Handshake (PING) ===" -ForegroundColor Cyan
Start-Sleep -Seconds 2.5
try {
    $sp = New-Object System.IO.Ports.SerialPort $port, 115200
    $sp.NewLine = "`n"
    $sp.ReadTimeout = 800
    $sp.DtrEnable = $true
    $sp.Open()
    Start-Sleep -Milliseconds 2200
    $sp.DiscardInBuffer()
    $sp.Write("PING`n")
    Start-Sleep -Milliseconds 400
    $resp = ""
    while ($sp.BytesToRead -gt 0) { $resp += $sp.ReadExisting() }
    $sp.Close()
    $sp.Dispose()
    if ($resp -match 'LEONARDO') {
        Write-Host $resp.Trim() -ForegroundColor Green
        Write-Host "Handshake OK. Mouse into Host Shield USB. Leonardo USB into PC." -ForegroundColor Green
        Write-Host "In EVADAV: Mouse Movement Method = Arduino Leonardo (Host Shield)" -ForegroundColor Green
    } else {
        Write-Host "No PING reply yet (CDC still coming up). Firmware is on the board." -ForegroundColor Yellow
        Write-Host "Reply: $resp"
    }
} catch {
    Write-Host "Could not PING ($($_.Exception.Message)). Firmware uploaded anyway." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. Uploaded EVADAV host-shield firmware to $port." -ForegroundColor Green
Wait-Exit 0
