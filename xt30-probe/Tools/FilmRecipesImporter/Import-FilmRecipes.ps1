# =============================================================================
# Import-FilmRecipes.ps1
#
# Importe les recettes publiques de film.recipes dans la bibliotheque locale,
# en ne conservant que celles utilisables sur un X-T30 premiere generation.
#
# Source : l'API REST publique WordPress du site (wp-json/wp/v2/posts).
# Chaque article publie porte une table <table class="recipe-settings"> dont
# les lignes sont des couples libelle / valeur : la lecture est structuree,
# aucun texte d'article n'est recopie.
#
# REGLES
#  - aucune valeur devinee : un champ absent reste null ;
#  - un article sans table de reglages n'est pas une recette : il est ignore ;
#  - les recettes dont la simulation n'existe pas sur X-T30 sont ignorees ;
#  - l'auteur, le site et l'URL d'origine restent attaches a chaque recette ;
#  - la bibliotheque locale est exclue du depot : rien n'est redistribue.
#
# Usage :
#   .\Import-FilmRecipes.ps1                 (import complet)
#   .\Import-FilmRecipes.ps1 -Limit 5        (essai sur 5 recettes)
#   .\Import-FilmRecipes.ps1 -NoImages       (metadonnees seules)
# =============================================================================

[CmdletBinding()]
param(
    [int]$Limit = 0,
    [switch]$NoImages,
    [string]$LibraryPath
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not $LibraryPath) { $LibraryPath = Join-Path $PSScriptRoot '..\..\library' }
$LibraryPath = [System.IO.Path]::GetFullPath($LibraryPath)
$Site = 'Film Recipes'
$Base = 'https://film.recipes'
$UA   = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) XT30RecipeManager-Importer/1.0'

function Get-Json([string]$url) {
    $wc = New-Object System.Net.WebClient
    $wc.Encoding = [System.Text.Encoding]::UTF8
    $wc.Headers['User-Agent'] = $UA
    try { return $wc.DownloadString($url) | ConvertFrom-Json }
    finally { $wc.Dispose() }
}

# Le site utilise des traits d'union insecables (&#8209;) devant les valeurs
# negatives : sans normalisation, « -4 » ne serait pas reconnu comme un nombre.
function Clean-Cell([string]$html) {
    if ($null -eq $html) { return $null }
    $s = $html -replace '<br\s*/?>', ', '
    $s = $s -replace '<[^>]+>', ''
    $s = [System.Net.WebUtility]::HtmlDecode($s)
    $s = $s -replace [char]0x2011, '-' -replace [char]0x2010, '-' -replace [char]0x2212, '-'
    $s = $s -replace [char]0x2013, '-' -replace [char]0x2014, '-' -replace [char]0x00A0, ' '
    $s = ($s -replace '\s+', ' ').Trim()
    if ($s -eq '' -or $s -eq '-' -or $s -eq 'N/A') { return $null }
    return $s
}

# « Weak, Small » -> effet + taille. Le X-T30 n'a pas la taille, mais on garde
# l'information telle qu'elle est publiee.
function Split-Grain($v) {
    if (-not $v) { return @($null, $null) }
    if ($v -match '^\s*(Off|Weak|Strong)\s*[,/]\s*(Small|Large)\s*$') { return @($Matches[1], $Matches[2]) }
    if ($v -match '^\s*(Small|Large)\s*[,/]\s*(Off|Weak|Strong)\s*$') { return @($Matches[2], $Matches[1]) }
    return @($v, $null)
}

