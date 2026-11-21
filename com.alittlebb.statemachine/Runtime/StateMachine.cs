using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Holds the current <see cref="StateId"/> and runs the <see cref="IState"/> registered for it.
    /// </summary>
    public class StateMachine
    {
        readonly Dictionary<StateId, IState> _states = new Dictionary<StateId, IState>();

        /// <summary>The current state, or null before the first transition.</summary>
        public StateId Current { get; private set; }

        /// <summary>
        /// Raised for this machine's listeners only, after the old state's Exit and the new state's Enter
        /// have finished and <see cref="Current"/> is the new state.
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
        /// Exits the current state, enters <paramref name="id"/> and sends <see cref="Changed"/>.
        /// Does nothing when <paramref name="id"/> is already the current state.
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
            if (from is not null && _states.TryGetValue(from, out IState oldState))
                oldState.Exit();

            Current = id;
            if (_states.TryGetValue(id, out IState newState))
                newState.Enter();

            Changed?.Invoke(new StateChanged(from, id, StateChangeKind.Switch));
        }
    }
}
