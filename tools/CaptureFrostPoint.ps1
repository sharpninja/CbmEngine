[CmdletBinding()]
param(
    [string]$D64 = 'F:\GitHub\CbmEngine\assets\d64\frostpoint.d64',
    [string]$CaptureDir = 'F:\GitHub\CbmEngine\artifacts\captures',
    [int]$WaitSecondsAfterLoad = 15
)

$ErrorActionPreference = 'Stop'
$x64sc = 'C:\Users\kingd\AppData\Local\UniGetUI\Chocolatey\bin\x64sc.exe'
New-Item -ItemType Directory -Force $CaptureDir | Out-Null

Stop-Process -Name x64sc -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "Launching x64sc -pal +cart -autostart $D64 -remotemonitor"
$proc = Start-Process -FilePath $x64sc -ArgumentList @('-pal', '+cart', '-autostart', $D64, '-remotemonitor') -PassThru
Write-Host "Waiting $WaitSecondsAfterLoad seconds for autoload + music to start..."
Start-Sleep -Seconds $WaitSecondsAfterLoad

$client = New-Object System.Net.Sockets.TcpClient
$client.Connect('127.0.0.1', 6510)
$stream = $client.GetStream()
$stream.ReadTimeout = 1500
$writer = New-Object System.IO.StreamWriter($stream)
$writer.NewLine = "`n"
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($stream)

function Drain {
    Start-Sleep -Milliseconds 300
    $buf = New-Object byte[] 65536
    $sb = New-Object System.Text.StringBuilder
    try {
        while ($stream.DataAvailable) {
            $n = $stream.Read($buf, 0, $buf.Length)
            if ($n -le 0) { break }
            $null = $sb.Append([System.Text.Encoding]::ASCII.GetString($buf, 0, $n))
        }
    } catch { }
    return $sb.ToString()
}

function Send-Cmd($cmd) {
    Write-Host "> $cmd"
    $writer.WriteLine($cmd)
    $out = Drain
    if ($out.Trim().Length -gt 0) { Write-Host $out }
    return $out
}

$null = Drain
$cap = $CaptureDir.Replace('\','/')
Send-Cmd 'bank ram'
Send-Cmd "save `"$cap/all-ram.bin`" 0 0000 ffff"
Send-Cmd 'bank io'
Send-Cmd "save `"$cap/color-vic.bin`" 0 d000 dbff"
$vicState = Send-Cmd 'm d000 d02f'
$vicState | Out-File "$CaptureDir\vic-registers.txt" -Encoding ASCII
$ciaState = Send-Cmd 'm dd00 dd03'
$ciaState | Out-File "$CaptureDir\cia2-registers.txt" -Encoding ASCII
Send-Cmd "screenshot `"$cap/frost-point-title.png`" 0"
Send-Cmd 'x'

Start-Sleep -Seconds 1
$client.Close()
Write-Host "Capture dir: $CaptureDir"
Get-ChildItem $CaptureDir | Format-Table Name, Length
