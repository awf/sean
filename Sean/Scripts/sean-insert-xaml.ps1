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
</Border>
"@;

$wpfhost.SeanInsertXaml($wrapxaml, $DataContext);
