using System;
using System.Collections.Generic;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// A listener registered with <see cref="StateMachine.Subscribe"/> for a set of <see cref="StateId"/>.
    /// Its <see cref="Status"/> follows the machine's stack; <see cref="Dispose"/> unsubscribes it.
    /// </summary>
    public sealed class StateSubscription : IDisposable
    {
        readonly StateMachine _machine;
        readonly HashSet<StateId> _states;
        readonly Action _onEnter;
        readonly Action _onExit;
        readonly Action _onPaused;
        readonly Action _onResumed;
        readonly Action<StateChanged> _onChanged;

        internal StateSubscription(
            StateMachine machine,
            IEnumerable<StateId> states,
            Action onEnter,
            Action onExit,
            Action onPaused,
            Action onResumed,
            Action<StateChanged> onChanged)
        {
            _machine = machine;
            _states = new HashSet<StateId>(states);
            _onEnter = onEnter;
            _onExit = onExit;
            _onPaused = onPaused;
            _onResumed = onResumed;
            _onChanged = onChanged;
        }

        /// <summary>Where this listener stands as of the last callback it was given.</summary>
        public StateListenerStatus Status { get; private set; }

        internal bool IsDisposed { get; private set; }

        /// <summary>
        /// Unsubscribes. No callback is called from now on, not even OnExit, and not for a transition
        /// that is being delivered right now. Safe to call inside a callback and more than once.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            _machine.Remove(this);
        }

        /// <summary>Calls OnPaused or OnExit when the stack makes this listener less active than it was.</summary>
        internal void Leave(List<StateId> stack)
        {
            StateListenerStatus next = StatusFor(stack);

            if (Status == StateListenerStatus.Active && next == StateListenerStatus.Paused)
            {
                Status = StateListenerStatus.Paused;
                Call(_onPaused);
            }
            else if (Status != StateListenerStatus.Inactive && next == StateListenerStatus.Inactive)
            {
                Status = StateListenerStatus.Inactive;
                Call(_onExit);
            }
        }

        /// <summary>Calls OnEnter or OnResumed when the stack makes this listener more active than it was.</summary>
        internal void Arrive(List<StateId> stack)
        {
            StateListenerStatus next = StatusFor(stack);

            if (Status == StateListenerStatus.Inactive && next != StateListenerStatus.Inactive)
            {
                Status = StateListenerStatus.Active;
                Call(_onEnter);

                // Only a late subscriber can find its state already covered by another one.
                if (next == StateListenerStatus.Paused && !IsDisposed)
                {
                    Status = StateListenerStatus.Paused;
                    Call(_onPaused);
                }
            }
            else if (Status == StateListenerStatus.Paused && next == StateListenerStatus.Active)
            {
                Status = StateListenerStatus.Active;
                Call(_onResumed);
            }
        }

        internal void SendChanged(StateChanged signal)
        {
            if (!IsDisposed)
                _onChanged?.Invoke(signal);
        }

        StateListenerStatus StatusFor(List<StateId> stack)
        {
            int top = stack.Count - 1;
            if (top < 0)
                return StateListenerStatus.Inactive;

            if (_states.Contains(stack[top]))
                return StateListenerStatus.Active;

            for (int i = top - 1; i >= 0; i--)
            {
                if (_states.Contains(stack[i]))
                    return StateListenerStatus.Paused;
            }

            return StateListenerStatus.Inactive;
        }

        void Call(Action callback)
        {
            if (!IsDisposed)
                callback?.Invoke();
        }
    }
}
