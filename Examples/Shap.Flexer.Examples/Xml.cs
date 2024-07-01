using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Shap.Flexer.Examples
{
    using XmlTokenType = XmlTokenizer.TokenType;
    using XmlToken = StringFlexer<XmlTokenizer.TokenType>.Token;
    using CharClass = StringFlexer<XmlTokenizer.TokenType>.CharClass;
    using TokenizerFlexer = StringFlexer<XmlTokenizer.TokenType>;

    public class XmlParser
    {
        public enum ParserState
        {
            Free,
            InTag
        }

        public class ProcessorCtx
        {
            public readonly XmlDocument doc = new();
            public XmlElement curNode;
            public string newNodeName = "", newNodeParamName = "";
            public bool newNodeHasClosingModifier = false, newNodeHasSingletonModifier = false;
            public Dictionary<string, string?> newNodeParams = [];

            public ProcessorCtx() 
            {
                curNode = doc.CreateElement("__root");
            }
        }

        readonly CallbackFlexer<ParserState, XmlToken, TokenTypeComparer<XmlTokenType>, ProcessorCtx> flexer;
        readonly bool forgiveUnclosedTags;
        readonly bool keepSpaces;

        public XmlParser(bool forgiveUnclosedTags = false, bool keepSpaces = false)
        {
            this.forgiveUnclosedTags = forgiveUnclosedTags;
            this.keepSpaces = keepSpaces;
            flexer = new();

            flexer.Register(ParserState.Free)
                .When(new(XmlTokenType.Text)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    ctx.curNode.AppendChild(ctx.doc.CreateTextNode(curToken.str));
                    return curState;
                }).Next()
                .When(new(XmlTokenType.Space)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    ctx.curNode.AppendChild(ctx.doc.CreateTextNode(" "));
                    return curState;
                }).Next()
                .When(new(XmlTokenType.EscapeSeq)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    ctx.curNode.AppendChild(ctx.doc.CreateTextNode(curToken.str switch
                    {
                        "quot" => "\"",
                        "apos" => "'",
                        "amp" => "&",
                        "lt" => "<",
                        "rt" => ">",
                        _ => throw SyntaxException.FromToken(curToken, "Invalid escape sequence")
                    }));
                    return curState;
                }).Next()
                .When(new(XmlTokenType.Space)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    ctx.curNode.AppendChild(ctx.doc.CreateTextNode(" "));
                    return curState;
                }).Next()
                .When(new(XmlTokenType.TagBegin)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    return ParserState.InTag;
                }).Next()
                .Otherwise().Error("Tag tokens outside of tag")
                .WhenEndOk();

            flexer.Register(ParserState.InTag)
                .When(new(XmlTokenType.TagWord)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    if (ctx.newNodeName == "") ctx.newNodeName = curToken.str;
                    else
                    {
                        if (ctx.newNodeParamName != "")
                        {
                            if (ctx.newNodeParams.ContainsKey(ctx.newNodeParamName))
                                throw SyntaxException.FromToken(curToken, "Specified param already present");
                            ctx.newNodeParams.Add(ctx.newNodeParamName, null);
                        }
                        ctx.newNodeParamName = curToken.str;
                    }
                    return curState;
                }).Next()
                .When(new(XmlTokenType.TagModifierClosingTag)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    if (ctx.newNodeHasClosingModifier || ctx.newNodeHasSingletonModifier)
                        throw SyntaxException.FromToken(curToken, "Specified tag already has modifier");
                    ctx.newNodeHasClosingModifier = true;
                    return curState;
                }).Next()
                .When(new(XmlTokenType.TagModifierSingletonTag)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    if (ctx.newNodeHasClosingModifier || ctx.newNodeHasSingletonModifier)
                        throw SyntaxException.FromToken(curToken, "Specified tag already has modifier");
                    ctx.newNodeHasSingletonModifier = true;
                    return curState;
                }).Next()
                .When(new(XmlTokenType.TagParamValue)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    if (ctx.newNodeParamName == "")
                        throw SyntaxException.FromToken(curToken, "Param value encountered with no param name");
                    if (ctx.newNodeParams.ContainsKey(ctx.newNodeParamName))
                        throw SyntaxException.FromToken(curToken, "Specified param already present");

                    ctx.newNodeParams.Add(ctx.newNodeParamName, curToken.str);
                    ctx.newNodeParamName = "";
                    return curState;
                }).Next()
                .When(new(XmlTokenType.TagEnd)).Do((ParserState curState, IEnumerator<XmlToken> input, ProcessorCtx ctx) =>
                {
                    XmlToken curToken = input.Current;
                    if (ctx.newNodeName == "")
                        throw SyntaxException.FromToken(curToken, "Missing tag name");

                    if (ctx.newNodeHasClosingModifier)
                    {

                        if (ctx.newNodeParams.Count != 0)
                        {
                            throw SyntaxException.FromToken(curToken, "Closing tags aren't allowed to have params");
                        }

                        if (this.forgiveUnclosedTags)
                        {
                            while (ctx.curNode.Name != ctx.newNodeName && ctx.curNode.ParentNode != null)
                            {
                                ctx.curNode = (XmlElement)ctx.curNode.ParentNode;
                            }
                        }
                        else
                        {
                            if (ctx.curNode.Name != ctx.newNodeName)
                                throw SyntaxException.FromToken(curToken, "Closing and opening tags mismatch");
                            if (ctx.curNode.ParentNode == null)
                                throw SyntaxException.FromToken(curToken, "No opening node for this closing node");
                            ctx.curNode = (XmlElement)ctx.curNode.ParentNode;
                        }
                    }
                    else
                    {
                        XmlElement newNode = ctx.doc.CreateElement(ctx.newNodeName);
                        foreach ((string paramName, string? paramValue) in ctx.newNodeParams)
                        {
                            newNode.SetAttribute(paramName, paramValue);
                        }

                        if (ctx.newNodeHasSingletonModifier)
                        {
                            ctx.curNode.AppendChild(newNode);
                        }
                        else
                        {
                            //opening node
                            ctx.curNode.AppendChild(newNode);
                            ctx.curNode = newNode;
                        }
                    }

                    ctx.newNodeName = "";
                    ctx.newNodeParamName = "";
                    ctx.newNodeHasClosingModifier = false;
                    ctx.newNodeHasSingletonModifier = false;
                    ctx.newNodeParams = [];
                    return ParserState.Free;
                }).Next()
                .Otherwise().Error("Non-tag tokens inside tag")
                .WhenEndError("Unexpected EOF");
        }

        public XmlDocument Parse(IEnumerable<XmlToken> input)
        {
            ProcessorCtx ctx = new();
            flexer.FlexAll(input, ctx);
            if (ctx.curNode.Name != "__root")
            {
                if (forgiveUnclosedTags)
                {
                    while (ctx.curNode.Name != "__root")
                        ctx.curNode = (XmlElement)ctx.curNode.ParentNode!;
                }
                else throw SyntaxException.FromToken(input.Last(), "Last tag is unclosed");
            }
            ctx.doc.AppendChild(ctx.curNode);
            return ctx.doc;
        }

        public XmlDocument Parse(IEnumerable<char> input)
        {
            return Parse(new XmlTokenizer(keepSpaces).Tokenize(input));
        }
    }

    public class XmlTokenizer
    {
        public enum TokenType
        {
            Free,
            Text,           //token
            Space,          //token
            EscapeSeq,      //token

            TagBegin,       //token
            TagWord,        //token
            TagModifierClosingTag,      //token
            TagModifierSingletonTag,    //token
            TagSpace,
            TagEquals,
            TagParamValue,  //token
            TagEnd,         //token
        }

        readonly TokenizerFlexer flexer;

        public XmlTokenizer(bool keepSpaces = false)
        {
            flexer = new();
            if (keepSpaces)
            {
                // TokenType.Space should not appear at all
                flexer.Register(TokenType.Free)
                    .When('&').ChangeState(TokenType.EscapeSeq).Skip()
                    .When('<').ChangeState(TokenType.TagBegin).Skip()
                    .When('>').Error("Closing tag outside of tag structure")
                    .Otherwise().ChangeState(TokenType.Text)
                    .WhenEofDropState();
                flexer.Register(TokenType.Text)
                    .When('&').NextState(TokenType.EscapeSeq).Skip()
                    .When('<').NextState(TokenType.TagBegin).Skip()
                    .When('>').Error("Closing tag outside of tag structure")
                    .Otherwise().Consume()
                    .WhenEofYieldState();
            }
            else
            {
                flexer.Register(TokenType.Free)
                    .When(CharClass.Whitespace, '\n').ChangeState(TokenType.Space)
                    .When('&').ChangeState(TokenType.EscapeSeq).Skip()
                    .When('<').ChangeState(TokenType.TagBegin).Skip()
                    .When('>').Error("Closing tag outside of tag structure")
                    .Otherwise().ChangeState(TokenType.Text)
                    .WhenEofDropState();
                flexer.Register(TokenType.Text)
                    .When(CharClass.Whitespace, '\n').NextState(TokenType.Space)
                    .When('&').NextState(TokenType.EscapeSeq).Skip()
                    .When('<').NextState(TokenType.TagBegin).Skip()
                    .When('>').Error("Closing tag outside of tag structure")
                    .Otherwise().Consume()
                    .WhenEofYieldState();
                flexer.Register(TokenType.Space)
                    .When(CharClass.Whitespace, '\n').Skip()
                    .When('&').NextState(TokenType.EscapeSeq).Skip()
                    .When('<').NextState(TokenType.TagBegin).Skip()
                    .When('>').Error("Closing tag outside of tag structure")
                    .Otherwise().NextState(TokenType.Text)
                    .WhenEofDropState();
            }

            flexer.Register(TokenType.EscapeSeq)
                .When(';').NextState(TokenType.Free).Skip()
                .When(CharClass.Number, CharClass.LatinLetter).Consume()
                .Otherwise().Error("Invalid character in escape sequence")
                .WhenEofError();

            flexer.Register(TokenType.TagBegin)
                .When('/').NextState(TokenType.TagModifierClosingTag).Skip()
                .When(CharClass.LatinLetter).NextState(TokenType.TagWord)
                .Otherwise().Error("Invalid character in tag")
                .WhenEofError();
            flexer.Register(TokenType.TagWord)
                .When(CharClass.LatinLetter, CharClass.Number, '_', '-').Consume()
                .When(CharClass.Whitespace).NextState(TokenType.TagSpace)
                .When('=').NextState(TokenType.TagEquals).Skip()
                .When('/').NextState(TokenType.TagModifierSingletonTag).Skip()
                .When('>').NextState(TokenType.TagEnd).Skip()
                .Otherwise().Error("Invalid character in tag")
                .WhenEofError();

            flexer.Register(TokenType.TagModifierClosingTag)
                .When(CharClass.LatinLetter).NextState(TokenType.TagWord).Consume()
                .Otherwise().Error("Invalid character in tag")
                .WhenEofError();

            flexer.Register(TokenType.TagModifierSingletonTag)
                .When('>').NextState(TokenType.TagEnd).Skip()
                .Otherwise().Error("Invalid character in tag")
                .WhenEofError();
            flexer.Register(TokenType.TagSpace)
                .When(CharClass.Whitespace, '\n').Skip()
                .When(CharClass.LatinLetter).ChangeState(TokenType.TagWord).Consume()
                .When('=').ChangeState(TokenType.TagEquals).Skip()
                .Otherwise().Error("Invalid character in tag")
                .WhenEofError();

            flexer.Register(TokenType.TagEquals)
                .When('"').ChangeState(TokenType.TagParamValue).Skip()
                .Otherwise().Error("Invalid character in tag")
                .WhenEofError();

            flexer.Register(TokenType.TagParamValue)
                .When('"').Skip().NextState(TokenType.TagWord)
                .Otherwise().Consume()
                .WhenEofError();
            flexer.Register(TokenType.TagEnd)
                .Otherwise().NextState(TokenType.Free)
                .WhenEofYieldState();
        }

        public List<TokenizerFlexer.Token> Tokenize(IEnumerable<char> input) => flexer.FlexAll(input);
    }
}
