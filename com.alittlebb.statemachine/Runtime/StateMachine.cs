using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Holds a stack of <see cref="StateId"/> and runs the <see cref="IState"/> registered for each.
    /// The top of the stack is the current state; the states underneath are paused.
    /// </summary>
    public class StateMachine
    {
        readonly Dictionary<StateId, IState> _states = new Dictionary<StateId, IState>();
        readonly List<StateId> _stack = new List<StateId>();
        readonly IReadOnlyList<StateId> _readOnlyStack;

        public StateMachine()
        {
            _readOnlyStack = _stack.AsReadOnly();
        }

        /// <summary>The state on top of <see cref="Stack"/>, or null before the first transition.</summary>
        public StateId Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        /// <summary>
        /// Read-only live view of the states that were entered and not exited yet, bottom → top.
        /// The last one is <see cref="Current"/>.
        /// </summary>
        public IReadOnlyList<StateId> Stack => _readOnlyStack;

        /// <summary>
        /// Raised for this machine's listeners only, once per transition, after every Exit and Enter
        /// of that transition has finished and <see cref="Current"/> is the new state.
        /// </summary>
        public event Action<StateChanged> Changed;

        /// <summary>Binds the logic of a state to its id.</summary>
        public void Register(StateId id, IState state)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _states[id] = state;
        }

        /// <summary>
        /// Exits every state on the stack from top to bottom, enters <paramref name="id"/> as the only state
        /// and sends one <see cref="Changed"/>. Does nothing when <paramref name="id"/> is already the current state.
        /// </summary>
        public void Switch(StateId id)
        {
            if (id == null)
            {
                Debug.LogError("StateMachine.Switch: StateId is null. The current state is unchanged.");
                return;
            }

            if (ReferenceEquals(id, Current))
                return;

            StateId from = Current;
            while (_stack.Count > 0)
                ExitTop();

            EnterOnTop(id);
            Changed?.Invoke(new StateChanged(from, id, StateChangeKind.Switch));
        }

        /// <summary>
        /// Enters <paramref name="id"/> on top of the current state without exiting it (it is paused)
        /// and sends <see cref="Changed"/>. A state can be on the stack only once.
        /// </summary>
        public void Push(StateId id)
        {
            if (id == null)
            {
                Debug.LogError("StateMachine.Push: StateId is null. The stack is unchanged.");
                return;
            }

            if (_stack.Contains(id))
            {
                Debug.LogError($"StateMachine.Push: '{id.name}' is already on the stack. The stack is unchanged.");
                return;
            }

            StateId from = Current;
            EnterOnTop(id);
            Changed?.Invoke(new StateChanged(from, id, StateChangeKind.Push));
        }

        /// <summary>
        /// Exits the current state and goes back to the state underneath without entering it again
        /// (it is resumed), then sends <see cref="Changed"/>. The last state cannot be popped.
        /// </summary>
        public void Pop()
        {
            if (_stack.Count == 0)
            {
                Debug.LogWarning("StateMachine.Pop: there is no state to pop.");
                return;
            }

            if (_stack.Count == 1)
            {
                Debug.LogWarning($"StateMachine.Pop: '{Current.name}' is the only state on the stack and cannot be popped. The stack is unchanged.");
                return;
            }

            StateId from = Current;
            ExitTop();
            Changed?.Invoke(new StateChanged(from, Current, StateChangeKind.Pop));
        }

        void EnterOnTop(StateId id)
        {
            _stack.Add(id);
            if (_states.TryGetValue(id, out IState state))
                state.Enter();
        }

        void ExitTop()
        {
            int top = _stack.Count - 1;
            if (_states.TryGetValue(_stack[top], out IState state))
                state.Exit();

            _stack.RemoveAt(top);
        }
    }
}
