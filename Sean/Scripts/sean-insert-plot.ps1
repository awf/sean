<# 
.SYNOPSIS
Insert a plot of the data. Expects an array of PSObjects with two properties.

.DESCRIPTION
$big5 = dir | Sort-Object Length -descending | select -first 5 | select Name, Length 
$foo = sean-insert-plot.ps1 $big5

$foo = $big5 | sean-insert-plot.ps1 


#>
param($table, [object]$DataContext = $null)

$wpfhost = $Host.PrivateData.UI -as [Sean.MyPSHostUserInterface]
if (!$wpfhost) {
  throw "Not a Sean shell"
}

# Build the API URL to get the plot
$theproperties = $table[0].psobject.properties | Foreach { $_.Name }
$thelabels=$()
if ($theproperties.Length -le 1) {
	$thevalues = $table | ForEach { $_.($theproperties[0]) }
} else {
    $thelabels = $table | ForEach { $_.($theproperties[0]) }
	$thevalues = $table | ForEach { $_.($theproperties[1]) }
}

$prettyval = $thevalues -join ','
$prettylabels = $thelabels -join '|'

$ploturl="https://chart.googleapis.com/chart?chs=500x100&chd=t:${prettyval}&cht=p"
if ($thelabels.Length -gt 0) {
  $ploturl += "&chl=${prettylabels}"
}

# XAML doesn't seem to like the "=" character in URLs (?)
$ploturl = $ploturl -replace '&','&amp;'

# wrap with border mainly to get the namespaces in place
$wrapxaml = @"
<Border 
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
$xaml
<Image Stretch="None" Source="$ploturl"/>
</Border>
"@;

$wpfhost.SeanInsertXaml($wrapxaml, $DataContext);
