# =============================================================================
# Restore-CameraSettings.ps1
#
# Automatise les trois gestes de la FUJIFILM Tether App qui chargent un fichier
# de reglages dans le boitier :
#     menu Appareil photo -> Restauration des parametres -> choix du fichier
#
# CE QUE FAIT CE SCRIPT : il pilote l'application de Fujifilm au clavier.
# Il n'envoie AUCUNE commande USB lui-meme ; c'est le logiciel du constructeur
# qui ecrit dans l'appareil, par sa fonction officiellement supportee sur X-T30.
#
# Navigation choisie : Alt, Droite, Bas, Fin, Entree. Elle ne depend ni de la
# position de la fenetre ni de la resolution, contrairement a des clics au pixel.
# « Fin » selectionne la DERNIERE entree du menu, qui est la restauration.
#
# Usage :
#   .\Restore-CameraSettings.ps1 -File "C:\...\mon-fichier.dat"
#   .\Restore-CameraSettings.ps1 -File "..." -Preview     (s'arrete avant de valider)
# =============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$File,
    # S'arrete juste avant d'activer la restauration et enregistre une capture,
    # pour verifier que la bonne entree de menu est surlignee.
    [switch]$Preview,
    [int]$TimeoutSeconds = 40
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;using System.Text;using System.Collections.Generic;
public static class Tether{
 public delegate bool P(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumWindows(P cb,IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p,P cb,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int m);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder s,int m);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out R r);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string l);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr p);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool at);
 public struct R{public int L,T,Rt,B;}
 public static void Focus(IntPtr w){
   uint c=GetCurrentThreadId(), f=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
   bool a = f!=c && AttachThreadInput(c,f,true);
   try{ ShowWindow(w,9); BringWindowToTop(w); SetForegroundWindow(w); }
   finally{ if(a) AttachThreadInput(c,f,false); }
 }
 // Cherche la fenetre principale. Une fenetre reduite se trouve a -32000 : on la
 // retient quand meme en repli, car Focus() la restaurera.
 public static IntPtr FindByTitle(string needle){
   IntPtr best=IntPtr.Zero, fallback=IntPtr.Zero;
   EnumWindows(delegate(IntPtr h,IntPtr l){
     if(IsWindowVisible(h)){
       var sb=new StringBuilder(256); GetWindowText(h,sb,256);
       if(sb.ToString().IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0){
         R r; GetWindowRect(h,out r);
         if(r.L>-30000 && (r.Rt-r.L)>300) best=h;
         else if((r.Rt-r.L)>100 || r.L<-30000) fallback=h;
       }
     }
     return true; },IntPtr.Zero);
   return best!=IntPtr.Zero ? best : fallback;
 }
 public static IntPtr FindDialog(){
   IntPtr found=IntPtr.Zero;
   EnumWindows(delegate(IntPtr h,IntPtr l){
     if(IsWindowVisible(h)){
       var cn=new StringBuilder(256); GetClassName(h,cn,256);
       if(cn.ToString()=="#32770") found=h;
     }
     return true; },IntPtr.Zero);
   return found;
 }
 public static IntPtr FindEdit(IntPtr parent){
   IntPtr found=IntPtr.Zero;
   EnumChildWindows(parent,delegate(IntPtr h,IntPtr l){
     var cn=new StringBuilder(256); GetClassName(h,cn,256);
     if(cn.ToString()=="Edit" && IsWindowVisible(h) && found==IntPtr.Zero) found=h;
     return true; },IntPtr.Zero);
   return found;
 }
}
'@

# Envoie une frappe UNIQUEMENT si la fenetre visee a bien le focus.
# Sans ce garde-fou, une application qui vole le focus recevrait nos touches —
# y compris « Entree ». On prefere abandonner plutot que taper a l'aveugle.
function Send-GuardedKey {
    param([IntPtr]$Window, [string]$Keys, [int]$PauseMs = 500)
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        if ([Tether]::GetForegroundWindow() -eq $Window) { break }
        [Tether]::Focus($Window)
        Start-Sleep -Milliseconds 300
    }
    if ([Tether]::GetForegroundWindow() -ne $Window) {
        throw "La fenetre visee a perdu le focus (une autre application l'a pris). Aucune touche n'a ete envoyee ; rien n'a ete modifie. Ferme les fenetres qui s'activent seules et relance."
    }
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds $PauseMs
}

