<# 
.SYNOPSIS
Insert xaml as an output block

.DESCRIPTION
$button = sean-insert-xaml '<Button>B</Button>'
$button.Child.AddHandler([System.Windows.Controls.Button]::ClickEvent, [System.Windows.RoutedEventhandler]{ 1 })


#>
param([string]$xaml, [object]$DataContext = $null)

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
<Image Source="http://a5.mzstatic.com/us/r30/Purple4/v4/c3/57/d5/c357d588-848b-474c-ed1b-4a636b2c33e9/icon_256.png"/>
</Border>
"@;

$wpfhost.SeanInsertXaml($wrapxaml, $DataContext);