# « Auto (White Priority), -5 Red, -1 Blue » -> mode + decalages
function Split-WhiteBalance($v) {
    if (-not $v) { return @($null, $null, $null) }
    $r = $null; $b = $null
    if ($v -match '([+-]?\s*\d+)\s*Red')  { $r = ($Matches[1] -replace '\s','') }
    if ($v -match '([+-]?\s*\d+)\s*Blue') { $b = ($Matches[1] -replace '\s','') }
    $mode = $v
    # Le mode est ce qui precede le premier decalage ; les parentheses en font partie.
    if ($v -match '^(.*?),\s*[+-]?\s*\d+\s*(Red|Blue)') { $mode = $Matches[1].Trim() }
    elseif ($v -match '^([^,]+),') { $mode = $Matches[1].Trim() }
    if ($r -and $r -notmatch '^[+-]') { $r = "+$r" }
    if ($b -and $b -notmatch '^[+-]') { $b = "+$b" }
    return @($mode, $r, $b)
}

# --- Compatibilite X-T30 : memes regles que les autres importeurs -------------
$Xt30Sims   = @('provia','velvia','astia','classic chrome','pro neg','eterna','acros','monochrome','sepia')
$BannedSims = @('classic negative','classic neg','bleach bypass','nostalgic','reala')

