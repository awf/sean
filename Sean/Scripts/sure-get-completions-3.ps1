param([string]$line, $tokens = $null, [switch]$verbose = $false)

if ($PSVersionTable.PSVersion.Major -lt 3) {
  throw "Version 3 only"
}

if ($verbose) {
  sure-write-debug "***"
  sure-write-debug "sure-get-completions3: line [$line]"
}

if ($line.EndsWith("``")) {
  $line = $line.Substring(0, $line.length-1)
  if ($verbose) {
    sure-write-debug "sure-get-completions3: stripped backtick [$line]"
  }
}
#	write-host ("1Elapsed {0:#.##} msec" -f (((get-date) - $t0).TotalSeconds * 1000))

# PS V3 won't complete a bare command with backticks
$wasbarecommand = $tokens.count -eq 1 -and $tokens[0].type -eq 'Command' -and $line -notmatch '^ *&'  -and $line -match '`'
if ($wasbarecommand) {
	if ($verbose) { sure-write-debug "Translating bare command for PSV3 bug from [$line]" }
	$line = $line -replace '`(.)','$1' -replace "'","''"
	$line = "& '$line'"
	if ($verbose) { sure-write-debug "Translating bare command for PSV3 bug to [$line]" }
}

#	write-host ("2Elapsed {0:#.##} msec" -f (((get-date) - $t0).TotalSeconds * 1000))
write-progress "Tab Completion" -perc 20
$options = $null
if ($line -ne '') {
$expansions = [System.Management.Automation.CommandCompletion]::CompleteInput(
    <#inputScript#>  $line,
    <#cursorColumn#> $line.Length,
    <#options#>      $options)
} else {
	$expansions = @()
}

write-progress "Tab Completion" -perc 80
#	write-host ("3Elapsed {0:#.##} msec" -f (((get-date) - $t0).TotalSeconds * 1000))

$global:sure_expansions = $expansions

$n = $expansions.CompletionMatches.count

if ($verbose) {
  sure-write-debug "Got $n expansions"
}

if (!$n) {
  @($line)

  write-progress "Tab Completion" -perc 100
  return

  # xxfixme: calling v2, need to trip backticks from last token if present
  sure-write-debug "Calling sure-get-completions-2"
  $tokens[-1].Content = $tokens[-1].Content -replace "``$",''
  return sure-get-completions-2 $line $tokens $verbose
}

$names = $expansions | % { $start = -1 } {
  if ($start -eq -1) {
    $start = $_.ReplacementIndex
  } else {
    if ($_.ReplacementIndex -ne $start) {
      sure-write-debug "EEEK: Resetting start"
      $start = [math]::min($_.ReplacementIndex, $start)
      sure-write-debug "EEEK: Resetting start -> $start"
    }
  }

  # result types:
  # Text, History, Command, ProviderItem, ProviderContainer, Property, 
  # Method, ParameterName, ParameterValue, Variable, Namespace, Type

  $completion_intro_char = $line.Substring($_.ReplacementIndex,1);
  $already_quoted = $completion_intro_char -eq "'" -or $completion_intro_char -eq '"'
  if ($already_quoted -and !$wasbarecommand) {
	# Just take the powershell list.  Complain if we see anything unquoted
	$quoth_matches = $_.CompletionMatches | % { 
	    $text = $_.CompletionText;
		$quoth = if (!($text.StartsWith("'") -or $text.StartsWith('"'))) {
			sure-write-debug "EEK: not quoted [$text]"
			"'$text'"
		} else {
			$text
		}
        if ($_.ResultType -eq [System.Management.Automation.CompletionResultType]::ProviderContainer) {
          $quoth + "\"
        } else {    
          $quoth
        }
    }
  } else {
    $quoth_matches = $_.CompletionMatches | % { 
	    $text = $_.CompletionText;
		if ($text.StartsWith("'")) {
		  $text = $text.Substring(1, $text.length-2);
		} 
		elseif ($text.StartsWith("& '")) {
		  $text = $text.Substring(3, $text.length-4);
		}
        $quoth = if ($_.ResultType -eq [System.Management.Automation.CompletionResultType]::ProviderContainer) {
          $text + "\"
        } else {    
          $text
        }
		if ($quoth -match "``") {
			sure-write-debug "WARNING: replacement contains  backtick -- is it quoting anything?"
		}
		$quoth -replace "('')","``'" -replace '([^''A-Za-z_0-9\\/.:${}`-])','`$1'
    }
  }
  $quoth_matches
}

if ($start -ge 0) {
  if ($wasbarecommand) {
	$start = 0
  }
  $restofline = $line.Substring(0, $start);
} else {
  sure-write-debug "start =0"
  $restofline = ''
}
@($restofline) + @($names)

write-progress "Tab Completion" -perc 100
exit

#############
#############
#############
#############
#############
#############
#############
#############
#############
#############
#############
#############
#############
#############
#############

if (!$tokens) { 
  sure-write-debug "NOTE: Parsing line, normally I expect the caller to do this"
  $tokens = [PowerSure.Parser]::Parse($line)

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
$global:suretokens = $tokens
$n = $tokens.count
if ($verbose) {
  sure-write-debug "sure-get-completions: PARSE got $n tokens: $(sure-parse-print $tokens)"
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
   sure-write-debug "sure-get-completions: OFF THE END lastcol=$lastcol LL = $($line.Length)"
   $lasttok = new-object PowerSure.Token
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
    sure-write-debug "sure-get-completions: NO TOKENS, NONEMPTY LINE [$line]"
    return
  }
  $lasttok = new-object PowerSure.Token
  $lasttok.Type = 'Command'
  $lasttok.Content = ''
  $lasttok.Start = 0
  $lasttok.Length = 0
  $n = 1
  $tokens = @($lasttok)
}

if ($verbose) {
  sure-write-debug "sure-get-completions: mutated to $n tokens: $(sure-parse-print $tokens)"
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
    sure-write-debug "sure-get-completions: Filename expansion, lastword [$lastword]"
  }

  $file_names = @(sure-dir-to-strings "${lastword}*")
  # If it was a string, return it as a double-quoted string
  if ($lasttype -eq 'String') {
     $file_names = $file_names | % { '"' + ($_ -replace '`([^"])','$1') }
	 #sure-write-debug "Back to string [$($file_names[0])]"
  }

  $names += @($file_names)
  if ($verbose) {
    sure-write-debug "sure-get-completions: Got $($file_names.count) files; first 4: |$($file_names[0..4] -join '|')|"
    sure-write-debug "files[$($file_names.count)]"
  }
  #sure-write-debug ("dirtime = " + (toc))
}