function Save-Shot([string]$name) {
    $w = [Tether]::FindByTitle('TETHER APP')
    if ($w -eq [IntPtr]::Zero) { return }
    $r = New-Object Tether+R; [Tether]::GetWindowRect($w, [ref]$r) | Out-Null
    $dir = Join-Path $PSScriptRoot 'captures'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $bmp = New-Object System.Drawing.Bitmap(([Math]::Min(900, $r.Rt - $r.L)), 320)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $dir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

# --- Verifications prealables -------------------------------------------------
$File = (Resolve-Path -LiteralPath $File).Path
$bytes = [System.IO.File]::ReadAllBytes($File)
if ($bytes.Length -ne 5628) { throw "Ce fichier fait $($bytes.Length) octets ; un backup X-T30 en fait 5628." }
if ([System.Text.Encoding]::ASCII.GetString($bytes, 0, 8) -ne 'FUJIFILM') { throw "Signature FUJIFILM absente : ce n'est pas un fichier de reglages." }
$model = ''
for ($i = 0x14; $i -lt 0x34 -and $bytes[$i] -ne 0; $i++) { $model += [char]$bytes[$i] }
if ($model -ne 'X-T30') { throw "Ce fichier provient d'un $model, pas d'un X-T30." }
# somme de controle (voir docs/10-piste-backup-c1c7.md)
$excluded = @(176, 177, 3772); $total = 0
for ($i = 0; $i -lt $bytes.Length; $i++) { if ($excluded -notcontains $i) { $total += $bytes[$i] } }
$computed = ($total + 0xE1E5) -band 0xFFFF
$stored = $bytes[176] + ($bytes[177] * 256)
if ($computed -ne $stored) { throw ("Somme de controle incoherente (stockee 0x{0:X4}, calculee 0x{1:X4}) : fichier refuse." -f $stored, $computed) }
Write-Host "Fichier valide : $File"
Write-Host ("  modele {0}, {1} octets, somme 0x{2:X4} coherente" -f $model, $bytes.Length, $stored)

$camera = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match 'VID_04CB' }
if (-not $camera) { throw "Aucun appareil Fujifilm detecte. Rebranche-le et reveille-le." }
Write-Host "Appareil detecte : $($camera.FriendlyName)"

$tether = [Tether]::FindByTitle('TETHER APP')
if ($tether -eq [IntPtr]::Zero) {
    # Fenetre absente : on tente de lancer l'application officielle nous-memes.
    $exe = 'C:\Program Files\FUJIFILM_TetherApp\FUJIFILM_TetherApp.exe'
    if (-not (Test-Path $exe)) { throw "La FUJIFILM Tether App n'est pas ouverte et son executable est introuvable ($exe)." }
    Write-Host "Lancement de la FUJIFILM Tether App..."
    Start-Process $exe | Out-Null
    for ($i = 0; $i -lt 25; $i++) {
        Start-Sleep -Milliseconds 800
        $tether = [Tether]::FindByTitle('TETHER APP')
        if ($tether -ne [IntPtr]::Zero) { break }
    }
    if ($tether -eq [IntPtr]::Zero) { throw "La Tether App a ete lancee mais sa fenetre n'est pas apparue." }
    Start-Sleep -Seconds 3   # laisser le temps a la detection de l'appareil
}

# --- Navigation dans le menu --------------------------------------------------
for ($i = 0; $i -lt 6; $i++) {
    [Tether]::Focus($tether); Start-Sleep -Milliseconds 350
    if ([Tether]::GetForegroundWindow() -eq $tether) { break }
}
if ([Tether]::GetForegroundWindow() -ne $tether) { throw "Impossible de mettre la Tether App au premier plan." }

Send-GuardedKey $tether '%'       500   # barre de menus
Send-GuardedKey $tether '{RIGHT}' 500   # Appareil photo
Send-GuardedKey $tether '{DOWN}'  400   # ouvre le sous-menu
Send-GuardedKey $tether '{END}'   600   # derniere entree = Restauration
Save-Shot 'menu-surligne'

if ($Preview) {
    Send-GuardedKey $tether '{ESC}' 200
    Send-GuardedKey $tether '{ESC}' 200
    Write-Host ""
    Write-Host "MODE APERCU : rien n'a ete active."
    Write-Host "Verifie captures\menu-surligne.png : l'entree surlignee doit etre"
    Write-Host "  « Restauration des parametres de l'appareil »."
    return
}

Send-GuardedKey $tether '{ENTER}' 300
Write-Host "Restauration demandee, attente de la boite de dialogue..."

# --- Boite de selection du fichier -------------------------------------------
$dialog = [IntPtr]::Zero
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 400
    $dialog = [Tether]::FindDialog()
    if ($dialog -ne [IntPtr]::Zero) { break }
}
if ($dialog -eq [IntPtr]::Zero) { throw "La boite de selection du fichier ne s'est pas ouverte." }

$edit = [Tether]::FindEdit($dialog)
if ($edit -eq [IntPtr]::Zero) { throw "Champ de nom de fichier introuvable dans la boite de dialogue." }
[Tether]::SendMessage($edit, 0x000C, [IntPtr]::Zero, $File) | Out-Null   # WM_SETTEXT
Start-Sleep -Milliseconds 500
Send-GuardedKey $dialog '{ENTER}' 300
Write-Host "Fichier valide, transfert en cours..."

# --- Attente de la fin --------------------------------------------------------
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    if ([Tether]::FindDialog() -eq [IntPtr]::Zero) { break }
}
Start-Sleep -Seconds 2
Save-Shot 'apres-restauration'
Write-Host ""
Write-Host "Termine. Capture : captures\apres-restauration.png"
Write-Host "Verifie sur le boitier que la banque visee porte bien son nouveau nom."
