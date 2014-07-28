param([string]$line, $tokens = $null, [switch]$verbose = $false)

if ($PSVersionTable.PSVersion.Major -ge 3) {
  sure-get-completions-3 $line $tokens $verbose
} else {
  sure-get-completions-2 $line $tokens $verbose
}
