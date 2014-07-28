param($tokens)
# Take a list of tokens from PowerSure.Parse, and reassemble into a string

if ($tokens -eq 'test') {
  # test
  function test($expr, $expected) {
    if (!$expected) {
      $expected = $expr;
    }
    $t = [PowerSure.Parser]::Parse($expr)
    $back = sure-parse-to-expr $t
    awf-test-assert "$back" $expected
  }
  write-host "Testing"
  test 'dir fred*'
  test '1 + 2'
  test '(1 + 2).a' '(1 + 2) . a'
  test '(1+2*4).a' '(1 + 2 * 4) . a'
  return
}
 
$tokens | % {
  $token = $_
  $c = $token.Content;
  switch ($token.Type) {
    Type {
      "[" + $c + "]"
    }
    Variable {
      '$' + $c
    }
    CommandArgument {
      $c
    }
    String {
      '"' + $c + '"'
    }
    Group {
      $kids = $token.Children
      "(" + (sure-parse-to-expr $kids) + ")"
    }
    default {
      $c
    }
  }
}
