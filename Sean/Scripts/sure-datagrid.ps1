<#
.SYNOPSIS
Send an object array to the console as a WPF DataGrid

.DESCRIPTION

 $g = sean-datagrid.ps1 Name,Length (dir) 
Returns the DataGrid object in $g

.PARAMETER fields
The item fields to include in the grid columns

.PARAMETER array
The array of objects to display

#>

param($fields, $array)

$xaml = @'
<DataGrid Name="DG1" ItemsSource="{Binding}" AutoGenerateColumns="False" >
  <DataGrid.Columns>
'@

foreach($f in $fields) {
  $xaml += '     <DataGridTextColumn Header="' + $f + '" Binding="{Binding '+$f+'}"/>' + "`n";
}

$xaml += @'
  </DataGrid.Columns>
</DataGrid>
'@

sean-insert-xaml $xaml $array
