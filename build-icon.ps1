param([ValidateSet('Nfo','Brew')][string]$Kind='Nfo')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$iconDir=Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force -Path $iconDir | Out-Null
$frames=@()
foreach($size in @(16,24,32,48,64,128,256)) {
 $bitmap=[Drawing.Bitmap]::new($size,$size)
 $g=[Drawing.Graphics]::FromImage($bitmap)
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.ScaleTransform(($size/256.0),($size/256.0))
 $g.Clear([Drawing.Color]::Transparent)
 $navy=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#152C46'))
 $white=[Drawing.SolidBrush]::new([Drawing.Color]::White)
 $accent=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($(if($Kind -eq 'Nfo'){'#45D5BD'}else{'#F4AE3C'})))
 $g.FillEllipse($navy,4,4,248,248)
 if($Kind -eq 'Nfo') {
  $wand=[Drawing.Pen]::new([Drawing.Color]::White,19);$wand.StartCap=[Drawing.Drawing2D.LineCap]::Round;$wand.EndCap=[Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($wand,66,199,151,104);$wand.Dispose()
  $gold=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFD768'))
  $star=[Drawing.PointF[]]@([Drawing.PointF]::new(163,49),[Drawing.PointF]::new(175,81),[Drawing.PointF]::new(210,88),[Drawing.PointF]::new(183,111),[Drawing.PointF]::new(185,146),[Drawing.PointF]::new(157,126),[Drawing.PointF]::new(125,139),[Drawing.PointF]::new(134,105),[Drawing.PointF]::new(112,78),[Drawing.PointF]::new(147,77))
  $g.FillPolygon($gold,$star)
  $g.FillEllipse($accent,66,64,13,13);$g.FillEllipse($accent,196,164,11,11);$g.FillEllipse($white,105,42,7,7)
  $gold.Dispose()
 } else {
  $pen=[Drawing.Pen]::new([Drawing.Color]::White,15)
  $g.DrawArc($pen,146,88,65,78,-95,190); $pen.Dispose()
  $g.FillRectangle($white,62,67,112,144)
  $g.FillRectangle($accent,73,92,90,105)
  foreach($x in @(78,106,134)) {$g.FillEllipse($white,$x,52,42,42)}
  $g.FillRectangle($white,72,70,90,20)
  $g.FillRectangle($white,89,110,7,64); $g.FillRectangle($white,119,110,7,64)
 }
 $memory=[IO.MemoryStream]::new();$bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
 $frames+=,[pscustomobject]@{Size=$size;Bytes=$memory.ToArray()}
 if($size -eq 256){$bitmap.Save((Join-Path $iconDir 'app-preview.png'),[Drawing.Imaging.ImageFormat]::Png)}
 $memory.Dispose();$g.Dispose();$bitmap.Dispose();$navy.Dispose();$white.Dispose();$accent.Dispose()
}
$stream=[IO.File]::Create((Join-Path $iconDir 'app.ico'));$writer=[IO.BinaryWriter]::new($stream)
try {
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
 $offset=6+16*$frames.Count
 foreach($frame in $frames) {
  $dim=if($frame.Size -eq 256){0}else{$frame.Size}
  $writer.Write([byte]$dim);$writer.Write([byte]$dim);$writer.Write([byte]0);$writer.Write([byte]0)
  $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset)
  $offset+=$frame.Bytes.Length
 }
 foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose();$stream.Dispose()}