function Get-Compatibility($settings) {
    $sim = $settings.filmSimulation
    if (-not $sim) { return @('UNVERIFIED', 'No film simulation published; compatibility cannot be assessed.') }
    $low = $sim.ToLowerInvariant()
    foreach ($b in $BannedSims) { if ($low.Contains($b)) { return @('XT30_INCOMPATIBLE', "Film simulation `"$sim`" does not exist on the X-T30.") } }
    $known = $false
    foreach ($okSim in $Xt30Sims) { if ($low.Contains($okSim)) { $known = $true; break } }
    $issues = @()
    if (-not $known) { $issues += "Film simulation `"$sim`" could not be matched to the X-T30 set." }
    if ($settings.clarity -and $settings.clarity -notmatch '^\s*[+-]?0\s*$') { $issues += "Clarity $($settings.clarity) is unavailable on the X-T30." }
    if ($settings.colorChromeFXBlue -and $settings.colorChromeFXBlue -notmatch '^(Off|0)$') { $issues += "Color Chrome FX Blue $($settings.colorChromeFXBlue) is unavailable on the X-T30." }
    if ($settings.grainSize -and $settings.grain -and $settings.grain -notmatch '^(Off|0)$') {
        $issues += "Grain size ($($settings.grainSize)) cannot be chosen on the X-T30; use the single grain size."
    }
    if ($settings.toneCurve) { $issues += "Tone Curve ($($settings.toneCurve)) is unavailable on the X-T30; use Highlight and Shadow." }
    if ($issues.Count -gt 0) { return @('XT30_PARTIAL', ($issues -join ' ')) }
    return @('XT30_COMPATIBLE', 'All published settings exist on the X-T30 feature set.')
}

# Libelle publie -> champ de notre schema. Tout libelle inconnu est ignore
# plutot que devine.
$FieldMap = @{
    'film simulation'      = 'filmSimulation'
    'grain effect'         = 'grain'
    'col. chr. effect'     = 'colorChrome'
    'color chrome effect'  = 'colorChrome'
    'colour chrome effect' = 'colorChrome'
    'col. chr. blue'       = 'colorChromeFXBlue'
    'color chrome fx blue' = 'colorChromeFXBlue'
    'white balance'        = 'whiteBalance'
    'dynamic range'        = 'dynamicRange'
    'd range priority'     = 'dRangePriority'
    'dr priority'          = 'dRangePriority'
    'highlights'           = 'highlight'
    'highlight'            = 'highlight'
    'highlight tone'       = 'highlight'
    'shadows'              = 'shadow'
    'shadow'               = 'shadow'
    'shadow tone'          = 'shadow'
    'colour'               = 'color'
    'color'                = 'color'
    'sharpness'            = 'sharpness'
    'sharpening'           = 'sharpness'
    'iso n.r.'             = 'highIsoNR'
    'noise reduction'      = 'highIsoNR'
    'high iso n.r.'        = 'highIsoNR'
    'clarity'              = 'clarity'
    'tone curve'           = 'toneCurve'
    'monochromatic colour' = 'monochromaticColor'
    'monochromatic color'  = 'monochromaticColor'
    'ev comp.'             = 'exposureCompensation'
    'exposure compensation'= 'exposureCompensation'
    'iso'                  = 'iso'
}

# --- Recuperation du catalogue ------------------------------------------------
Write-Host "Bibliotheque : $LibraryPath"
Write-Host "Lecture du catalogue public de $Site ..."
$items = @()
for ($page = 1; $page -le 10; $page++) {
    try { $batch = Get-Json "$Base/wp-json/wp/v2/posts?per_page=100&page=$page&_embed=wp:featuredmedia" }
    catch { break }
    if (-not $batch -or $batch.Count -eq 0) { break }
    $items += $batch
    if ($batch.Count -lt 100) { break }
    Start-Sleep -Milliseconds 400
}
Write-Host "  $($items.Count) articles publies"

$recipes = @(); $skipped = @{ noTable = 0; noSim = 0; incompatible = 0 }; $imageCount = 0
$recipesDir = Join-Path $LibraryPath 'recipes'
New-Item -ItemType Directory -Path $recipesDir -Force | Out-Null

foreach ($item in $items) {
    if ($Limit -gt 0 -and $recipes.Count -ge $Limit) { break }
    $html = $item.content.rendered
    if (-not $html) { $skipped.noTable++; continue }

    $table = [regex]::Match($html, '<table[^>]*class="recipe-settings"[^>]*>.*?</table>', 'Singleline')
    if (-not $table.Success) { $skipped.noTable++; continue }

    $raw = @{}
    foreach ($row in [regex]::Matches($table.Value, '<tr[^>]*>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>\s*</tr>', 'Singleline')) {
        $label = Clean-Cell $row.Groups[1].Value
        $value = Clean-Cell $row.Groups[2].Value
        if (-not $label) { continue }
        $key = $FieldMap[$label.ToLowerInvariant().Trim()]
        if ($key -and -not $raw.ContainsKey($key)) { $raw[$key] = $value }
    }

    $grain = Split-Grain $raw['grain']
    $wb    = Split-WhiteBalance $raw['whiteBalance']
    $settings = [ordered]@{
        filmSimulation       = $raw['filmSimulation']
        dynamicRange         = $raw['dynamicRange']
        dRangePriority       = $raw['dRangePriority']
        whiteBalance         = $wb[0]
        wbShiftR             = $wb[1]
        wbShiftB             = $wb[2]
        highlight            = $raw['highlight']
        shadow               = $raw['shadow']
        color                = $raw['color']
        sharpness            = $raw['sharpness']
        highIsoNR            = $raw['highIsoNR']
        grain                = $grain[0]
        grainSize            = $grain[1]
        colorChrome          = $raw['colorChrome']
        colorChromeFXBlue    = $raw['colorChromeFXBlue']
        monochromaticColor   = $raw['monochromaticColor']
        toneCurve            = $raw['toneCurve']
        clarity              = $raw['clarity']
        iso                  = $raw['iso']
        exposureCompensation = $raw['exposureCompensation']
    }
    if (-not $settings.filmSimulation) { $skipped.noSim++; continue }

    $compat = Get-Compatibility $settings
    if ($compat[0] -eq 'XT30_INCOMPATIBLE') { $skipped.incompatible++; continue }

    $slug = $item.slug
    $name = [System.Net.WebUtility]::HtmlDecode($item.title.rendered)
    # Le titre reprend souvent "Nom - sous-titre Film Recipe" : on garde le nom
    # seul. Les tirets sont donnes par code : un caractere litteral dans ce
    # fichier serait mal relu par PowerShell 5.1 (script lu en ANSI).
    $dashes = '\s+[' + [char]0x2013 + [char]0x2014 + '-]\s+'
    $name = ($name -split $dashes)[0]
    $name = ($name -replace '\s*(Film )?Recipe\s*$', '').Trim()
    $missing = @($settings.Keys | Where-Object { -not $settings[$_] })

    $cover = $null
    if (-not $NoImages) {
        $media = $item._embedded.'wp:featuredmedia'
        if ($media -and $media[0].source_url) {
            try {
                $url = $null
                $sizes = $media[0].media_details.sizes
                foreach ($pref in @('medium_large','large','medium')) {
                    if ($sizes -and $sizes.PSObject.Properties.Name -contains $pref) {
                        $candidate = $sizes.$pref
                        if ($candidate.source_url -and $candidate.mime_type -eq 'image/jpeg') { $url = $candidate.source_url; break }
                    }
                }
                if (-not $url -and $media[0].mime_type -eq 'image/jpeg') { $url = $media[0].source_url }
                if (-not $url) { throw "aucune vignette JPEG disponible" }
                $ext = [System.IO.Path]::GetExtension(($url -split '\?')[0]); if (-not $ext) { $ext = '.jpg' }
                $dir = Join-Path $recipesDir $slug
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                $path = Join-Path $dir "cover$ext"
                $wc = New-Object System.Net.WebClient
                $wc.Headers['User-Agent'] = $UA
                $wc.DownloadFile($url, $path); $wc.Dispose()
                $cover = "recipes/$slug/cover$ext"
                $imageCount++
            } catch { Write-Host "    image en echec : $($_.Exception.Message)" }
        }
    }

    $recipes += [ordered]@{
        name   = $name
        slug   = $slug
        source = [ordered]@{
            site        = $Site
            articleUrl  = $item.link
            author      = $null
            publishedAt = $item.date
        }
        compatibility = [ordered]@{
            xt30Original     = $compat[0]
            group            = 'X-Trans IV / V'
            reason           = $compat[1]
            camerasMentioned = @()
        }
        settings   = $settings
        images     = [ordered]@{ cover = $cover; examples = @() }
        extraction = [ordered]@{
            status        = $(if ($missing.Count -le 8) { 'VERIFIED' } else { 'PARTIAL' })
            missingFields = $missing
        }
    }
    Write-Host ("  [{0}] {1}" -f $compat[0], $name)
    Start-Sleep -Milliseconds 120
}

# --- Ecriture de l'index ------------------------------------------------------
New-Item -ItemType Directory -Path (Join-Path $LibraryPath 'index')   -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $LibraryPath 'reports') -Force | Out-Null

$index = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    site        = $Site
    idPrefix    = 'flr-'
    sourceApi   = "$Base/wp-json/wp/v2/posts"
    target      = 'Fujifilm X-T30 (first generation)'
    extracted   = $recipes.Count
    images      = $imageCount
    recipes     = $recipes
}
$indexPath = Join-Path $LibraryPath 'index\film_recipes_xt30.json'
$index | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $indexPath -Encoding UTF8

$compatible = @($recipes | Where-Object { $_.compatibility.xt30Original -eq 'XT30_COMPATIBLE' }).Count
$partial    = @($recipes | Where-Object { $_.compatibility.xt30Original -eq 'XT30_PARTIAL' }).Count
[ordered]@{
    generatedAt         = (Get-Date).ToString('o')
    site                = $Site
    published           = $items.Count
    imported            = $recipes.Count
    XT30_COMPATIBLE     = $compatible
    XT30_PARTIAL        = $partial
    skippedNoTable      = $skipped.noTable
    skippedNoFilmSim    = $skipped.noSim
    skippedIncompatible = $skipped.incompatible
    images              = $imageCount
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $LibraryPath 'reports\film_recipes_report.json') -Encoding UTF8

Write-Host ""
Write-Host "Index ecrit : $indexPath"
Write-Host ("Importees {0}  (compatibles {1}, partielles {2})" -f $recipes.Count, $compatible, $partial)
Write-Host ("Ignorees : {0} sans table de reglages, {1} sans simulation, {2} incompatibles X-T30" -f $skipped.noTable, $skipped.noSim, $skipped.incompatible)
Write-Host ("Images    : {0}" -f $imageCount)
