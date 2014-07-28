using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Management.Automation;

namespace PowerSean
{
    /// <summary>
    /// Standard PS token types plus a "Group"
    /// </summary>
    public enum TokenType
    {
        Unknown = 0,
        Command = 1,
        CommandParameter = 2,
        CommandArgument = 3,
        Number = 4,
        String = 5,
        Variable = 6,
        Member = 7,
        LoopLabel = 8,
        Attribute = 9,
        Type = 10,
        Operator = 11,
        GroupStart = 12,
        GroupEnd = 13,
        Keyword = 14,
        Comment = 15,
        StatementSeparator = 16,
        NewLine = 17,
        LineContinuation = 18,
        Position = 19,
        Group = 20
    }

    public class Token
    {
        public TokenType Type;
        public string Content;
        public Token[] Children;

        public int EndColumn;
        public int EndLine;
        public int Length;

        /// <summary>
        /// Offset in the string
        /// </summary>
        public int Start;

        /// <summary>
        /// Offset in the line
        /// </summary>
        public int StartColumn;

        /// <summary>
        /// Start line within the string
        /// </summary>
        public int StartLine;
        
        /// <summary>
        /// Indicates that this token was incomplete in the source, for example
        ///   [doub
        ///   "A bit of a strin
        ///   ${varia
        /// </summary>
        public bool IsPartial;
    }

    public class Parser
    {
        static public PSToken[] GetTokens(string line)
        {
            // see "horrible" below.
            if (line.IndexOf('ø') != -1)
                throw new NotSupportedException("It bit me");

            string line_to_parse = line;
            bool need_parse = true;
            Collection<PSToken> toks = new Collection<PSToken>();
            while (need_parse)
            {
                Collection<PSParseError> parse_errors = new Collection<PSParseError>();
                toks = PSParser.Tokenize(line_to_parse, out parse_errors);
                need_parse = false;
                string old_l2p = line_to_parse;
                //awf-debug "psh-parse: TOKS=[$toks], line [$line_to_parse]"
                //awf-debug "psh-parse: ERRS=[$($parse_errors | select -exp message)]"

                // Check for fixable errors, and re-parse
                // Yes, this is horrible, but apparently there's a great new parser coming out,
                // so rewriting it would be wasteful...
                // For super horrendousness, the faked additions are marked with an ø
                // because it looks like a null set, counts as an identifier, and because 
                // I randomly assert that programmers who use ø in their native languages 
                // normally code in English...
                foreach (PSParseError err in parse_errors)
                {
                    var matches = Regex.Matches(err.Message, "is missing the terminator: (.)");
                    if (matches.Count > 0)
                    {
                        string terminator = matches[0].Groups[1].Captures[0].Value;
                        line_to_parse += "ø" + terminator;
                        need_parse = true;
                        break;
                    }

                    matches = Regex.Matches(err.Message, "is missing the closing '(.)'");
                    if (matches.Count > 0)
                    {
                      string terminator = matches[0].Groups[1].Captures[0].Value;
                      line_to_parse += "ø" + terminator;
                      need_parse = true;
                      break;
                    }

                    matches = Regex.Matches(err.Message, "Missing closing '(.)' in statement block");
                    if (matches.Count > 0)
                    {
                      string terminator = matches[0].Groups[1].Captures[0].Value;
                      line_to_parse += "ø" + terminator;
                      need_parse = true;
                      break;
                    }

                    matches = Regex.Matches(err.Message, "Variable reference is not valid\\. ':' was not followed by");
                    if (matches.Count > 0)
                    {
                        line_to_parse += "ødummy";
                        need_parse = true;
                        break;
                    }

                    if (Regex.IsMatch(err.Message, "Missing \\] at end of "))
                    {
                        line_to_parse += "ø]";
                        need_parse = true;
                        break;
                    }

                    if (Regex.IsMatch(err.Message, "Missing property name after reference"))
                    {
                        line_to_parse = Regex.Replace(line_to_parse, "::$", "::ø");
                        line_to_parse = Regex.Replace(line_to_parse, "\\.$", ".ø");
                        need_parse = true;
                        break;
                    }

                    // unexplained error....
                    throw new Exception(err.Message);
                }
                if (need_parse && old_l2p == line_to_parse)
                    throw new Exception("Infinite loop in Parser");
            }

            return toks.ToArray();
        }

        struct ParseOut
        {
            public Token[] toks;
            public int nconsumed;
        };

        static public Token[] Parse(string line)
        {
            PSToken[] toks = GetTokens(line);
            return ParseAux(toks, "").toks;
        }

        static ParseOut ParseAux(PSToken[] pstoks, string indent)
        {
            ParseOut po = new ParseOut();

            List<Token> toks = new List<Token>();
            int n = pstoks.Length;
            for (int k = 0; k < n; ++k)
            {
                PSToken pstok = pstoks[k];
                switch (pstok.Type)
                {
                    case PSTokenType.GroupEnd:
                        {
                            po.toks = toks.ToArray();
                            po.nconsumed = k + 1;
                            return po;
                        }
                    case PSTokenType.GroupStart:
                        {
                            po = ParseAux(pstoks.Skip(k + 1).ToArray(), indent + ".");
                            k += po.nconsumed;
                            Token tok = new Token();
                            tok.Type = TokenType.Group;
                            tok.Children = po.toks;
                            tok.Start = pstok.Start;
                            tok.StartColumn = pstok.StartColumn;
                            tok.StartLine = pstok.StartLine;
                            tok.EndColumn = pstoks[k].EndColumn;
                            tok.EndLine = pstoks[k].EndLine;
                            tok.Length = 0;
                            foreach(Token t in po.toks)
                                tok.Length += t.Length;
                            toks.Add(tok);
                            break;
                        }
                    default:
                        {
                            Token tok = new Token();
                            tok.Type = (TokenType)pstok.Type;
                            tok.Content = pstok.Content;
                            tok.EndColumn = pstok.EndColumn;
                            tok.EndLine = pstok.EndLine;
                            tok.Length = pstok.Length;
                            tok.Start = pstok.Start;
                            tok.StartColumn = pstok.StartColumn;
                            tok.StartLine = pstok.StartLine;
    
                            int sentinel = tok.Content.IndexOf('ø');
                            if (sentinel == -1)
                            {
                                tok.IsPartial = false;
                            }
                            else
                            {
                                tok.IsPartial = true;
                                int delta = tok.Content.Length - sentinel;
                                tok.Content = tok.Content.Substring(0, sentinel);
                                tok.Length -= delta + 1; //Plus 1 for sentinel and terminator, assumes the added guff was at EOL
                                tok.EndColumn -= delta + 1; 
                            }

                            
                            toks.Add(tok);
                            break;
                        }

                }
            }
            po.toks = toks.ToArray();
            po.nconsumed = n;
            return po;

        }
    }
}
