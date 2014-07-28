
# Take a list of tokens from PowerSure.Parser and convert to an array of strings for debug output
param($tokens)

[string]::join(", ", ($tokens | % { "" + $_.Type + "[" + $_.Content + "]" }))
