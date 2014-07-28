param($test)

if (!$test) {
	sure-test-completions "echo 'c:\program "
	sure-test-completions 'echo "c:\program '
	sure-test-completions "echo c:\program`` "
	return
}

if (0) {
$te = TabExpansion2 $test ($test.length)
$te | ft -au
$te | select -exp CompletionMatches
} else {
	write-host -fore cyan "Testing [$test]`n"
	$t0 = get-date
	$global:sure_tokens = [PowerSure.Parser]::Parse($test);
	$out = sure-get-completions-3 $test $sure_tokens -verbose
	$t = get-date
	write-host ("Elapsed {0:#.##} msec" -f (($t - $t0).TotalSeconds * 1000))
	$out | out-string | write-host 

}
