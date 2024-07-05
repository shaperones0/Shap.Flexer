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
                AddEndAction().Add(new ActionNoop<StateType, ItemType, ProcessorCtxType>());
            }

            public void WhenEndError(string? message = null)
            {
                AddEndAction().Add(new ActionError<StateType, ItemType, ProcessorCtxType>(message));
            }

            public Rule Do(ActionCb<StateType, ItemType, ProcessorCtxType>.Cb cb)
            {
                curList.Add(new ActionCb<StateType, ItemType, ProcessorCtxType>(cb)); return this;
            }

            public Rule Next()
            {
                curList.Add(new ActionNext<StateType, ItemType, ProcessorCtxType>()); return this;
            }

            public Rule Error(string? message = null)
            {
                curList.Add(new ActionError<StateType, ItemType, ProcessorCtxType>(message)); return this;
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
