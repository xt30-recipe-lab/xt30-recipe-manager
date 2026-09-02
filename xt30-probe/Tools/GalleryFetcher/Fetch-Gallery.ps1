# =============================================================================
# Fetch-Gallery.ps1
#
# Complete les recettes deja importees avec TOUTES les photos publiees dans
# leur article, et plus seulement la couverture.
#
# Une seule passe pour les trois catalogues : chaque index porte l'URL de
# l'article, il suffit de lire la page et d'y relever les images de contenu.
#
# REGLES
#  - seules les images servies par le site lui-meme sont retenues (uploads) ;
#  - les avatars, logos, boutons et pixels de suivi sont ecartes ;
#  - au plus -Max images par recette, en version redimensionnee (~1024 px) ;
#  - une recette deja pourvue n'est pas retelechargee : le script est reprenable.
#
# Usage :
#   .\Fetch-Gallery.ps1                              (tous les index)
#   .\Fetch-Gallery.ps1 -Index film_recipes_xt30.json
#   .\Fetch-Gallery.ps1 -Limit 5 -Max 4              (essai)
# =============================================================================

[CmdletBinding()]
param(
    [string]$Index,
    [int]$Limit = 0,
    [int]$Max = 6,
    [switch]$Force,
    [string]$LibraryPath
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not $LibraryPath) { $LibraryPath = Join-Path $PSScriptRoot '..\..\library' }
$LibraryPath = [System.IO.Path]::GetFullPath($LibraryPath)
$UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) XT30RecipeManager-Importer/1.0'
$recipesDir = Join-Path $LibraryPath 'recipes'
New-Item -ItemType Directory -Path $recipesDir -Force | Out-Null

# Vignettes d'interface, avatars et pixels de suivi : jamais des photos d'exemple.
$Reject = @('gravatar', 'avatar', 'logo', 'icon', 'badge', 'button', 'banner',
            'pixel.wp.com', 'stats.wp.com', 'spacer', 'emoji', 'smiley',
            'patreon', 'paypal', 'app-store', 'google-play', 'qr-')

function Is-Photo([string]$url) {
    $low = $url.ToLowerInvariant()
    if ($low -notmatch '\.(jpe?g|png)(\?|$)') { return $false }
    foreach ($bad in $Reject) { if ($low.Contains($bad)) { return $false } }
    # Une photo vient du stockage du site.
    if ($low -match 'wp-content/uploads' -or $low -match 'files\.wordpress\.com') { return $true }
    return $false
}

# Version redimensionnee : inutile de rapatrier un JPEG pleine resolution pour
# une vignette de 480 px.
function To-Sized([string]$url) {
    $clean = ($url -split '\?')[0]
    return $clean + '?w=1024'
}

# Le corps de l'article seulement : la page entiere contient aussi la barre
# laterale, le pied de page et les bandeaux « App Store ».
function Get-ArticleBody([string]$html) {
    foreach ($marker in @('class="entry-content', 'class="wp-block-post-content', '<article')) {
        $start = $html.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase)
        if ($start -lt 0) { continue }
        $rest = $html.Substring($start)
        foreach ($stop in @('id="comments"', 'class="entry-footer', 'class="wp-block-post-comments', '</article>')) {
            $end = $rest.IndexOf($stop, [StringComparison]::OrdinalIgnoreCase)
            if ($end -gt 500) { $rest = $rest.Substring(0, $end); break }
        }
        return $rest
    }
    return $html
}

# Retient les images du corps de l'article, dans l'ordre de publication.
function Get-ArticleImages([string]$html) {
    # Le corps d'abord ; s'il donne trop peu d'images, c'est que le decoupage n'a
    # pas trouve le bon marqueur : on retombe sur la page entiere, le filtrage par
    # taille suffit alors a ecarter bandeaux et logos.
    $body = Get-ArticleBody $html
    $result = Scan-Images $body
    if ($result.Count -lt 3) { $result = Scan-Images $html }
    return $result
}

