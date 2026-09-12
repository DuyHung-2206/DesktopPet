param(
    [string]$SourcePath,
    [string]$OutputDir = "Assets\Pets\Cat",
    [int]$FrameSize = 64,
    [int]$TargetBaseline = 56
)

Add-Type -AssemblyName System.Drawing

if (-not $SourcePath -or -not (Test-Path $SourcePath)) {
    Write-Error "Please specify a valid -SourcePath parameter."
    exit 1
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$srcBmp = New-Object System.Drawing.Bitmap($SourcePath)
$srcW = $srcBmp.Width
$srcH = $srcBmp.Height

# Definitions for the 8 animation rows in the sheet
$rowDefs = @(
    @{ Name = "Idle";  StartY = 13;  EndY = 57;  MinFrameX = 190 },
    @{ Name = "Walk";  StartY = 63;  EndY = 107; MinFrameX = 190 },
    @{ Name = "Run";   StartY = 115; EndY = 162; MinFrameX = 190 },
    @{ Name = "Eat";   StartY = 168; EndY = 211; MinFrameX = 190 },
    @{ Name = "Bath";  StartY = 220; EndY = 263; MinFrameX = 190 },
    @{ Name = "Sleep"; StartY = 270; EndY = 310; MinFrameX = 190 },
    @{ Name = "Happy"; StartY = 311; EndY = 359; MinFrameX = 190 },
    @{ Name = "Hurt";  StartY = 363; EndY = 403; MinFrameX = 190 }
)

Write-Output "=== AUTOMATIC SPRITE SHEET EXTRACTION START ==="
Write-Output "Source: $SourcePath ($($srcW)x$($srcH))"
Write-Output "Destination: $OutputDir"

foreach ($row in $rowDefs) {
    # 1. Detect column occupancy for frames (X >= MinFrameX)
    $colHasPixels = New-Object bool[] $srcW
    for ($x = $row.MinFrameX; $x -lt $srcW; $x++) {
        $hasPx = $false
        for ($y = $row.StartY; $y -le $row.EndY; $y++) {
            $c = $srcBmp.GetPixel($x, $y)
            if ($c.A -gt 10) {
                $hasPx = $true
                break
            }
        }
        $colHasPixels[$x] = $hasPx
    }

    # 2. Cluster frames horizontally
    $clusters = New-Object System.Collections.ArrayList
    $inCluster = $false
    $startX = 0
    for ($x = $row.MinFrameX; $x -lt $srcW; $x++) {
        if ($colHasPixels[$x] -and -not $inCluster) {
            $inCluster = $true
            $startX = $x
        }
        elseif (-not $colHasPixels[$x] -and $inCluster) {
            $inCluster = $false
            $clusters.Add(@{ StartX = $startX; EndX = $x - 1 }) | Out-Null
        }
    }
    if ($inCluster) {
        $clusters.Add(@{ StartX = $startX; EndX = $srcW - 1 }) | Out-Null
    }

    $frameCount = $clusters.Count
    if ($frameCount -eq 0) {
        Write-Warning "No frames detected for $($row.Name)!"
        continue
    }

    # 3. Find exact bounding boxes and row baseline
    $frameBoxes = New-Object System.Collections.ArrayList
    $rowBaseline = 0
    for ($i = 0; $i -lt $frameCount; $i++) {
        $cl = $clusters[$i]
        $minX = 9999; $maxX = -1; $minY = 9999; $maxY = -1
        for ($x = $cl.StartX; $x -le $cl.EndX; $x++) {
            for ($y = $row.StartY; $y -le $row.EndY; $y++) {
                if ($srcBmp.GetPixel($x, $y).A -gt 10) {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }
        if ($maxY -gt $rowBaseline) { $rowBaseline = $maxY }
        $frameBoxes.Add(@{
            MinX = $minX; MaxX = $maxX; Width = ($maxX - $minX + 1);
            MinY = $minY; MaxY = $maxY; Height = ($maxY - $minY + 1);
        }) | Out-Null
    }

    # 4. Create horizontal animation strip: (frameCount * FrameSize) x FrameSize
    $stripWidth = $frameCount * $FrameSize
    $stripHeight = $FrameSize
    $stripBmp = New-Object System.Drawing.Bitmap($stripWidth, $stripHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    # Initialize transparent background
    $g = [System.Drawing.Graphics]::FromImage($stripBmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.Dispose()

    # Copy each frame into the strip with aligned baseline and centered X
    for ($i = 0; $i -lt $frameCount; $i++) {
        $fb = $frameBoxes[$i]
        
        # Calculate destination origin in the i-th cell
        $cellStartX = $i * $FrameSize
        $offsetX = [Math]::Floor(($FrameSize - $fb.Width) / 2.0)
        
        # Baseline alignment: bottom of frame aligns with TargetBaseline relative to rowBaseline
        $bottomDiff = $rowBaseline - $fb.MaxY
        $dstBottom = $TargetBaseline - $bottomDiff
        $dstY = $dstBottom - $fb.Height + 1

        # Copy pixels directly (preserve authentic pixel art)
        for ($sy = $fb.MinY; $sy -le $fb.MaxY; $sy++) {
            $dy = $dstY + ($sy - $fb.MinY)
            if ($dy -lt 0 -or $dy -ge $FrameSize) { continue }

            for ($sx = $fb.MinX; $sx -le $fb.MaxX; $sx++) {
                $dx = $cellStartX + $offsetX + ($sx - $fb.MinX)
                if ($dx -lt $cellStartX -or $dx -ge ($cellStartX + $FrameSize)) { continue }

                $pixelColor = $srcBmp.GetPixel($sx, $sy)
                if ($pixelColor.A -gt 0) {
                    $stripBmp.SetPixel($dx, $dy, $pixelColor)
                }
            }
        }
    }

    # 5. Save output PNG
    $name = $row["Name"]
    $outPath = Join-Path $OutputDir ($name + ".png")
    $stripBmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $stripBmp.Dispose()

    Write-Output ("[OK] Extracted " + $name + ".png -> Frames: " + $frameCount + ", Size: " + $stripWidth + "x" + $stripHeight)
}

$srcBmp.Dispose()
Write-Output "=== EXTRACTION COMPLETE ==="
