# External functional validation helpers. These inspect and interact with the
# normally launched application through Windows controls, never its model/API.
# No camera operation is implemented here; Scan Camera invokes the existing UI.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class LiveUiNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr window,int x,int y,int width,int height,bool repaint);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,IntPtr process);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint first,uint second,bool attach);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr window);
    public static void FocusWindow(IntPtr window) {
        uint current=GetCurrentThreadId(), foreground=GetWindowThreadProcessId(GetForegroundWindow(),IntPtr.Zero);
        bool attached=foreground!=current && AttachThreadInput(current,foreground,true);
        try { BringWindowToTop(window); SetForegroundWindow(window); }
        finally { if(attached) AttachThreadInput(current,foreground,false); }
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window,StringBuilder text,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window,StringBuilder text,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,string text);
}
'@
$script:LiveRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:LiveEvidence = Join-Path $script:LiveRoot 'validation\real-ui'
New-Item -ItemType Directory -Path $script:LiveEvidence -Force | Out-Null

function Get-LiveWindow {
    $appPath = Join-Path $script:LiveRoot 'xt30-recipe-manager.exe'
    $process = @(Get-Process -Name xt30-recipe-manager -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $appPath })
    if ($process.Count -ne 1) { throw "Expected one final application process, found $($process.Count)." }
    $process[0].Refresh()
    if ($process[0].MainWindowHandle -eq 0) { throw 'Application has no main window.' }
    return [System.Windows.Automation.AutomationElement]::FromHandle($process[0].MainWindowHandle)
}

function Focus-LiveWindow($Window) {
    $handle = [IntPtr]$Window.Current.NativeWindowHandle
    if ([LiveUiNative]::GetForegroundWindow() -eq $handle) { return }
    $shell = New-Object -ComObject WScript.Shell
    try { $shell.AppActivate($Window.Current.ProcessId) | Out-Null } finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
    for ($attempt=0; $attempt -lt 10; $attempt++) {
        [LiveUiNative]::FocusWindow($handle)
        Start-Sleep -Milliseconds 200
        if ([LiveUiNative]::GetForegroundWindow() -eq $handle) { return }
    }
}

function Find-LiveControl($Window, [string]$Name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$Name)
    $matches = $Window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    foreach ($control in $matches) { if (-not $control.Current.IsOffscreen) { return $control } }
    throw "Visible control not found: $Name"
}

function Invoke-LiveClick($Control, [int]$X=-1, [int]$Y=-1) {
    $state = $Control.Current
    if (-not $state.IsEnabled -or $state.IsOffscreen) { throw "Cannot activate disabled/hidden control: $($state.Name)" }
    $handle = [IntPtr]$state.NativeWindowHandle
    if ($handle -eq [IntPtr]::Zero) { throw 'Control has no native HWND.' }
    if ($X -lt 0) { $X = [int]($state.BoundingRectangle.Width/2) }
    if ($Y -lt 0) { $Y = [int]($state.BoundingRectangle.Height/2) }
    $position = [IntPtr](($Y -shl 16) -bor ($X -band 65535))
    [LiveUiNative]::PostMessage($handle,0x201,[IntPtr]1,$position) | Out-Null
    [LiveUiNative]::PostMessage($handle,0x202,[IntPtr]0,$position) | Out-Null
    Start-Sleep -Milliseconds 300
}

function Set-LiveText($Control, [string]$Text) {
    $pattern = $null
    if ($Control.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)) { $pattern.SetValue($Text) }
    else { [LiveUiNative]::SendMessage([IntPtr]$Control.Current.NativeWindowHandle,0xC,[IntPtr]0,$Text) | Out-Null }
    Start-Sleep -Milliseconds 200
}

function Get-LiveSnapshot($Window) {
    $result = @()
    $controls = $Window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    foreach ($control in $controls) {
        $current = $control.Current
        if ($current.IsOffscreen) { continue }
        $value = $null
        $pattern = $null
        if ($control.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)) { $value = $pattern.Current.Value }
        $class = New-Object System.Text.StringBuilder(256)
        [LiveUiNative]::GetClassName([IntPtr]$current.NativeWindowHandle,$class,$class.Capacity) | Out-Null
        $result += [pscustomobject]@{name=$current.Name;class=$class.ToString();type=$current.ControlType.ProgrammaticName;enabled=$current.IsEnabled;handle=$current.NativeWindowHandle;bounds=$current.BoundingRectangle.ToString();value=$value}
    }
    return $result
}

function Save-LiveEvidence($Window, [string]$Name) {
    $handle = [IntPtr]$Window.Current.NativeWindowHandle
    Focus-LiveWindow $Window
    if ([LiveUiNative]::GetForegroundWindow() -ne $handle) { throw 'Application window could not be foregrounded for a real screen capture.' }
    $bounds = $Window.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width,[int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size)
        $bitmap.Save((Join-Path $script:LiveEvidence ($Name+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    [ordered]@{capturedAt=(Get-Date).ToString('o');title=$Window.Current.Name;controls=@(Get-LiveSnapshot $Window)} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:LiveEvidence ($Name+'.json'))
    Write-Output "Captured real window: $Name"
}
