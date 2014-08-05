param($act)
# A collection of routines to interact with Sean
# Examples:
#    sean do "Message"   # adds message to TODO.txt
#    sean copy           # copy installation to ~/bin

function go($cmd)
{
  write-host $cmd
  & $cmd
}

$seandir = "c:\dev\SeanShell\Sean"
$todo = "$seandir/Documents/TODO.txt"

switch ($act) {
  "newbug" {
	throw "use sean-report-bug to report bugs"
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
    go { copy Sean.exe,AuDotNet.dll,PowerSean.dll,"$seandir/Scripts/*.ps1" ~/bin }
  }
}
