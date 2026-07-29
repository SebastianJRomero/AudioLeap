# make-icon.ps1 — Genera app.ico (altavoz azul acento) en esta carpeta.
# Ejecutar una sola vez:  powershell -ExecutionPolicy Bypass -File .\make-icon.ps1
# Después, dotnet build/publish lo incrusta automáticamente en el exe.

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 256
$images = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $f = $s / 32.0

    $accent = [System.Drawing.Color]::FromArgb(255, 0, 120, 212)  # azul Windows
    $brush = New-Object System.Drawing.SolidBrush($accent)
    $pen = New-Object System.Drawing.Pen($accent, [float](2.4 * $f))

    # Cuerpo del altavoz
    $pts = @(
        (New-Object System.Drawing.PointF( 5 * $f, 12 * $f)),
        (New-Object System.Drawing.PointF(12 * $f, 12 * $f)),
        (New-Object System.Drawing.PointF(19 * $f,  5 * $f)),
        (New-Object System.Drawing.PointF(19 * $f, 27 * $f)),
        (New-Object System.Drawing.PointF(12 * $f, 20 * $f)),
        (New-Object System.Drawing.PointF( 5 * $f, 20 * $f))
    )
    $g.FillPolygon($brush, $pts)

    # Ondas de sonido
    $g.DrawArc($pen, 20 * $f,  9 * $f,  8 * $f, 14 * $f, -55, 110)
    $g.DrawArc($pen, 23 * $f,  6 * $f, 12 * $f, 20 * $f, -50, 100)

    $g.Dispose(); $pen.Dispose(); $brush.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $images += , $ms.ToArray()
}

# Contenedor ICO (entradas PNG, válido desde Vista)
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)                # reservado
$bw.Write([uint16]1)                # tipo: icono
$bw.Write([uint16]$sizes.Count)     # nº de imágenes

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $images[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 = 256 px
    $bw.Write([byte]$dim)           # ancho
    $bw.Write([byte]$dim)           # alto
    $bw.Write([byte]0)              # paleta
    $bw.Write([byte]0)              # reservado
    $bw.Write([uint16]1)            # planos
    $bw.Write([uint16]32)           # bpp
    $bw.Write([uint32]$data.Length) # tamaño de datos
    $bw.Write([uint32]$offset)      # offset de datos
    $offset += $data.Length
}
foreach ($data in $images) { $bw.Write($data) }

$path = Join-Path $PSScriptRoot 'app.ico'
[System.IO.File]::WriteAllBytes($path, $out.ToArray())
Write-Host "Icono generado: $path"
