$asmPath = 'C:\Users\kingd\.nuget\packages\myra\1.6.1\lib\netstandard2.0\Myra.dll'
$asm = [System.Reflection.Assembly]::LoadFile($asmPath)
$desk = $asm.GetType('Myra.Graphics2D.UI.Desktop')
$prop = $desk.GetProperty('Scale')
Write-Output ("Desktop.Scale type: {0}" -f $prop.PropertyType.FullName)
