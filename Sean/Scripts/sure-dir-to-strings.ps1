#
#  sure-dir-to-strings 
#  
#  Expand glob $pattern and return as an array of strings,
#  replacing home dir with ~, current dir with ., etc.
#  Maintains a cache of providers for speed
#
param([string]$pattern)

$pattern = $pattern -replace '/','\'
if ($pattern -eq '~*') {
  return @('~\')
}

#$quoted_home = ($HOME -replace '([]\\$^{}(|)*+?[])','\$1');

# Set initial_fullname to the dir name of  everything up to the last "\",
# and then replace them at the end
if ($pattern -match '^(~?[.\\]*[\\])') 
{
  $initial = $matches[1] -replace '\\+$','';
  $initial_fullname = (get-item $initial).fullname
}
else
{
  $initial = '.';
  $initial_fullname = (get-item .).fullname
}

$initial_fullname = $initial_fullname -replace '\\+$',''

if ($verbose) {
  sure-write-debug "dir-to-strings [$pattern] init [$initial|$initial_fullname]"
}

if (!$global:sure_translations_src -or $sure_reset) {
  sure-write-debug "dir-to-strings: Getting translations"
  $global:sure_translations_src = @()
  $global:sure_translations_dst = @()
  get-psprovider | % { $_.Drives } | % {
    $src = $_.provider.ToString() + "::" + ($_.root -replace '\\$','');
    $dst = $_.name;
    $global:sure_translations_src += @($src);
    $global:sure_translations_dst += @($dst)
    sure-write-debug "dir-to-strings:   [$src] -->  [$dst]"
  }
}

dir -force $pattern | % {
  $fn = $_.PSPath

  # Quote specials
  $fn = ($fn -replace '([^a-zA-Z0-9_.:/\\-])','`$1');

  # Replace drive names
  if ($fn -match 'Microsoft.PowerShell.Core\\FileSystem::(.:.*)') {
    # special-case filesystem for speed.. much faster for me on 1/xi/2010..
    $fn = $matches[1]
  } else {
    for($i = 0; $i -lt $global:sure_translations_src.count; ++$i) {
      if ($fn.StartsWith($global:sure_translations_src[$i])) {
        $fn = $global:sure_translations_dst[$i] + ":" +
           $fn.Substring($global:sure_translations_src[$i].length);
        break
      }
    }
  }

  # Directories get a trailing \
  # this allows them (a) to be visually identified, and 
  # (b) one can rapidly descend a hierarchy without needing 
  # to enter the slashes
  if ($_.PSIsContainer) {
    $fn += "\"
  }

  # And put back however it was that the user referred to the prefix in the
  # first place (e.g. ~/f, ./f etc)
  if ($initial -and $fn.StartsWith($initial_fullname)) {
    $fn = $initial + $fn.Substring($initial_fullname.length);
  }
  $fn
}
