using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shap.Flexer
{
    public class CallbackFlexer<StateType, ItemType, ItemClassType, ProcessorCtxType>()
        where StateType : Enum
        where ItemType : notnull
        where ItemClassType : IItemClass<ItemType>
        where ProcessorCtxType : class, new()
    {
        // man i would love if C# had >69iq type aliases
        readonly BaseFlexer<StateType, ItemType, ProcessorCtxType, Rule> baseFlexer = new();

        public class Rule() : FlexerStateHandler<StateType, ItemType, ProcessorCtxType>
        {
            List<IAction<StateType, ItemType, ProcessorCtxType>> curList = [];

            public delegate StateType FlexerCallback(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx);

            public Rule When(ItemClassType itemClass)
            {
                curList = AddClass(itemClass);
                return this;
            }

            public Rule Otherwise()
            {
                curList = AddClass(new ItemClassAlwaysTrue<ItemType>());
                return this;
            }
            
            public void WhenEndOk()
            {
                AddEndAction().Add(new ActionNoop());
            }

            public void WhenEndError(string? message = null)
            {
                AddEndAction().Add(new ActionError(message));
            }

            public Rule Do(FlexerCallback cb)
            {
                curList.Add(new ActionCb(cb)); return this;
            }

            public Rule Next()
            {
                curList.Add(new ActionNext()); return this;
            }

            public Rule Error(string? message = null)
            {
                curList.Add(new ActionError(message)); return this;
            }

            class ActionCb(FlexerCallback cb) : IAction<StateType, ItemType, ProcessorCtxType>
            {
                public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
                {
                    return cb.Invoke(curState, input, ctx);
                }
            }

            class ActionNoop : IAction<StateType, ItemType, ProcessorCtxType>
            {
                public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
                {
                    return curState;
                }
            }

            class ActionNext : IAction<StateType, ItemType, ProcessorCtxType>
            {
                public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
                {
                    input.MoveNext();
                    return curState;
                }
            }

            class ActionError(string? errorMessage = null) : IAction<StateType, ItemType, ProcessorCtxType>
            {
                readonly string? errorMessage = errorMessage;

                public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
                {
                    throw new Exception(errorMessage);
                }
            }
        }

        public Rule Register(StateType state) => baseFlexer.Register(state);

        public IEnumerable<StateType> Flex(IEnumerable<ItemType> input, ProcessorCtxType ctx, StateType? initState = default)
        {
            foreach (var state in baseFlexer.Process(input, ctx, initState))
            {
                yield return state;
            }
        }

        public void FlexAll(IEnumerable<ItemType> input, ProcessorCtxType ctx, StateType? initState = default)
        {
            baseFlexer.ProcessAll(input, ctx, initState);
        }
    }
}
