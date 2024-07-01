using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shap.Flexer.Examples
{
    using CharClass = StringFlexer<IniTokenizer.TokenizerStates>.CharClass;
    using Tokenizer = StringFlexer<IniTokenizer.TokenizerStates>;
    using Token = StringFlexer<IniTokenizer.TokenizerStates>.Token;
    using Parser = CallbackFlexer<
        IniParser.ParserStates,
        StringFlexer<IniTokenizer.TokenizerStates>.Token,
        TokenTypeComparer<IniTokenizer.TokenizerStates>,
        IniParser.ParserContext>;
    using static Shap.Flexer.Examples.IniTokenizer;

    public class IniTokenizer
    {
        public enum TokenizerStates
        {
            Free,
            Comment,
            Section,
            ParamName,
            SpaceAfterParamName,
            SpaceAfterEqualsSign,
            ParamValue
        }

        readonly Tokenizer tokenizer = new();

        public IniTokenizer()
        {
            tokenizer.Register(TokenizerStates.Free)
                .When(CharClass.LatinLetter).ChangeState(TokenizerStates.ParamName)
                .When('[').Skip().ChangeState(TokenizerStates.Section)
                .When(CharClass.Whitespace, '\n').Skip()
                .When(';', '#').ChangeState(TokenizerStates.Comment)
                .Otherwise().Error("Expected a param name (starts with latin letter)")
                .WhenEofDropState();

            tokenizer.Register(TokenizerStates.Comment)
                .When('\n').ChangeState(TokenizerStates.Free)
                .Otherwise().Skip()
                .WhenEofDropState();

            tokenizer.Register(TokenizerStates.Section)
                .When(CharClass.LatinLetter).Consume()
                .When(']').NextState(TokenizerStates.Free).Skip()
                .Otherwise().Error("Forbidden symbol in section name")
                .WhenEofError();

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
        }

        public List<Token> Tokenize(IEnumerable<char> input)
        {
            return tokenizer.FlexAll(input);
        }
    }

    public class IniParser
    {
        public enum ParserStates
        {
            ExpectParamNameOrSection,
            ExpectParamValue
        }

        public class ParserContext
        {
            public Dictionary<string, Dictionary<string, string>> parsed = [];
            public Dictionary<string, string> curSection = [];
            public string curParamName = "";

            public ParserContext()
            {
                parsed.Add("__global", curSection);
            }
        }

        readonly Parser parser = new();

        public IniParser()
        {
            parser.Register(ParserStates.ExpectParamNameOrSection)
                .When(new(TokenizerStates.Section)).Do((ParserStates curState, IEnumerator<Token> input, ParserContext ctx) =>
                {
                    Token token = input.Current;
                    if (ctx.parsed.ContainsKey(token.str))
                        throw SyntaxException.FromToken(token, "Section is already handled");
                    ctx.curSection = [];
                    ctx.parsed.Add(token.str, ctx.curSection);
                    return curState;
                }).Next()
                .When(new(TokenizerStates.ParamName)).Do((ParserStates curState, IEnumerator<Token> input, ParserContext ctx) =>
                {
                    Token token = input.Current;
                    if (ctx.curSection.ContainsKey(token.str))
                        throw SyntaxException.FromToken(token, "Specified key is already present in section");
                    if (ctx.curParamName != "")
                        throw SyntaxException.FromToken(token, "Unexpected param name");
                    ctx.curParamName = token.str;
                    return ParserStates.ExpectParamValue;
                }).Next()
                .Otherwise().Error("Unexpected token")
                .WhenEndOk();

            parser.Register(ParserStates.ExpectParamValue)
                .When(new(TokenizerStates.ParamValue)).Do((ParserStates curState, IEnumerator<Token> input, ParserContext ctx) =>
                {
                    Token token = input.Current;
                    if (ctx.curParamName == "")
                        throw SyntaxException.FromToken(token, "Missing param name");
                    ctx.curSection.Add(ctx.curParamName, token.str);
                    ctx.curParamName = "";
                    return ParserStates.ExpectParamNameOrSection;
                }).Next()
                .Otherwise().Error("Unexpected token")
                .WhenEndError();
        }

        public Dictionary<string, Dictionary<string, string>> Parse(IEnumerable<Token> input)
        {
            ParserContext ctx = new();
            parser.FlexAll(input, ctx);
            return ctx.parsed;
        }

        public Dictionary<string, Dictionary<string, string>> Parse(IEnumerable<char> input)
        {
            return Parse(new IniTokenizer().Tokenize(input));
        }
    }
}
