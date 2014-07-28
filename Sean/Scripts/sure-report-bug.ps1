<#
.SYNOPSIS
Open a page to report a bug

.DESCRIPTION
sean-report-bug [-title] "Bug title" [[-repro] "Repro steps"] [[-description] "Description"]

.PARAMETER title
This is required.  

.PARAMETER repro
Steps to reproduce the bug.

.PARAMETER description
General description of the bug: what should be seen after repro in the bug and no-bug cases

#>
param([string]$title = $(throw "need bug title"), [string]$repro = '', [string]$description = '')

# $title = 'TabCompletion%3A%20%24Env%3Ap%5BTAB%5D%20does%20nothing';
# $repro = @'
#
# $Env:P[TAB]
#
# Nothing happens...
#
# '@;

$title = $title -replace ';','%3B'
$description = $description -replace ';','%3B'
$repro = $repro -replace ';','%3B'

tfpt workitem /collection:http://vstfcodebox:8080/tfs/upsilon /new SeanShell\Bug `
 /fields:"Title=$title;Description=$description"

## old code:
#$base = 'http://co1vmvstfat45:8090/wi.aspx?pname=SeanShell&wit=Bug';
#$url = $base;

#$url += '&[System.Title]=' + $title
#$url +=  &[Microsoft.VSTS.Common.ReproInformation]=' + $repro
#$url +=  &[System.Description]=' + $description

