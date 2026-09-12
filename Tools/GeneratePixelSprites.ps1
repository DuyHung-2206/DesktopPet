Add-Type -AssemblyName System.Drawing

function Create-PixelSprite {
    param(
        [string]$Path,
        [int]$Width = 32,
        [int]$Height = 32,
        [string[]]$Grid,
        [hashtable]$Palette
    )

    $dir = Split-Path $Path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    
    # Fill transparent
    for ($y = 0; $y -lt $Height; $y++) {
        for ($x = 0; $x -lt $Width; $x++) {
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
        }
    }

    # Render grid
    for ($y = 0; $y -lt $Grid.Length -and $y -lt $Height; $y++) {
        $row = $Grid[$y]
        for ($x = 0; $x -lt $row.Length -and $x -lt $Width; $x++) {
            $ch = [string]$row[$x]
            if ($Palette.ContainsKey($ch)) {
                $c = $Palette[$ch]
                $bmp.SetPixel($x, $y, $c)
            }
        }
    }

    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# --- PALETTES ---
$O = [System.Drawing.Color]::FromArgb(255, 26, 26, 36)      # Outline #1A1A24
$W = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)  # White #FFFFFF
$E = [System.Drawing.Color]::FromArgb(255, 20, 20, 24)     # Eye pupil
$B = [System.Drawing.Color]::FromArgb(255, 255, 138, 128)  # Blush pink #FF8A80
$N = [System.Drawing.Color]::FromArgb(255, 216, 27, 96)    # Nose pink

# Cat Palette (Orange Tabby)
$C1 = [System.Drawing.Color]::FromArgb(255, 255, 167, 38) # Main Orange
$C2 = [System.Drawing.Color]::FromArgb(255, 239, 108, 0)  # Darker Orange / Stripes
$C3 = [System.Drawing.Color]::FromArgb(255, 255, 224, 130)# Cream / Belly
$catPal = @{ 'O'=$O; 'C'=$C1; 'D'=$C2; 'K'=$C3; 'W'=$W; 'E'=$E; 'B'=$B; 'N'=$N }

# Dog Palette (Golden Shiba)
$D1 = [System.Drawing.Color]::FromArgb(255, 215, 136, 68) # Golden Tan
$D2 = [System.Drawing.Color]::FromArgb(255, 166, 92, 34)  # Dark Tan / Ears
$D3 = [System.Drawing.Color]::FromArgb(255, 250, 235, 215)# Muzzle Cream
$dogPal = @{ 'O'=$O; 'D'=$D1; 'S'=$D2; 'M'=$D3; 'W'=$W; 'E'=$E; 'B'=$B; 'N'=$N }

# Rabbit Palette (Soft White/Pink)
$R1 = [System.Drawing.Color]::FromArgb(255, 245, 245, 250)# Soft White
$R2 = [System.Drawing.Color]::FromArgb(255, 218, 224, 233)# Shadow White
$R3 = [System.Drawing.Color]::FromArgb(255, 255, 179, 198)# Inner Ear Pink
$rabPal = @{ 'O'=$O; 'R'=$R1; 'S'=$R2; 'P'=$R3; 'W'=$W; 'E'=$E; 'B'=$B; 'N'=$N }

# ----------------- CAT 32x32 -----------------
# 32x32 Cat Idle
$catIdle = @(
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"        OO            OO        ",
"       ODCO          OCDO       ",
"      ODCCCO        OCCCDO      ",
"      OCCCDO        ODCCCO      ",
"      OCCKDOOOOOOOOOODKCCO      ",
"     OCCKCCCCCCCCCCCCCCKCCO     ",
"    OCCCCCCCCCCCCCCCCCCCCCCCO   ",
"   OCCCCDCCCCCDCCCCDCCCCCCCCO   ",
"   OCCCCDCCCCCDCCCCDCCCCCCCCO   ",
"  OCCCCCCCCCCCCCCCCCCCCCCCCCO   ",
"  OCCCCEEOCCCCCCCCCCCEEOCCCCO   ",
"  OCCCEWEOCCCCCCCCCCEWEOCBCO    ",
"  OCCCCOEOCCCCCCCCCCCOEOCCCO    ",
"  OCCCCBCCCCCCCCCCCCCCCBCCCO    ",
"  OCCCCCCCCCCCCNCCCCCCCCCCCCO   ",
"   OCCCCCCCCCCONCCCCCCCOCCCO    ",
"    OCCKKKKKCCCCCCCCCCCCCO      ",
"    OCCCCKKKCCCCCCCCCCCCO  OO   ",
"    OCCCCCCKKCCCCCCCCCCCCO OCO  ",
"    OCCCCCCCCCCCCCCCCCCCCCOOCO  ",
"     OCCCCOCCCCCOCCCCCOCCCCO    ",
"      OOOO OOOOO OOOOO OOOO     ",
"                                ",
"                                ",
"                                ",
"                                "
)

