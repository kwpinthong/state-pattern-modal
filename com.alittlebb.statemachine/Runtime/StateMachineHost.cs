using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Puts one <see cref="StateMachine"/> in a scene. Set <see cref="InitialState"/> in the Inspector and call
    /// <see cref="Switch"/>, <see cref="Push"/> and <see cref="Pop"/> from a UnityEvent (a UI Button's On Click)
    /// or from code.
    /// <para>Order: <see cref="Machine"/> exists as soon as the component does, before any Awake, so another
    /// component can <see cref="StateMachine.Register"/> or <see cref="StateMachine.Subscribe"/> in its own Awake,
    /// OnEnable or Start whatever the script order. The initial state is entered in this component's Start:
    /// a listener that subscribed earlier gets OnEnter then, one that subscribes later gets it when it subscribes.
    /// State logic that must run for the initial state has to be registered before that Start, so register it in
    /// Awake or OnEnable.</para>
    /// <para>Every host has its own machine, which lives and dies with the component. Nothing is sent when the host
    /// is destroyed: no Exit, no signal.</para>
    /// </summary>
    [AddComponentMenu("ALittleBB/State Machine/State Machine Host")]
    public class StateMachineHost : MonoBehaviour
    {
        [Tooltip("State entered in Start. Leave empty to start without a state.")]
        [SerializeField] StateId _initialState;

        readonly StateMachine _machine = new StateMachine();

        /// <summary>The machine of this host. Never null; use it to register states and to subscribe listeners.</summary>
        public StateMachine Machine => _machine;

        /// <summary>
        /// State entered in Start, or null to start without a state. It is not entered when a state was already
        /// entered before Start, and setting it after Start changes nothing.
        /// </summary>
        public StateId InitialState
        {
            get => _initialState;
            set => _initialState = value;
        }

        void Start()
        {
            if (_initialState != null && _machine.Current == null)
                _machine.Switch(_initialState);
        }

        /// <summary>Same as <see cref="StateMachine.Switch"/> on <see cref="Machine"/>.</summary>
        public void Switch(StateId id) => _machine.Switch(id);

        /// <summary>Same as <see cref="StateMachine.Push"/> on <see cref="Machine"/>.</summary>
        public void Push(StateId id) => _machine.Push(id);

        /// <summary>Same as <see cref="StateMachine.Pop"/> on <see cref="Machine"/>.</summary>
        public void Pop() => _machine.Pop();
    }
}
