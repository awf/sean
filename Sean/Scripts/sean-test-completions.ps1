param($test)

if (!$test) {
	sean-test-completions "echo 'c:\program "
	sean-test-completions 'echo "c:\program '
	sean-test-completions "echo c:\program`` "
	return
}

if (0) {
$te = TabExpansion2 $test ($test.length)
$te | ft -au
$te | select -exp CompletionMatches
} else {
	write-host -fore cyan "Testing [$test]`n"
	$t0 = get-date
	$global:sean_tokens = [PowerSean.Parser]::Parse($test);
	$out = sean-get-completions-3 $test $sean_tokens -verbose
	$t = get-date
	write-host ("Elapsed {0:#.##} msec" -f (($t - $t0).TotalSeconds * 1000))
	$out | out-string | write-host 

}