function Scan-Images([string]$html) {
    $found = New-Object System.Collections.Generic.List[string]
    $seen = New-Object System.Collections.Generic.HashSet[string]
    foreach ($m in [regex]::Matches($html, '<img[^>]+>', 'IgnoreCase')) {
        $tag = $m.Value
        # Dimensions declarees : un bandeau ou une icone n'est jamais une photo.
        $w = [regex]::Match($tag, 'width=["'']?(\d+)', 'IgnoreCase')
        if ($w.Success -and [int]$w.Groups[1].Value -lt 400) { continue }
        $src = $null
        # data-orig-file / data-large-file portent l'original sur WordPress ;
        # a defaut on prend src.
        foreach ($attr in @('data-large-file', 'data-orig-file', 'src')) {
            $a = [regex]::Match($tag, $attr + '=["'']([^"'']+)["'']', 'IgnoreCase')
            if ($a.Success) { $src = $a.Groups[1].Value; break }
        }
        if (-not $src) { continue }
        if ($src.StartsWith('//')) { $src = 'https:' + $src }
        if (-not (Is-Photo $src)) { continue }
        $key = ($src -split '\?')[0]
        if ($seen.Add($key)) { $found.Add($key) }
    }
    return $found
}

$indexes = @()
if ($Index) { $indexes += (Join-Path $LibraryPath "index\$Index") }
else { $indexes = @(Get-ChildItem (Join-Path $LibraryPath 'index') -Filter *.json | ForEach-Object { $_.FullName }) }

$totalAdded = 0; $totalDone = 0; $totalSkipped = 0; $totalFailed = 0

foreach ($indexPath in $indexes) {
    if (-not (Test-Path $indexPath)) { Write-Host "Index introuvable : $indexPath"; continue }
    Write-Host ""
    Write-Host ("=== {0}" -f (Split-Path $indexPath -Leaf))
    $data = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $data.recipes) { Write-Host "  pas de recettes"; continue }
    $changed = $false

    foreach ($r in $data.recipes) {
        if ($Limit -gt 0 -and $totalDone -ge $Limit) { break }
        $url = $r.source.articleUrl
        if (-not $url) { continue }
        # Deja pourvue : on n'y retouche pas, le script reste reprenable.
        if (-not $Force -and $r.images -and $r.images.examples -and @($r.images.examples).Count -ge 2) { $totalSkipped++; continue }

        try {
            $page = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -Headers @{ 'User-Agent' = $UA }
            $images = Get-ArticleImages $page.Content
            if ($images.Count -eq 0) { throw "aucune photo dans l'article" }

            $slug = $r.slug
            $dir = Join-Path $recipesDir $slug
            New-Item -ItemType Directory -Path $dir -Force | Out-Null

            $saved = @()
            $sizes = New-Object System.Collections.Generic.HashSet[long]
            foreach ($img in $images) {
                if ($saved.Count -ge $Max) { break }
                $n = $saved.Count + 1
                $path = Join-Path $dir ("photo-{0}.jpg" -f $n)
                try {
                    $wc = New-Object System.Net.WebClient
                    $wc.Headers['User-Agent'] = $UA
                    try { $wc.DownloadFile((To-Sized $img), $path) } finally { $wc.Dispose() }
                    $length = (Get-Item $path).Length
                    # Un fichier leger est un bouton ou un logo, pas une photo de 1024 px ;
                    # une taille identique signale la meme image servie deux fois.
                    if ($length -lt 25000 -or -not $sizes.Add($length)) { Remove-Item $path -Force; continue }
                    $saved += "recipes/$slug/photo-$n.jpg"
                }
                catch { if (Test-Path $path) { Remove-Item $path -Force } }
                Start-Sleep -Milliseconds 60
            }
            if ($saved.Count -eq 0) { throw "aucune photo exploitable" }

            if (-not $r.images) {
                $r | Add-Member -NotePropertyName images -NotePropertyValue ([pscustomobject]@{ cover = $saved[0]; examples = $saved }) -Force
            }
            else {
                $r.images.examples = $saved
                if (-not $r.images.cover) { $r.images.cover = $saved[0] }
            }
            $changed = $true
            $totalAdded += $saved.Count
            $totalDone++
            if ($totalDone % 20 -eq 0) { Write-Host ("  {0} recettes illustrees, {1} photos" -f $totalDone, $totalAdded) }
        }
        catch {
            $totalFailed++
            if ($totalFailed -le 10) { Write-Host ("  echec {0} : {1}" -f $r.slug, ($_.Exception.Message -split "`n")[0]) }
        }
        Start-Sleep -Milliseconds 150
    }

    if ($changed) {
        $data | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $indexPath -Encoding UTF8
        Write-Host ("  index mis a jour : {0}" -f (Split-Path $indexPath -Leaf))
    }
}

Write-Host ""
Write-Host ("Recettes illustrees : {0}   photos ajoutees : {1}   deja pourvues : {2}   echecs : {3}" -f $totalDone, $totalAdded, $totalSkipped, $totalFailed)
