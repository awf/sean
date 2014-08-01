<# 
.SYNOPSIS
Insert an image as an output block

.DESCRIPTION
$foo = sean-insert-image http://a5.mzstatic.com/us/r30/Purple4/v4/c3/57/d5/c357d588-848b-474c-ed1b-4a636b2c33e9/icon_256.png


#>
param([string]$imgurl, [object]$DataContext = $null)

$wpfhost = $Host.PrivateData.UI -as [Sean.MyPSHostUserInterface]
if (!$wpfhost) {
  throw "Not a Sean shell"
}

# wrap with border mainly to get the namespaces in place
$wrapxaml = @"
<Border 
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
$xaml
<Image Source="$imgurl"/>
</Border>
"@;

$wpfhost.SeanInsertXaml($wrapxaml, $DataContext);
