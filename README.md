# Shap.Flexer
Finite state machine based lexer. It is a simple programmable sequence processor, that takes different actions based on its current state and current sequence item.

## Quick start
Following code utilizes `StringFlexer` in order to split given string into words

```csharp
using Shap.Flexer;

namespace SampleStringSplitter
{
    // Type alias for easier time using Character Classes.
    using CharClass = StringFlexer<Program.LexerStates>.CharClass;

    internal class Program
    {
        // These are states, that Flexer can use. Some of them are tokens.
        public enum LexerStates
        {
            Start,  // Initial state, used to skip spaces in
                    // the start of the document.
            Word,   // Word state, which acts also as a token.
            Space   // Space state.
        }

        static void Main(string[] _)
        {
            StringFlexer<LexerStates> lexer = new();

            // Registering states takes approach of
            // writing these script-like instructions.
            lexer.Register(LexerStates.Start)
                .When(CharClass.Whitespace).Skip()
                // Skip spaces at the start.
                .Otherwise().ChangeState(LexerStates.Word)
                // Any non-space symbol indicates start of a word
                .WhenEofDropState();
                // If document is empty (from our perspective),
                // we don't need to get any tokens from it

            lexer.Register(LexerStates.Word)
                .When(CharClass.Whitespace).NextState(LexerStates.Space)
                // When encounter whitespace, yield the current word token
                // and change state to "Space"
                .Otherwise().Consume()
                // Any non-whitespace character is part of a word and must
                // be consumed in token
                .WhenEofYieldState();
                // Handle case when last word ends where ends the document

            lexer.Register(LexerStates.Space)
                .When(CharClass.Whitespace).Skip()
                // There is no reason to keep whitespaces in result
                .Otherwise().ChangeState(LexerStates.Word)
                // If we encounter anything other than whitespace, then should start word
                .WhenEofDropState();
                // No need to remember the last state

            foreach (var token in lexer.Flex("Lorem Ipsumn      is\n\t\rsimply" +
                " \t\n     dummy\r\n\ttext."))
            {
                Console.WriteLine($"Line {token.endPos.line} pos {token.endPos.pos}: " +
                    $"{token.type}({token.str})");
            }
        }
    }
}

```
## Proper example: `.ini` parser
Following code utilizes most of library potential. It parses given `.ini` file string into dictionary (simplified version, for comments and section support see More examples below)
<details>

<summary>code is quite large</summary>

```csharp
using Shap.Flexer;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace SampleIniParser
{
    using CharClass = StringFlexer<Program.TokenizerStates>.CharClass;
    using Tokenizer = StringFlexer<Program.TokenizerStates>;
    using Token = StringFlexer<Program.TokenizerStates>.Token;
    using Parser = CallbackFlexer<
        Program.ParserStates,
        StringFlexer<Program.TokenizerStates>.Token,
        TokenTypeComparer<Program.TokenizerStates>,
        Program.ParserContext>;

    internal class Program
    {
        public enum TokenizerStates
        {
            Free,
            ParamName,
            SpaceAfterParamName,
            SpaceAfterEqualsSign,
            ParamValue
        }

        public enum ParserStates
        {
            ExpectParamName,
            ExpectParamValue
        }

        public class ParserContext
        {
            public Dictionary<string, string> parsed = [];
            public string curParamName = "";
        }

        static void Main(string[] args)
        {
            string iniText = @"
name = John Doe
organization = Acme Widgets Inc.

server = 192.0.2.62
port = 143
file = ""payroll.dat""".Replace("\r", "");

            //flexer setup
            Tokenizer tokenizer = new();

            tokenizer.Register(TokenizerStates.Free)
                .When(CharClass.LatinLetter).ChangeState(TokenizerStates.ParamName)
                .When(CharClass.Whitespace, '\n').Skip()
                .Otherwise().Error("Expected a param name (starts with latin letter)")
                .WhenEofDropState();

            tokenizer.Register(TokenizerStates.ParamName)
                .When(CharClass.LatinLetter, CharClass.Number, '_', '-').Consume()
                .When('=').Skip().NextState(TokenizerStates.SpaceAfterEqualsSign)
                .When(CharClass.Whitespace).NextState(TokenizerStates.SpaceAfterParamName)
                .Otherwise().Error("Forbidden symbol in param name")
                .WhenEofError();

            tokenizer.Register(TokenizerStates.SpaceAfterParamName)
                .When(CharClass.Whitespace).Skip()
                .When('=').Skip().ChangeState(TokenizerStates.SpaceAfterEqualsSign)
                .Otherwise().Error("Expected '=' sign after param name")
                .WhenEofError();

            tokenizer.Register(TokenizerStates.SpaceAfterEqualsSign)
                .When(CharClass.Whitespace).Skip()
                .When('=').Error("Another equals sign is bad")
                .Otherwise().ChangeState(TokenizerStates.ParamValue)
                .WhenEofError();

            tokenizer.Register(TokenizerStates.ParamValue)
                .When('\n').NextState(TokenizerStates.Free)
                .Otherwise().Consume()
                .WhenEofYieldState();

            Parser parser = new();

            parser.Register(ParserStates.ExpectParamName)
                .When(new(TokenizerStates.ParamName)).Do((ParserStates curState, IEnumerator<Token> input, ParserContext ctx) =>
                {
                    Token token = input.Current;
                    ctx.curParamName = token.str;
                    return ParserStates.ExpectParamValue;
                }).Next()
                .Otherwise().Error("Unexpected token")
                .WhenEndOk();

            parser.Register(ParserStates.ExpectParamValue)
                .When(new(TokenizerStates.ParamValue)).Do((ParserStates curState, IEnumerator<Token> input, ParserContext ctx) =>
                {
                    Token token = input.Current;
                    ctx.parsed.Add(ctx.curParamName, token.str);
                    ctx.curParamName = "";
                    return ParserStates.ExpectParamName;
                }).Next()
                .Otherwise().Error("Unexpected token")
                .WhenEndOk();

            List<Token> tokens = tokenizer.FlexAll(iniText);
            ParserContext ctx = new();
            parser.FlexAll(tokens, ctx);

            string serialized = JsonSerializer.Serialize(ctx.parsed, options: new() { WriteIndented = true });
            Console.WriteLine(serialized);
        }
    }
}

```
</details>

## More examples
From `Examples/` folder:
* Proper `.ini` parser: TODO link
* Xml-like languages parser (with funky options): TODO link