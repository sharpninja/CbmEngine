Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Image]::FromFile('F:/GitHub/CbmEngine/artifacts/captures/frost-point-title.png.bmp')
Write-Output "BMP size: $($bmp.Width)x$($bmp.Height)"
$cropped = New-Object System.Drawing.Bitmap(320, 200)
$g = [System.Drawing.Graphics]::FromImage($cropped)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$srcRect = New-Object System.Drawing.Rectangle(32, 35, 320, 200)
$dstRect = New-Object System.Drawing.Rectangle(0, 0, 320, 200)
$g.DrawImage($bmp, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$cropped.Save('F:/GitHub/CbmEngine/artifacts/captures/frost-point-title.png', [System.Drawing.Imaging.ImageFormat]::Png)
$cropped.Dispose()
$bmp.Dispose()
Write-Output 'Saved 320x200 PNG'
