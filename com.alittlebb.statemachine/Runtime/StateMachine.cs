using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Holds a stack of <see cref="StateId"/> and runs the <see cref="IState"/> registered for each.
    /// The top of the stack is the current state; the states underneath are paused.
    /// <para><see cref="Switch"/>, <see cref="Push"/> and <see cref="Pop"/> called from outside a transition finish
    /// before they return. Called while a transition is running (inside <see cref="IState.Enter"/>,
    /// <see cref="IState.Exit"/>, <see cref="Changed"/> or a callback of <see cref="Subscribe"/>) they only queue
    /// the request and return: the running transition first tells every listener, then the queued requests run
    /// one by one in the order they were made.</para>
    /// </summary>
    public class StateMachine
    {
        /// <summary>
        /// How many requests one outside call may run (itself plus everything queued behind it, counting requests
        /// that end up doing nothing). When more are still queued, an error is logged and they are dropped,
        /// so states that keep switching to each other cannot hang the game.
        /// </summary>
        public const int MaxTransitionsPerRun = 64;

        readonly Dictionary<StateId, IState> _states = new Dictionary<StateId, IState>();
        readonly List<StateId> _stack = new List<StateId>();
        readonly IReadOnlyList<StateId> _readOnlyStack;
        readonly List<StateSubscription> _subscriptions = new List<StateSubscription>();
        readonly Queue<Request> _requests = new Queue<Request>();
        bool _isRunning;
        int _sendDepth;
        bool _hasDisposedSubscriptions;

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
        /// Listeners added with <see cref="Subscribe"/> get their status callbacks before it.
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
        /// Subscribes a listener that should be active while the top of the stack is one of <paramref name="states"/>.
        /// <para>Its status is active when the top of the stack is in the set, paused when a state of the set is on the
        /// stack but the top is not, and inactive otherwise. After each transition: inactive → active calls
        /// <paramref name="onEnter"/>, active → paused <paramref name="onPaused"/>, paused → active
        /// <paramref name="onResumed"/>, and active or paused → inactive <paramref name="onExit"/>. Moving between
        /// states of the set calls none of them.</para>
        /// <para>Per transition, every listener that is paused or exited is told first, then every listener that is
        /// entered or resumed, then <see cref="Changed"/> is raised and <paramref name="onChanged"/> is called for
        /// every listener whatever its status — each step in subscribe order.</para>
        /// <para>The listener is synced with the stack before this method returns: <paramref name="onEnter"/> is called
        /// right away when a state of the set is already on the stack, followed by <paramref name="onPaused"/> when
        /// that state is not the top. A transition requested inside those two callbacks runs after both,
        /// still before this method returns.</para>
        /// </summary>
        /// <returns>The subscription; dispose it to unsubscribe.</returns>
        public StateSubscription Subscribe(
            IEnumerable<StateId> states,
            Action onEnter = null,
            Action onExit = null,
            Action onPaused = null,
            Action onResumed = null,
            Action<StateChanged> onChanged = null)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));

            var subscription = new StateSubscription(this, states, onEnter, onExit, onPaused, onResumed, onChanged);
            _subscriptions.Add(subscription);
            if (_isRunning)
                subscription.Arrive(_stack);
            else
                Run(subscription);

            return subscription;
        }

        /// <summary>
        /// Exits every state on the stack from top to bottom, enters <paramref name="id"/> as the only state
        /// and sends one <see cref="Changed"/>. Does nothing when <paramref name="id"/> is already the current state.
        /// </summary>
        public void Switch(StateId id) => Enqueue(StateChangeKind.Switch, id);

        /// <summary>
        /// Enters <paramref name="id"/> on top of the current state without exiting it (it is paused)
        /// and sends <see cref="Changed"/>. A state can be on the stack only once.
        /// </summary>
        public void Push(StateId id) => Enqueue(StateChangeKind.Push, id);

        /// <summary>
        /// Exits the current state and goes back to the state underneath without entering it again
        /// (it is resumed), then sends <see cref="Changed"/>. The last state cannot be popped.
        /// </summary>
        public void Pop() => Enqueue(StateChangeKind.Pop, null);

        void Enqueue(StateChangeKind kind, StateId id)
        {
            _requests.Enqueue(new Request(kind, id));
            if (!_isRunning)
                Run();
        }

        /// <summary>
        /// Syncs <paramref name="newcomer"/> with the stack (when a listener was just subscribed), then runs the
        /// queued requests one by one. Whatever they request in turn is queued behind them and run here too.
        /// A request is checked against the stack when its turn comes, exactly like a call from outside.
        /// </summary>
        void Run(StateSubscription newcomer = null)
        {
            _isRunning = true;
            try
            {
                newcomer?.Arrive(_stack);

                int ran = 0;
                while (_requests.Count > 0)
                {
                    if (ran == MaxTransitionsPerRun)
                    {
                        Debug.LogError(
                            $"StateMachine: stopped after {MaxTransitionsPerRun} transitions in one run. A state or listener " +
                            "keeps requesting a transition from inside a transition. " +
                            $"Current state: '{(Current != null ? Current.name : "none")}'. " +
                            $"Queued requests dropped: {_requests.Count}.");
                        break;
                    }

                    ran++;
                    Request request = _requests.Dequeue();
                    switch (request.Kind)
                    {
                        case StateChangeKind.Switch:
                            RunSwitch(request.Id);
                            break;
                        case StateChangeKind.Push:
                            RunPush(request.Id);
                            break;
                        case StateChangeKind.Pop:
                            RunPop();
                            break;
                    }
                }
            }
            finally
            {
                // After the limit or an exception: what is left must not run later, behind an unrelated call.
                _requests.Clear();
                _isRunning = false;
            }
        }

        void RunSwitch(StateId id)
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
            Send(new StateChanged(from, id, StateChangeKind.Switch));
        }

        void RunPush(StateId id)
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
            Send(new StateChanged(from, id, StateChangeKind.Push));
        }

        void RunPop()
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
            Send(new StateChanged(from, Current, StateChangeKind.Pop));
        }

        void Send(StateChanged signal)
        {
            // Listeners subscribed while this signal is being delivered already synced with the new stack.
            int count = _subscriptions.Count;
            _sendDepth++;
            try
            {
                for (int i = 0; i < count; i++)
                    _subscriptions[i].Leave(_stack);

                for (int i = 0; i < count; i++)
                    _subscriptions[i].Arrive(_stack);

                Changed?.Invoke(signal);

                for (int i = 0; i < count; i++)
                    _subscriptions[i].SendChanged(signal);
            }
            finally
            {
                _sendDepth--;
                if (_sendDepth == 0 && _hasDisposedSubscriptions)
                {
                    _hasDisposedSubscriptions = false;
                    _subscriptions.RemoveAll(subscription => subscription.IsDisposed);
                }
            }
        }

        /// <summary>Called by <see cref="StateSubscription.Dispose"/>.</summary>
        internal void Remove(StateSubscription subscription)
        {
            // The list must keep its indexes while Send walks it; the disposed one is skipped until then.
            if (_sendDepth > 0)
                _hasDisposedSubscriptions = true;
            else
                _subscriptions.Remove(subscription);
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

        /// <summary>A Switch, Push or Pop waiting for its turn.</summary>
        readonly struct Request
        {
            public readonly StateChangeKind Kind;
            public readonly StateId Id;

            public Request(StateChangeKind kind, StateId id)
            {
                Kind = kind;
                Id = id;
            }
        }
    }
}
