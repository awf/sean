<# 
.SYNOPSIS
Insert a plot of the data. Expects an array of PSObjects with two properties.

.DESCRIPTION
$big5 = dir | Sort-Object Length -descending | select -first 3 | select Name, Length 
$foo = sean-insert-plot.ps1 $big5
or:
$foo = $big5 | sean-insert-plot.ps1 
(either works)

#>
param($table = $null
      , [object]$DataContext = $null
	  , [Parameter(ValueFromPipeline=$True)] $tablePipeline = $null)

# we're using the "advanced" form as a way to get to the data either from the pipeline, or from a variable.

Begin {
	$wpfhost = $Host.PrivateData.UI -as [Sean.MyPSHostUserInterface]
	if (!$wpfhost) {
	  throw "Not a Sean shell"
	}
	if ($null -eq $table) { $table = @() }
}

Process { 
	if ($tablePipeline -ne $null) {
		$table += $tablePipeline
	}
}

End {
	# Build the API URL to get the plot
	$props = @($table[0].psobject.properties)
	if ($props.Length -eq 0) {
		# we were passed an array instead of an object with properties. So we'll just draw that.
		$thevalues = $table
		$thelabels = $()
	} else {
		# we have an object with properties. Let's pick two and draw those.
		$theproperties = $props | Foreach { $_.Name }
		$thelabels=$()
		if ($theproperties.Length -le 1) {
			$thevalues = $table | ForEach { $_.($theproperties[0]) }
		} else {
			$thelabels = $table | ForEach { $_.($theproperties[0]) }
			$thevalues = $table | ForEach { $_.($theproperties[1]) }
		}
	}

	$prettyval = $thevalues -join ','
	$prettylabels = $thelabels -join '|'

	$ploturl="https://chart.googleapis.com/chart?chs=500x100&chd=t:${prettyval}&cht=p"
	if ($thelabels.Length -gt 0) {
	  $ploturl += "&chl=${prettylabels}"
	}

	# XAML doesn't seem to like the "=" character in URLs (?)
	$ploturl = $ploturl -replace '&','&amp;'

	sean-write-debug.ps1 "plot URL is $ploturl"

	# wrap with border mainly to get the namespaces in place
	$wrapxaml = @"
<Border 
	xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
	xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
<Image Stretch="None" Source="$ploturl"/>
</Border>
"@;


	#  <Image Stretch="None" Source="$ploturl"/>

	$wpfhost.SeanInsertXaml($wrapxaml, $DataContext);
}