# 1b. Command expansion
if (($lasttype -eq 'Command' -or
    ($lasttype -eq 'String' -and $n -eq 1)) -and
   !($lastword -match '[/\\]') ) {
  if ($verbose) {
    sure-write-debug "sure-get-completions: Command expansion, lastword [$lastword]"
  }
  $command_names = @(get-command -name "${lastword}*" | % { if (!($_.name -match '\.dll$')) {  $_.name } })
  $names += $command_names
  ##sure-write-debug "commands[$($command_names.count)]: $command_names"
  # sure-write-debug "cmds[$($names.count)]: ¦$($names[0..4] -join '¦')¦"
}

# 1c. Get variables 
if ($lasttype -eq 'Variable') {
  if ($verbose) {
    sure-write-debug "sure-get-completions: Variable expansion, lastword [$lastword]"
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
    sure-write-debug "variables: count=$($names.count) names=[$names]"
    sure-write-debug "vars[$($names.count)]: ¦$($names -join '¦')¦"
  }
}

# 1d. Member expansions: $Host.<TAB>
# EXPR.Metho[TAB]
if ($lasttype -eq 'Member' -and ($n -gt 2)) {
  $exprtok = $tokens[($n-3)]
  $optok = $tokens[($n-2)]
  if ($exprtok.Type -eq 'Type') {
    $t = $exprtok.Content
    sure-write-debug "Type EXPR [$t]"
    $type = invoke-expression "[$t]"
    $mems = $type.GetMembers() | select -expand Name | sort | get-unique
    $restofline = $line.Substring(0, $tokens[$n-3].Start);
    $names += @($mems | % { if ($_.StartsWith($lastword)) { "[$t]::" + $_ }})
    
  } else {
    # Evaluate it...
    $expr = [string](sure-parse-to-expr $exprtok);
    $sep = [string](sure-parse-to-expr $optok);
    $var = $lastword;
    $restofline = $line.Substring(0, $tokens[$n-3].Start);
    $value = invoke-expression $expr
    sure-write-debug "Member EXPR [$expr] -> $value [$($value.GetType())]"
    $names += @(get-member -input $value -name "$var*" | 
                   % { "$expr$sep" + $_.name })
    #sure-write-debug "EXPR: names -> [$names]"
    #sure-write-debug "membs[$($names.count)]: ¦$($names -join '¦')¦"
  }
}

#sure-write-debug "names[$($names.count)]: ¦$($names[0..10] -join '¦')¦"
# awf-new @{names=$names; linestart=$restofline}
@($restofline) + @($names)

sure-write-debug ("time = " + (toc))
