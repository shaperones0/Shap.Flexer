using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Shap.Flexer
{
    public interface IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class
    {
        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx);
    }

    public class ActionCb<StateType, ItemType, ProcessorCtxType>(ActionCb<StateType, ItemType, ProcessorCtxType>.Cb cb) 
        : IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
    {
        public delegate StateType Cb(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx);

        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
        {
            return cb.Invoke(curState, input, ctx);
        }
    }

    public class ActionNoop<StateType, ItemType, ProcessorCtxType> 
        : IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
    {
        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
        {
            return curState;
        }
    }

    public class ActionNext<StateType, ItemType, ProcessorCtxType> 
        : IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
    {
        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
        {
            input.MoveNext();
            return curState;
        }
    }

    public class ActionSetState<StateType, ItemType, ProcessorCtxType>(StateType newState)
        : IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
    {
        readonly StateType nextState = newState;

        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
        {
            return nextState;
        }
    }

    public class ActionError<StateType, ItemType, ProcessorCtxType>(string? errorMessage = null) 
        : IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
    {
        readonly string? errorMessage = errorMessage;

        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx)
        {
            throw new Exception(errorMessage);
        }
    }
}
