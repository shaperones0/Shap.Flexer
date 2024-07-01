namespace Shap.Flexer
{
    /// <summary>
    /// Syntax exception class for handling syntaxical errors in provided documents
    /// </summary>
    /// <param name="line">Line, on which error has occured in provided document.</param>
    /// <param name="pos">Position on the line, where error has occured.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public class SyntaxException(int line, int pos, string? message = null, Exception? innerException = null)
        : Exception(message, innerException)
    {
        /// <summary>
        /// Line, on which error has occured in provided document.
        /// </summary>
        public readonly int line = line;

        /// <summary>
        /// Position on the line, where error has occured.
        /// </summary>
        public readonly int pos = pos;

        public static SyntaxException FromToken<StateType>(StringFlexer<StateType>.Token token, string? message = null, Exception? innerException = null)
            where StateType : Enum
        {
            return new(token.endPos.line, token.endPos.pos, message, innerException);
        }
    }

    public class StringFlexer<StateType>()
        where StateType : Enum
    {
        // man i would love if C# had >69iq type aliases
        readonly BaseFlexer<StateType, char, ProcessorCtx, Rule> baseFlexer = new();

        class AnyChar(char? c = null, CharClass? cls = null) : IItemClass<char>
        {
            readonly char? c = c;
            readonly CharClass? cls = cls;

            public bool Contains(char item)
            {
                if (c != null) return item == c;
                else
                {
                    return cls switch
                    {
                        CharClass.AnyLetter => char.IsLetter(item),
                        CharClass.LatinLetter => char.IsAsciiLetter(item),
                        CharClass.Number => char.IsNumber(item),
                        CharClass.Whitespace => char.IsWhiteSpace(item),
                        _ => throw new NotImplementedException(),
                    };
                }
            }
        }

        public enum CharClass
        {
            /// <summary>
            /// Corresponds to anything that can be classified as a letter.
            /// </summary>
            AnyLetter,

            /// <summary>
            /// Corresponds to latin letters only
            /// </summary>
            LatinLetter,

            /// <summary>
            /// Corresponds to 0-9.
            /// </summary>
            Number,

            /// <summary>
            /// Corresponds to any whitespace character.
            /// </summary>
            Whitespace,
        }

        public readonly struct TextPos(int i, int line, int pos)
        {
            public readonly int i = i, line = line, pos = pos;
        }

        public readonly struct Token(TextPos endPos, string str, StateType state)
        {
            public readonly TextPos endPos = endPos;
            public readonly string str = str;
            public readonly StateType type = state;
        }

        public class ProcessorCtx()
        {
            public List<char> currentTokenStr = [];
            public int curLine = 1, curPos = 0, i = 0;
            public Queue<Token> unyieldedTokens = [];
        }

        public class Rule : FlexerStateHandler<StateType, char, ProcessorCtx>
        {
            List<IAction<StateType, char, ProcessorCtx>> curList = [];

            public Rule When(char c)
            {
                curList = AddClass(new AnyChar(c: c));
                return this;
            }

            public Rule When(CharClass cls)
            {
                curList = AddClass(new AnyChar(cls: cls));
                return this;
            }

            public Rule When(params object[] objs)
            {
                List<IAction<StateType, char, ProcessorCtx>> sharedList = [];
                foreach (object obj in objs)
                {
                    if (obj is char c)
                        curList = AddClass(new AnyChar(c: c), sharedList);
                    //else if (obj.GetType() == typeof(CharClass)) 
                    //    AddTriggerClass((CharClass)obj, sharedList);
                    else if (obj is CharClass cls)
                        curList = AddClass(new AnyChar(cls: cls), sharedList);
                    else
                        throw new ArgumentException("Only chars and char classes expected", nameof(objs));
                }
                return this;
            }

            public Rule Otherwise()
            {
                curList = AddClass(new ItemClassAlwaysTrue<char>()); return this;
            }

            public Rule Skip()
            {
                curList.Add(new ActionSkip()); return this;
            }

            public Rule Consume()
            {
                curList.Add(new ActionConsume()); return this;
            }

            public Rule NextState(StateType state)
            {
                curList.Add(new ActionNextState(state)); return this;
            }

            public Rule ChangeState(StateType state)
            {
                curList.Add(new ActionChangeState(state)); return this;
            }

            public Rule Error(string? errorMessage = null)
            {
                curList.Add(new ActionError(errorMessage)); return this;
            }

            public void WhenEofYieldState()
            {
                AddEndAction().Add(new ActionYieldState());
            }

            public void WhenEofDropState()
            {
                AddEndAction().Add(new ActionDropState());
            }

            public void WhenEofError()
            {
                AddEndAction().Add(new ActionError("Unexpected EOF"));
            }

            class ActionSkip : IAction<StateType, char, ProcessorCtx>
            {
                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    if (input.Current == '\n')
                    {
                        ctx.curLine++;
                        ctx.curPos = 0;
                    }
                    else ctx.curPos++;
                    input.MoveNext(); ctx.i++;

                    return curState;
                }
            }

            class ActionConsume : IAction<StateType, char, ProcessorCtx>
            {
                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    ctx.currentTokenStr.Add(input.Current);

                    if (input.Current == '\n')
                    {
                        ctx.curLine++;
                        ctx.curPos = 0;
                    }
                    else ctx.curPos++;
                    input.MoveNext(); ctx.i++;

                    return curState;
                }
            }

            class ActionNextState(StateType nextState) : IAction<StateType, char, ProcessorCtx>
            {
                readonly StateType nextState = nextState;

                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    ctx.unyieldedTokens.Enqueue(new(new(ctx.i, ctx.curLine, ctx.curPos), new([.. ctx.currentTokenStr]), curState));
                    ctx.currentTokenStr = [];
                    return nextState;
                }
            }

            class ActionChangeState(StateType newState) : IAction<StateType, char, ProcessorCtx>
            {
                readonly StateType newState = newState;

                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    return newState;
                }
            }

            class ActionError(string? errorMessage = null) : IAction<StateType, char, ProcessorCtx>
            {
                readonly string? errorMessage = errorMessage;

                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    throw new SyntaxException(ctx.curLine, ctx.curPos, errorMessage);
                }
            }

            class ActionYieldState : IAction<StateType, char, ProcessorCtx>
            {
                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    ctx.unyieldedTokens.Enqueue(new(new(ctx.i, ctx.curLine, ctx.curPos), new([.. ctx.currentTokenStr]), curState));
                    ctx.currentTokenStr = [];
                    return curState;
                }
            }

            class ActionDropState : IAction<StateType, char, ProcessorCtx>
            {
                public StateType Act(StateType curState, IEnumerator<char> input, ProcessorCtx ctx)
                {
                    return curState;
                }
            }
        }

        public Rule Register(StateType state) => baseFlexer.Register(state);

        public IEnumerable<Token> Flex(IEnumerable<char> input, StateType? initState = default)
        {
            ProcessorCtx ctx = new();

            foreach (var _ in baseFlexer.Process(input, ctx, initState))
            {
                while (ctx.unyieldedTokens.Count > 0)
                {
                    yield return ctx.unyieldedTokens.Dequeue();
                }
            }
            while (ctx.unyieldedTokens.Count > 0)
            {
                yield return ctx.unyieldedTokens.Dequeue();
            }
        }

        public List<Token> FlexAll(IEnumerable<char> input, StateType? initState = default)
        {
            ProcessorCtx ctx = new();
            baseFlexer.ProcessAll(input, ctx, initState);
            return [.. ctx.unyieldedTokens];
        }
    }
}
