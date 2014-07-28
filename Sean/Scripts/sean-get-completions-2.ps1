param([string]$line, $tokens = $null, [switch]$verbose = $false)

if ($line -eq '?') {
	# Test mode.
	sean-write-debug "TEST"
	sean-test-completions | % { $_ -replace "[`n`r]","" }
	return
}

if ($verbose) {
  sean-write-debug "***"
  sean-write-debug "sean-get-completions2: line [$line]"
}
if (!$tokens) { 
  sean-write-debug "NOTE: Parsing line, normally I expect the caller to do this"
  $tokens = [PowerSean.Parser]::Parse($line)

  if ($PSVersionTable.PSVersion.Major -ge 3) {
    $tokens | % {
      if ($_.Type -eq [PowerSean.TokenType]::Variable) {
	$_.Content = [regex]::Replace($_.Content, '^\$', '');
      }
    }
  }

  ## V3 Parser	
  #$toks = [System.Management.Automation.Language.Token[]]@()
  #$errs = [System.Management.Automation.Language.ParseError[]]@()
  #$ans = [System.Management.Automation.Language.Parser]::ParseInput('$PSV', [ref]$toks, [ref]$errs)
}

$t0 = get-date
function toc
{
  $t = get-date
  ("Elapsed {0:#.##} msec" -f (($t - $t0).TotalSeconds * 1000))
}

# record them for debug
$global:seantokens = $tokens
$n = $tokens.count
if ($verbose) {
  sean-write-debug "sean-get-completions: PARSE got $n tokens: $(sean-parse-print $tokens)"
}

# Check if last token ends before the end of the line,
# i.e. this is the position for a new CommandArgument
# in which case we do file completion in the current directory 
# That is indicated by adding a new empty CommandArgument token
$lastcol = 0;
if ($n -gt 0) {
   $lastcol = $tokens[$n-1].Start + $tokens[$n-1].Length
}
if ($lastcol -lt $line.Length -and $line.EndsWith(' ')) {
   sean-write-debug "sean-get-completions: OFF THE END lastcol=$lastcol LL = $($line.Length)"
   $lasttok = new-object PowerSean.Token
   $lasttok.Type = 'CommandArgument'
   $lasttok.Content = ''
   $lasttok.Start = $line.Length
   $lasttok.Length = 0

   $tokens += @($lasttok)
   ++$n
}

## If $n = 0 and line is empty, make a Command with content ""
if ($n -le 0) {
  if ($line -ne '') {
    sean-write-debug "sean-get-completions: NO TOKENS, NONEMPTY LINE [$line]"
    return
  }
  $lasttok = new-object PowerSean.Token
  $lasttok.Type = 'Command'
  $lasttok.Content = ''
  $lasttok.Start = 0
  $lasttok.Length = 0
  $n = 1
  $tokens = @($lasttok)
}

if ($verbose) {
  sean-write-debug "sean-get-completions: mutated to $n tokens: $(sean-parse-print $tokens)"
}

## Set up lasttype, lastword etc
$lasttok = $tokens[$n-1]
$lastword = $lasttok.Content  
$lasttype = $lasttok.Type
$restofline = $line.Substring(0, $lasttok.Start);

########
## OK, now try for completions

$names = @()
# 1a. Filename expansion
if (($lasttype -eq 'CommandArgument') -or
    ($lasttype -eq 'Command' -and $lastword -match '[/\\]') -or
    ($lasttype -eq 'String')   ) {
  if ($verbose) {
    sean-write-debug "sean-get-completions: Filename expansion, lastword [$lastword]"
  }

  $file_names = @(sean-dir-to-strings "${lastword}*")
  # If it was a string, return it as a double-quoted string
  if ($lasttype -eq 'String') {
     $file_names = $file_names | % { '"' + ($_ -replace '`([^"])','$1') }
	 #sean-write-debug "Back to string [$($file_names[0])]"
  }

  $names += @($file_names)
  if ($verbose) {
    sean-write-debug "sean-get-completions: Got $($file_names.count) files; first 4: |$($file_names[0..4] -join '|')|"
    sean-write-debug "files[$($file_names.count)]"
  }
  #sean-write-debug ("dirtime = " + (toc))
}

# 1b. Command expansion
if (($lasttype -eq 'Command' -or
    ($lasttype -eq 'String' -and $n -eq 1)) -and
   !($lastword -match '[/\\]') ) {
  if ($verbose) {
    sean-write-debug "sean-get-completions: Command expansion, lastword [$lastword]"
  }
  $command_names = @(get-command -name "${lastword}*" | % { if (!($_.name -match '\.dll$')) {  $_.name } })
  $names += $command_names
  ##sean-write-debug "commands[$($command_names.count)]: $command_names"
  # sean-write-debug "cmds[$($names.count)]: ¦$($names[0..4] -join '¦')¦"
}

# 1c. Get variables 
if ($lasttype -eq 'Variable') {
  if ($verbose) {
    sean-write-debug "sean-get-completions: Variable expansion, lastword [$lastword]"
  }
  
  $var = $lastword;
  $names += @(get-variable -scope global -name "$var*" | % { '$' + $_.name })

  # if we see $f[TAB], add "$function:" to list of names
  foreach ($special in @('function', 'env')) {
    if ($special.StartsWith($var)) {
      $names += @('$' + $special + ':');
    }
  }

  # conversely, if we see $function:[TAB], add list of functions
  if ($lastword.StartsWith('function:', 'CurrentCultureIgnoreCase')) {
    $names += @(get-childitem "$lastword*" | % { '$function:' + $_.name })
  }
  if ($verbose) {
    sean-write-debug "variables: count=$($names.count) names=[$names]"
    sean-write-debug "vars[$($names.count)]: ¦$($names -join '¦')¦"
  }
}

# 1d. Member expansions: $Host.<TAB>
# EXPR.Metho[TAB]
if ($lasttype -eq 'Member' -and ($n -gt 2)) {
  $exprtok = $tokens[($n-3)]
  $optok = $tokens[($n-2)]
  if ($exprtok.Type -eq 'Type') {
    $t = $exprtok.Content
    sean-write-debug "Type EXPR [$t]"
    $type = invoke-expression "[$t]"
    $mems = $type.GetMembers() | select -expand Name | sort | get-unique
    $restofline = $line.Substring(0, $tokens[$n-3].Start);
    $names += @($mems | % { if ($_.StartsWith($lastword)) { "[$t]::" + $_ }})
    
  } else {
    # Evaluate it...
    $expr = [string](sean-parse-to-expr $exprtok);
    $sep = [string](sean-parse-to-expr $optok);
    $var = $lastword;
    $restofline = $line.Substring(0, $tokens[$n-3].Start);
    $value = invoke-expression $expr
    sean-write-debug "Member EXPR [$expr] -> $value [$($value.GetType())]"
    $names += @(get-member -input $value -name "$var*" | 
                   % { "$expr$sep" + $_.name })
    #sean-write-debug "EXPR: names -> [$names]"
    #sean-write-debug "membs[$($names.count)]: ¦$($names -join '¦')¦"
  }
}
#sean-write-debug "names[$($names.count)]: ¦$($names[0..10] -join '¦')¦"
# awf-new @{names=$names; linestart=$restofline}
@($restofline) + @($names)

sean-write-debug ("time = " + (toc))
