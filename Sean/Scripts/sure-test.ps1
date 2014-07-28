
# Test progressbar
1..10 | % { 
	write-progress "sure test $p" -percent (($_-1)/10*100)
	$p = [math]::pow(3,$_)	
	sleep .8
	write-progress "sure test $p" -percent ($_/10*100)
}
