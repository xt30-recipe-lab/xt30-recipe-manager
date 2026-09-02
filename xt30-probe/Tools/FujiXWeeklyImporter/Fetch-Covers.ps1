# =============================================================================
# Fetch-Covers.ps1
#
# Ajoute une image de couverture aux recettes deja importees de Fuji X Weekly.
#
# L'importeur d'origine ne collectait aucune image. Cette passe separee ne
# telecharge QUE l'image mise en avant de l'article (balise og:image), une par
# recette, dans la bibliotheque locale exclue du depot. L'URL de l'article et
# l'auteur restent attaches a chaque recette : rien n'est redistribue.
#
# Une passe distincte plutot qu'un nouvel import : les reglages deja extraits
# ne sont pas refaits, seule l'image manque.
#
# Usage :
#   .\Fetch-Covers.ps1                       (toutes les recettes sans image)
#   .\Fetch-Covers.ps1 -Limit 10             (essai)
#   .\Fetch-Covers.ps1 -Index autre.json     (autre index)
# =============================================================================

[CmdletBinding()]
param(
    [int]$Limit = 0,
    [string]$Index = 'fuji_x_weekly_xt30_full.json',
    [string]$LibraryPath
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not $LibraryPath) { $LibraryPath = Join-Path $PSScriptRoot '..\..\library' }
$LibraryPath = [System.IO.Path]::GetFullPath($LibraryPath)
$indexPath = Join-Path $LibraryPath "index\$Index"
if (-not (Test-Path $indexPath)) { throw "Index introuvable : $indexPath" }

$UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) XT30RecipeManager-Importer/1.0'
$recipesDir = Join-Path $LibraryPath 'recipes'
New-Item -ItemType Directory -Path $recipesDir -Force | Out-Null

Write-Host "Index : $indexPath"
$data = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host ("  {0} recettes" -f $data.recipes.Count)

$done = 0; $failed = 0; $already = 0

foreach ($r in $data.recipes) {
    if ($Limit -gt 0 -and $done -ge $Limit) { break }
    if ($r.images -and $r.images.cover) { $already++; continue }
    $url = $r.source.articleUrl
    if (-not $url) { continue }

    try {
        $page = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -Headers @{ 'User-Agent' = $UA }
        $html = $page.Content

        # Image mise en avant declaree par le site lui-meme.
        $m = [regex]::Match($html, '<meta[^>]+property=["'']og:image["''][^>]+content=["'']([^"'']+)["'']', 'IgnoreCase')
        if (-not $m.Success) { $m = [regex]::Match($html, '<meta[^>]+content=["'']([^"'']+)["''][^>]+property=["'']og:image["'']', 'IgnoreCase') }
        if (-not $m.Success) { throw "aucune og:image" }

        $img = $m.Groups[1].Value
        # WordPress.com sert des redimensionnements : une vignette suffit largement,
        # l'application plafonne l'affichage a 480 px.
        if ($img -notmatch '\?') { $img = $img + '?w=640' } else { $img = $img + '&w=640' }

        $slug = $r.slug
        $dir = Join-Path $recipesDir $slug
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $path = Join-Path $dir 'cover.jpg'

        $wc = New-Object System.Net.WebClient
        $wc.Headers['User-Agent'] = $UA
        try { $wc.DownloadFile($img, $path) } finally { $wc.Dispose() }

        if ((Get-Item $path).Length -lt 1000) { Remove-Item $path -Force; throw "image vide" }

        if (-not $r.images) {
            $r | Add-Member -NotePropertyName images -NotePropertyValue ([pscustomobject]@{ cover = "recipes/$slug/cover.jpg"; examples = @() }) -Force
        } else {
            $r.images.cover = "recipes/$slug/cover.jpg"
        }
        $done++
        if ($done % 25 -eq 0) { Write-Host ("  {0} couvertures" -f $done) }
    }
    catch {
        $failed++
        Write-Host ("  echec {0} : {1}" -f $r.slug, ($_.Exception.Message -split "`n")[0])
    }
    Start-Sleep -Milliseconds 250
}

$data | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $indexPath -Encoding UTF8

Write-Host ""
Write-Host ("Couvertures ajoutees : {0}   deja presentes : {1}   echecs : {2}" -f $done, $already, $failed)
Write-Host "Index mis a jour : $indexPath"