# Cat Walk
$catWalk = @(
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"        OO            OO        ",
"       ODCO          OCDO       ",
"      ODCCCO        OCCCDO      ",
"      OCCCDO        ODCCCO      ",
"      OCCKDOOOOOOOOOODKCCO      ",
"     OCCKCCCCCCCCCCCCCCKCCO     ",
"    OCCCCCCCCCCCCCCCCCCCCCCCO   ",
"   OCCCCDCCCCCDCCCCDCCCCCCCCO   ",
"   OCCCCDCCCCCDCCCCDCCCCCCCCO   ",
"  OCCCCCCCCCCCCCCCCCCCCCCCCCO   ",
"  OCCCCEEOCCCCCCCCCCCEEOCCCCO   ",
"  OCCCEWEOCCCCCCCCCCEWEOCBCO    ",
"  OCCCCOEOCCCCCCCCCCCOEOCCCO    ",
"  OCCCCBCCCCCCCCCCCCCCCBCCCO    ",
"  OCCCCCCCCCCCCNCCCCCCCCCCCCO   ",
"   OCCCCCCCCCCONCCCCCCCOCCCO    ",
"    OCCKKKKKCCCCCCCCCCCCCO OO   ",
"    OCCCCKKKCCCCCCCCCCCCCOOCO   ",
"     OCCCCCKKCCCCCCCCCCCCCOCO   ",
"     OCCCCCCCCCCCCCCCCCCCCOO    ",
"     OOOO OCCCCO   OCCCCO       ",
"          OOOOO     OOOO        ",
"                                ",
"                                ",
"                                ",
"                                "
)

# Cat Sleep
$catSleep = @(
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"        OO            OO        ",
"       ODCO          OCDO       ",
"      ODCCCOOOOOOOOOOCCCDO      ",
"     OCCKCCCCCCCCCCCCCCKCCO     ",
"    OCCCCCCCCCCCCCCCCCCCCCCCO   ",
"   OCCCCDCCCCCDCCCCDCCCCCCCCO   ",
"   OCCCCCCCCCCCCCCCCCCCCCCCCCO  ",
"   OCCCCBCCCCCCCCCCCCCCCBCCCCO  ",
"   OCCCOOOOCCCCCCCCCCCOOOOCCO   ",
"   OCCCCCCCCCCCONCCCCCCCCCCCO   ",
"    OCCKKKKKCCCCCCCCCCCCCO OO   ",
"    OCCCCKKKCCCCCCCCCCCCCOCO    ",
"    OCCCCCCKKCCCCCCCCCCCOCO     ",
"     OCCCCCCCCCCCCCCCCCCCO      ",
"      OOOOOOOOOOOOOOOOOOO       ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                "
)

# ----------------- DOG 32x32 -----------------
$dogIdle = @(
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"                                ",
"       OOOO          OOOO       ",
"      OSSSDO        ODSSSO      ",
"     OSSSSDO        ODSSSSO     ",
"     OSSSSDDOOOOOOOODDSSSSO     ",
"     OSSSDDDDDDDDDDDDDSSSSO     ",
"     OSDDDDDDDDDDDDDDDDSDSO     ",
"      ODDDDDDDDDDDDDDDDDDO      ",
"     ODDDDDDDDDDDDDDDDDDDDO     ",
"    ODDDDDDDDDDDDDDDDDDDDDDO    ",
"    ODDDDEEODDDDDDDDDDEEODDDO   ",
"    ODDDDEWODDDDDDDDDDEWODDDO   ",
"    ODDDDOOODDDDDDDDDDOOODDDO   ",
"    ODDDBDDDDDNNDDDDDDDBDDDDO   ",
"    ODDDDDDODNMMNDODDDDDDDDDO   ",
"     ODDDDDDMMMMMMDDDDDDDDDO    ",
"      ODDDDDDMMMMDDDDDDDDDO     ",
"     ODDDDDDDDDDDDDDDDDDDDDO    ",
"     ODDDDDDDDDDDDDDDDDDDDDO OO ",
"     ODDDDDDDDDDDDDDDDDDDDDDOCO ",
"     ODDDDDDDDDDDDDDDDDDDDDDOCO ",
"      ODDDO ODDDO ODDDO ODDDO   ",
"       OOO   OOO   OOO   OOO    ",
"                                ",
"                                ",
"                                ",
"                                "
)

