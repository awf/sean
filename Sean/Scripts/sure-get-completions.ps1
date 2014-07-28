param([string]$line, $tokens = $null, [switch]$verbose = $false)

if ($PSVersionTable.PSVersion.Major -ge 3) {
  sean-get-completions-3 $line $tokens $verbose
} else {
  sean-get-completions-2 $line $tokens $verbose
}
