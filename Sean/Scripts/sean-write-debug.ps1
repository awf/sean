<#
.SYNOPSIS
Write debug messages to Sean's debug window

.DESCRIPTION
Write them.

.PARAMETER clear
Clear the debug window before printing

.PARAMETER c
Do not add a newline after printing

.PARAMETER msg
The string to be printed
#>

param([switch]$clear = $false, 
      [switch]$c = $false,
      [switch]$dont = $false,
      [switch]$reset = $false, 
      [switch]$on = $false, 
      [switch]$off = $false, 
      [string]$msg)

if ($on) {
  $global:awfdebug_silent = 0;
}

if ($off) {
  $global:awfdebug_silent = 1;
}

if ($awfdebug_silent) {
  return
}

# Check for various wpf hosts
$wpfhost = $null
$hosttype = $Host.PrivateData.GetType().ToString();
if ($hosttype -eq "wpfhost.MyHost") {
  $wpfhost = $Host.PrivateData -as [wpfhost.MyHost];
}
if ($hosttype -eq "Sean.MyPSHost") {
  $wpfhost = $Host.PrivateData.UI -as [Sean.MyPSHostUserInterface]
}
if ($wpfhost) {
  if (!$c) { $msg += "`n"; }
  if (!$dont) { $wpfhost.SeanWriteToDebugWindow($msg); }
  return
}

throw "Not in a SeanShell"