# ----------------- RABBIT 32x32 -----------------
$rabIdle = @(
"                                ",
"                                ",
"       OO              OO       ",
"      ORPO            OPRO      ",
"      ORPPO          OPPRO      ",
"      ORPPO          OPPRO      ",
"      ORPPO          OPPRO      ",
"      ORPPO          OPPRO      ",
"      ORPPO          OPPRO      ",
"      ORRPOO        OOPRRO      ",
"      ORRRRROOOOOOOORRRRRO      ",
"     ORRRRRRRRRRRRRRRRRRRRO     ",
"    ORRRRRRRRRRRRRRRRRRRRRRO    ",
"   ORRRRRRRRRRRRRRRRRRRRRRRRO   ",
"   ORRRREEOORRRRRRRREEOORRRRO   ",
"   ORRRREWPORRRRRRRREWPORRRRO   ",
"   ORRRROOPORRRRRRRROOPORRRRO   ",
"   ORRRBPPPORRRRRRRBPPPORRRRO   ",
"   ORRRRRRRRRRNNRRRRRRRRRRRRO   ",
"    ORRRRRRRRRNPNRRRRRRRRRRRO   ",
"     ORRRRRRRRPPPRRRRRRRRRRO    ",
"     ORRRRRRRRRRRRRRRRRRRRRO    ",
"    ORRRRRRRRRRRRRRRRRRRRRRO    ",
"    ORRRRRRRRRRRRRRRRRRRRRRO    ",
"   OORRRRRRRRRRRRRRRRRRRRRRO    ",
"   ORORRRRRRRRRRRRRRRRRRRRRO    ",
"    OORRRO ORRRO  ORRRO ORRO    ",
"      OOO   OOO    OOO   OO     ",
"                                ",
"                                ",
"                                ",
"                                "
)

$baseAssets = "c:\Users\admin\OneDrive\Desktop\DesktopPet\Assets\Pets"
Create-PixelSprite -Path "$baseAssets\Cat\Idle.png" -Grid $catIdle -Palette $catPal
Create-PixelSprite -Path "$baseAssets\Cat\Walk.png" -Grid $catWalk -Palette $catPal
Create-PixelSprite -Path "$baseAssets\Cat\Run.png" -Grid $catWalk -Palette $catPal
Create-PixelSprite -Path "$baseAssets\Cat\Sleep.png" -Grid $catSleep -Palette $catPal
Create-PixelSprite -Path "$baseAssets\Cat\Eat.png" -Grid $catIdle -Palette $catPal
Create-PixelSprite -Path "$baseAssets\Cat\Happy.png" -Grid $catIdle -Palette $catPal
Create-PixelSprite -Path "$baseAssets\Cat\Bath.png" -Grid $catIdle -Palette $catPal

Create-PixelSprite -Path "$baseAssets\Dog\Idle.png" -Grid $dogIdle -Palette $dogPal
Create-PixelSprite -Path "$baseAssets\Dog\Walk.png" -Grid $dogIdle -Palette $dogPal
Create-PixelSprite -Path "$baseAssets\Dog\Sleep.png" -Grid $dogIdle -Palette $dogPal

Create-PixelSprite -Path "$baseAssets\Rabbit\Idle.png" -Grid $rabIdle -Palette $rabPal
Create-PixelSprite -Path "$baseAssets\Rabbit\Walk.png" -Grid $rabIdle -Palette $rabPal
Create-PixelSprite -Path "$baseAssets\Rabbit\Sleep.png" -Grid $rabIdle -Palette $rabPal

Write-Output "Pixel sprites created successfully."
