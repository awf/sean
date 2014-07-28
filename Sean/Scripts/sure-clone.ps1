<#
.SYNOPSIS
Clone a PSObject.

.DESCRIPTION
Generate a clone of a PSObject.  PowerShell normally copies by reference.  For example
given object $a, with fields Name,Address the sequence
   $b = $a
   $b.Name = 'fred'
will overwrite $a.Name.  In contrast,
   $b = sure-clone $a
   $b.Name = 'fred'
will not modify $a.Name   

.PARAMETER obj
The object to clone

.PARAMETER fields
The object fields to clone

.PARAMETER test
Run unit tests on sure-clone, and ignore other options
#>
param([object]$obj, [string[]]$fields = $null, [switch]$test)

if ($test) {
	$a = [datetime]::Now
	$b = sure-clone $a
	$b.Millisecond = $a.Millisecond + 1
	if ($b.Millisecond -eq $a.Millisecond) {
		throw "clone failed"
	}
	write-host "Test1 passed: millisecs not equal"

	$c = sure-clone $b Hour,Minute
	$members = $c | get-member -membertype Properties | select -expand Name
	if ("$members" -ne "Hour Minute") {
		throw "clone failed"
	}
	write-host "Test2 passed: members = $members"

	return
}

if (!$obj) {
	throw "Object required"
}

if (!$fields) {
	$fields = get-member -input $obj -type properties | select -expand Name
}

# Create an empty object
$out = new-object PSObject

# and add fields
foreach ($field in $fields) { 
  add-member -i $out NoteProperty $field $obj.($field)
}

$out
