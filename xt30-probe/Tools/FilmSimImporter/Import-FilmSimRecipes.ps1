# =============================================================================
# Import-FilmSimRecipes.ps1
#
# Importe les recettes publiques de filmsimrecipes.com dans la bibliotheque
# locale, en ne conservant que celles utilisables sur un X-T30 premiere
# generation.
#
# Source : l'API REST publique WordPress du site (wp-json/wp/v2/portfolio).
# Les reglages y sont deja structures (champs ACF) : aucun parsing de page,
# aucun texte d'article recopie.
#
# REGLES
#  - aucune valeur devinee : un champ absent reste null ;
#  - les recettes marquees "pro" (reservees aux abonnes) sont ignorees ;
#  - les recettes dont la simulation n'existe pas sur X-T30 sont ignorees ;
#  - l'auteur, le site et l'URL d'origine restent attaches a chaque recette ;
#  - la bibliotheque locale est exclue du depot par .gitignore : rien n'est
#    redistribue.
#
# Usage :
#   .\Import-FilmSimRecipes.ps1                 (import complet)
#   .\Import-FilmSimRecipes.ps1 -Limit 5        (essai sur 5 recettes)
#   .\Import-FilmSimRecipes.ps1 -NoImages       (metadonnees seules)
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
$Site = 'Filmsim Recipes'
$Base = 'https://filmsimrecipes.com'
$UA   = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) XT30RecipeManager-Importer/1.0'

function Get-Json([string]$url) {
    $wc = New-Object System.Net.WebClient
    $wc.Encoding = [System.Text.Encoding]::UTF8
    $wc.Headers['User-Agent'] = $UA
    try { return $wc.DownloadString($url) | ConvertFrom-Json }
    finally { $wc.Dispose() }
}

# --- Valeurs : jamais devinees -------------------------------------------------
function Clean-Value($v) {
    if ($null -eq $v) { return $null }
    $s = ([string]$v).Trim()
    if ($s -eq '' -or $s -eq 'False' -or $s -eq 'N/A' -or $s -eq '-') { return $null }
    return $s
}

# « Strong / Large » -> effet + taille ; le X-T30 n'a pas la taille, mais on
# conserve l'information telle qu'elle est publiee.
function Split-Grain($v) {
    $s = Clean-Value $v
    if (-not $s) { return @($null, $null) }
    if ($s -match '^\s*(Off|Weak|Strong)\s*[/,]\s*(Small|Large)\s*$') {
        return @($Matches[1], $Matches[2])
    }
    # Certaines fiches inversent l'ordre : « Small / Weak »
    if ($s -match '^\s*(Small|Large)\s*[/,]\s*(Off|Weak|Strong)\s*$') {
        return @($Matches[2], $Matches[1])
    }
    return @($s, $null)
}

# « Auto, +5 Red, -6 Blue » -> mode + decalages
function Split-WhiteBalance($v) {
    $s = Clean-Value $v
    if (-not $s) { return @($null, $null, $null) }
    $r = $null; $b = $null
    if ($s -match '([+-]?\s*\d+)\s*Red')  { $r = ($Matches[1] -replace '\s','') }
    if ($s -match '([+-]?\s*\d+)\s*Blue') { $b = ($Matches[1] -replace '\s','') }
    $mode = $s
    if ($s -match '^([^,]+),') { $mode = $Matches[1].Trim() }
    if ($r -and $r -notmatch '^[+-]') { $r = "+$r" }
    if ($b -and $b -notmatch '^[+-]') { $b = "+$b" }
    return @($mode, $r, $b)
}

# --- Compatibilite X-T30 : memes regles que l'importeur Fuji X Weekly ---------
$Xt30Sims  = @('provia','velvia','astia','classic chrome','pro neg','eterna','acros','monochrome','sepia')
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
    # La taille du grain n'a de sens que si le grain est actif : inutile de
    # signaler une limitation sur un reglage desactive.
    if ($settings.grainSize -and $settings.grain -and $settings.grain -notmatch '^(Off|0)$') {
        $issues += "Grain size ($($settings.grainSize)) cannot be chosen on the X-T30; use the single grain size."
    }
    if ($settings.smoothSkin -and $settings.smoothSkin -notmatch '^(Off|0)$') { $issues += "Smooth Skin Effect $($settings.smoothSkin) is unavailable on the X-T30." }
    if ($issues.Count -gt 0) { return @('XT30_PARTIAL', ($issues -join ' ')) }
    return @('XT30_COMPATIBLE', 'All published settings exist on the X-T30 feature set.')
}

# --- Recuperation du catalogue ------------------------------------------------
Write-Host "Bibliotheque : $LibraryPath"
Write-Host "Lecture du catalogue public de $Site ..."
$items = @()
for ($page = 1; $page -le 10; $page++) {
    try { $batch = Get-Json "$Base/wp-json/wp/v2/portfolio?per_page=100&page=$page&_embed=1" }
    catch { break }
    if (-not $batch -or $batch.Count -eq 0) { break }
    $items += $batch
    if ($batch.Count -lt 100) { break }
}
Write-Host "  $($items.Count) recettes publiees"

$recipes = @(); $skipped = @{ pro = 0; noSim = 0; incompatible = 0 }; $imageCount = 0
$recipesDir = Join-Path $LibraryPath 'recipes'
New-Item -ItemType Directory -Path $recipesDir -Force | Out-Null

