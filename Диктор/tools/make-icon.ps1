$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$asset=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Dictor/Assets'
$frames=@()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $bitmap=[Drawing.Bitmap]::new($size,$size)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size/256.0,$size/256.0)
    $blue=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#245DD8'))
    $white=[Drawing.SolidBrush]::new([Drawing.Color]::White)
    $pen=[Drawing.Pen]::new([Drawing.Color]::White,14)
    $pen.StartCap=$pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
    $path=[Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(8,8,112,112,180,90); $path.AddArc(136,8,112,112,270,90)
    $path.AddArc(136,136,112,112,0,90); $path.AddArc(8,136,112,112,90,90); $path.CloseFigure()
    $g.FillPath($blue,$path)
    $g.FillEllipse($white,102,44,52,52); $g.FillRectangle($white,102,70,52,56); $g.FillEllipse($white,102,100,52,52)
    $g.DrawLine($pen,78,119,78,132); $g.DrawArc($pen,78,82,100,100,0,180); $g.DrawLine($pen,178,119,178,132)
    $g.DrawLine($pen,128,182,128,210); $g.DrawLine($pen,100,210,156,210)
    $ms=[IO.MemoryStream]::new(); $bitmap.Save($ms,[Drawing.Imaging.ImageFormat]::Png)
    $frames+=,[pscustomobject]@{Size=$size;Bytes=$ms.ToArray()}
    if ($size -eq 256) { $bitmap.Save((Join-Path $asset 'Dictor.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $ms.Dispose(); $g.Dispose(); $bitmap.Dispose(); $path.Dispose(); $blue.Dispose(); $white.Dispose(); $pen.Dispose()
}
$out=[IO.File]::Create((Join-Path $asset 'Dictor.ico')); $writer=[IO.BinaryWriter]::new($out)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset=6+16*$frames.Count
    foreach($frame in $frames) {
        $dimension=if($frame.Size -eq 256){0}else{$frame.Size}
        $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
        $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset)
        $offset+=$frame.Bytes.Length
    }
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose();$out.Dispose()}
