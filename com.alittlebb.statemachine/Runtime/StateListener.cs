using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Listens to the machine of a <see cref="StateMachineHost"/> for a set of <see cref="StateId"/> and raises
    /// UnityEvents, so a UI panel, a sound or any other object can follow the states from the Inspector, without code.
    /// <para>The rules are those of <see cref="StateMachine.Subscribe"/>: the listener is active while the current
    /// state is one of <see cref="States"/>, paused while one of them is on the stack under another state, and
    /// inactive otherwise. <see cref="OnEnter"/> is raised for inactive → active, <see cref="OnExit"/> for active or
    /// paused → inactive, <see cref="OnPaused"/> for active → paused and <see cref="OnResumed"/> for paused → active.
    /// Moving between two states of the set raises nothing.</para>
    /// <para>Lifetime: it subscribes in Awake and unsubscribes in OnDestroy, not in OnEnable and OnDisable. The events
    /// are still raised while its GameObject is inactive or the component is disabled, so a panel can hide itself
    /// with SetActive(false) in On Exit and show itself again in On Enter. In Awake it is synced with the stack:
    /// when one of its states is already there (a prefab instantiated while its state is the current one),
    /// On Enter is raised right away, followed by On Paused when that state is covered by another.</para>
    /// <para>Unity does not run Awake on a GameObject that starts inactive. Such a listener is not subscribed and
    /// gets nothing until its GameObject is activated for the first time; it is synced with the stack then.
    /// For a panel that must start hidden, put the listener on an object that starts active (its parent, for example)
    /// and point the events at the panel.</para>
    /// <para><see cref="Host"/> and <see cref="States"/> are read once, in Awake. Nothing is raised when the listener
    /// is destroyed: no On Exit.</para>
    /// </summary>
    [AddComponentMenu("ALittleBB/State Machine/State Listener")]
    public class StateListener : MonoBehaviour
    {
        [Tooltip("Host whose state machine this listens to. Leave empty to use the State Machine Host on this GameObject or on its nearest parent.")]
        [SerializeField] StateMachineHost _host;

        [Tooltip("The listener is active while the current state is one of these.")]
        [SerializeField] List<StateId> _states = new List<StateId>();

        [Tooltip("The current state became one of States.")]
        [SerializeField] UnityEvent _onEnter = new UnityEvent();

        [Tooltip("None of States is on the stack any more.")]
        [SerializeField] UnityEvent _onExit = new UnityEvent();

        [Tooltip("Another state was pushed on top of one of States.")]
        [SerializeField] UnityEvent _onPaused = new UnityEvent();

        [Tooltip("The state on top was popped and one of States is the current state again.")]
        [SerializeField] UnityEvent _onResumed = new UnityEvent();

        StateSubscription _subscription;

        /// <summary>
        /// Host whose machine this listens to, as set in the Inspector. When it is null in Awake, the host on this
        /// GameObject or on its nearest parent is used instead (a prefab cannot point at a host in the scene);
        /// when there is none either, one error is logged and no event is ever raised.
        /// Read once, in Awake: to set it from code, do it before the GameObject is first activated.
        /// </summary>
        public StateMachineHost Host
        {
            get => _host;
            set => _host = value;
        }

        /// <summary>
        /// The set of states this listener is active in. Empty slots are ignored, and an empty set never raises
        /// an event. Read once, in Awake: to set it from code, do it before the GameObject is first activated.
        /// </summary>
        public IReadOnlyList<StateId> States
        {
            get => _states;
            set => _states = value != null ? new List<StateId>(value) : new List<StateId>();
        }

        /// <summary>Raised when the current state becomes one of <see cref="States"/> from a state outside the set.</summary>
        public UnityEvent OnEnter => _onEnter;

        /// <summary>Raised when none of <see cref="States"/> is on the stack any more.</summary>
        public UnityEvent OnExit => _onExit;

        /// <summary>Raised when another state is pushed on top of one of <see cref="States"/>.</summary>
        public UnityEvent OnPaused => _onPaused;

        /// <summary>Raised when the state on top is popped and one of <see cref="States"/> is the current state again.</summary>
        public UnityEvent OnResumed => _onResumed;

        void Awake()
        {
            StateMachineHost host = _host != null ? _host : GetComponentInParent<StateMachineHost>(true);
            if (host == null)
            {
                Debug.LogError(
                    $"StateListener on '{name}': Host is not set and there is no StateMachineHost on this GameObject or its parents. " +
                    "It will not raise any event. Set Host in the Inspector, or from code before the GameObject is first activated.",
                    this);
                return;
            }

            _subscription = host.Machine.Subscribe(_states, _onEnter.Invoke, _onExit.Invoke, _onPaused.Invoke, _onResumed.Invoke);
        }

        void OnDestroy()
        {
            _subscription?.Dispose();
        }
    }
}
