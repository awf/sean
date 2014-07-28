param($act)
# A collection of routines to interact with Sure
# Examples:
#    sure do "Message"   # adds message to TODO.txt
#    sure copy           # copy installation to ~/bin

function go($cmd)
{
  write-host $cmd
  & $cmd
}

$suredir = "c:\dev\SureShell\Sure"
$todo = "$suredir/Documents/TODO.txt"

switch ($act) {
  "newbug" {
	throw "use sure-report-bug to report bugs"
  }
  "do" {
    $msg = "$args"
    go {
      write-host "Append [$msg]" 
      echo "$msg" | out-file -enc ascii -append $todo
    }
    cat $todo | select -last 10
  }
  "copy" {
    go { copy Sure.exe,AuDotNet.dll,PowerSure.dll,"$suredir/Scripts/*.ps1" ~/bin }
  }
}
