using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shap.Flexer
{
    public partial class BaseFlexer<StateType, ItemType, ProcessorCtxType, StateHandlerType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
        where StateHandlerType : FlexerStateHandler<StateType, ItemType, ProcessorCtxType>, new()
    {
        private readonly Dictionary<StateType, StateHandlerType> handlers = [];

        public StateHandlerType Register(StateType state)
        {
            if (handlers.ContainsKey(state)) throw new ArgumentException("State is already registered", nameof(state));
            StateHandlerType handler = new();
            handlers[state] = handler;
            return handler;
        }

        public IEnumerable<StateType>Process(IEnumerable<ItemType> input, ProcessorCtxType ctx, StateType? initState = default)
        {
            if (!input.Any()) yield break;

            BetterEnumerator<ItemType> inputEnumerator = new(input.GetEnumerator());
            StateType state = initState ?? (StateType)(object)0;
            bool mayExitLoop = false;

            do
            {
                if (!handlers.TryGetValue(state, out StateHandlerType? handler)) throw new KeyNotFoundException("Unregistered state");
                List<IAction<StateType, ItemType, ProcessorCtxType>> whatToDo;
                if (inputEnumerator.Ended)
                {
                    whatToDo = handler.WhatToDoOnEnd();
                    mayExitLoop = true;
                }
                else
                {
                    whatToDo = handler.WhatToDo(inputEnumerator.Current);
                }

                foreach (IAction<StateType, ItemType, ProcessorCtxType> action in whatToDo)
                {
                    state = action.Act(state, inputEnumerator, ctx);
                }

                yield return state;
            }
            while (!mayExitLoop);
        }

        public void ProcessAll(IEnumerable<ItemType> input, ProcessorCtxType ctx, StateType? initState = default)
        {
            foreach (var _ in Process(input, ctx, initState)) { }
        }

    }

    public abstract class FlexerStateHandler<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class, new()
    {
        private readonly Dictionary<IItemClass<ItemType>, List<IAction<StateType, ItemType, ProcessorCtxType>>> toDoList = [];
        private readonly List<IItemClass<ItemType>> itemClasses = [];
        private readonly List<IAction<StateType, ItemType, ProcessorCtxType>> endAction = [];

        protected List<IAction<StateType, ItemType, ProcessorCtxType>> AddClass
            (IItemClass<ItemType> cls, List<IAction<StateType, ItemType, ProcessorCtxType>>? sharedList = null)
        {
            if (toDoList.ContainsKey(cls)) throw new ArgumentException("Item class already handled", nameof(cls));
            List<IAction<StateType, ItemType, ProcessorCtxType>> list = sharedList ?? [];
            toDoList[cls] = list;
            itemClasses.Add(cls);
            return list;
        }

        protected List<IAction<StateType, ItemType, ProcessorCtxType>> AddEndAction() => endAction;

        public List<IAction<StateType, ItemType, ProcessorCtxType>> WhatToDo(ItemType item)
        {
            foreach (IItemClass<ItemType> cls in itemClasses)
            {
                if (cls.Contains(item)) return toDoList[cls];
            }
            throw new KeyNotFoundException("Unhandled item");
        }

        public List<IAction<StateType, ItemType, ProcessorCtxType>> WhatToDoOnEnd() => endAction;
    }

    public interface IAction<StateType, ItemType, ProcessorCtxType>
        where StateType : Enum
        where ItemType : notnull
        where ProcessorCtxType : class
    {
        public StateType Act(StateType curState, IEnumerator<ItemType> input, ProcessorCtxType ctx);
    }

    public interface IItemClass<ItemType>
        where ItemType : notnull
    {
        public bool Contains(ItemType item);
    }

    internal class BetterEnumerator<ItemType> : IEnumerator<ItemType>
        where ItemType : notnull
    {
        readonly IEnumerator<ItemType> underlying;

        public ItemType Current => underlying.Current;

        object IEnumerator.Current => underlying.Current;

        public bool Ended { get; private set; } = false;

        public BetterEnumerator(IEnumerator<ItemType> underlying)
        {
            this.underlying = underlying;
            this.underlying.MoveNext();
        }

        public void Dispose()
        {
            underlying.Dispose();
        }

        public bool MoveNext()
        {
            if (!Ended)
            {
                Ended = !underlying.MoveNext();
            }
            return !Ended;
        }

        public void Reset()
        {
            underlying.Reset();
        }
    }
}