foreach ($item in $items) {
    if ($Limit -gt 0 -and $recipes.Count -ge $Limit) { break }
    $acf = $item.acf
    if (-not $acf) { continue }
    if ("$($acf.pro)" -eq 'True') { $skipped.pro++; continue }

    $grain = Split-Grain $acf.grain_effect
    $wb    = Split-WhiteBalance $acf.white_balance
    $settings = [ordered]@{
        filmSimulation       = Clean-Value $acf.film_simulation
        dynamicRange         = Clean-Value $acf.dynamic_range
        dRangePriority       = Clean-Value $acf.d_range_priority
        whiteBalance         = $wb[0]
        wbShiftR             = $wb[1]
        wbShiftB             = $wb[2]
        highlight            = Clean-Value $acf.highlight
        shadow               = Clean-Value $acf.shadow
        color                = Clean-Value $acf.color
        sharpness            = Clean-Value $acf.sharpness
        highIsoNR            = Clean-Value $acf.noise_reduction
        grain                = $grain[0]
        grainSize            = $grain[1]
        colorChrome          = Clean-Value $acf.color_chrome_effect
        colorChromeFXBlue    = Clean-Value $acf.color_chrome_fx_blue
        smoothSkin           = Clean-Value $acf.smooth_skin_effect
        clarity              = Clean-Value $acf.clarity
        iso                  = Clean-Value $acf.iso_range
        exposureCompensation = Clean-Value $acf.exposure_compensation
    }
    if (-not $settings.filmSimulation) { $skipped.noSim++; continue }

    $compat = Get-Compatibility $settings
    if ($compat[0] -eq 'XT30_INCOMPATIBLE') { $skipped.incompatible++; continue }

    $slug = $item.slug
    $name = [System.Net.WebUtility]::HtmlDecode($item.title.rendered)
    $missing = @($settings.Keys | Where-Object { -not $settings[$_] })

    # Image de couverture : uniquement le media mis en avant, publiquement servi.
    $cover = $null
    if (-not $NoImages) {
        $media = $item._embedded.'wp:featuredmedia'
        if ($media -and $media[0].source_url) {
            try {
                # On prefere une version generee par le site plutot que l'original :
                # celui-ci peut etre un TIFF de plusieurs Mo, inutilisable comme
                # vignette (l'application plafonne de toute facon a 480 px).
                $url = $null
                $sizes = $media[0].media_details.sizes
                foreach ($pref in @('medium_large','large','woocommerce_single','medium')) {
                    if ($sizes -and $sizes.PSObject.Properties.Name -contains $pref) {
                        $candidate = $sizes.$pref
                        if ($candidate.source_url -and $candidate.mime_type -eq 'image/jpeg') { $url = $candidate.source_url; break }
                    }
                }
                # Si le site ne propose aucune vignette JPEG, on renonce a l'image :
                # l'original peut etre un TIFF de plusieurs Mo, inaffichable comme
                # couverture. Mieux vaut aucune image qu'un fichier inutilisable.
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
            author      = Clean-Value $acf.author
            publishedAt = $item.date
        }
        compatibility = [ordered]@{
            xt30Original     = $compat[0]
            group            = Clean-Value $acf.camera_model
            reason           = $compat[1]
            camerasMentioned = @(Clean-Value $acf.camera_model | Where-Object { $_ })
        }
        settings   = $settings
        images     = [ordered]@{ cover = $cover; examples = @() }
        extraction = [ordered]@{
            status        = $(if ($missing.Count -le 6) { 'VERIFIED' } else { 'PARTIAL' })
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
    idPrefix    = 'fsr-'
    sourceApi   = "$Base/wp-json/wp/v2/portfolio"
    target      = 'Fujifilm X-T30 (first generation)'
    extracted   = $recipes.Count
    images      = $imageCount
    recipes     = $recipes
}
$indexPath = Join-Path $LibraryPath 'index\filmsimrecipes_xt30.json'
$index | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $indexPath -Encoding UTF8

$compatible = @($recipes | Where-Object { $_.compatibility.xt30Original -eq 'XT30_COMPATIBLE' }).Count
$partial    = @($recipes | Where-Object { $_.compatibility.xt30Original -eq 'XT30_PARTIAL' }).Count
[ordered]@{
    generatedAt      = (Get-Date).ToString('o')
    site             = $Site
    published        = $items.Count
    imported         = $recipes.Count
    XT30_COMPATIBLE  = $compatible
    XT30_PARTIAL     = $partial
    skippedPro       = $skipped.pro
    skippedNoFilmSim = $skipped.noSim
    skippedIncompatible = $skipped.incompatible
    images           = $imageCount
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $LibraryPath 'reports\filmsimrecipes_report.json') -Encoding UTF8

Write-Host ""
Write-Host "Index ecrit : $indexPath"
Write-Host ("Importees {0}  (compatibles {1}, partielles {2})" -f $recipes.Count, $compatible, $partial)
Write-Host ("Ignorees : {0} pro, {1} sans simulation, {2} incompatibles X-T30" -f $skipped.pro, $skipped.noSim, $skipped.incompatible)
Write-Host ("Images    : {0}" -f $imageCount)
