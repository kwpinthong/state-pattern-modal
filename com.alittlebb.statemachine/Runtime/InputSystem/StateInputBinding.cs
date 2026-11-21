using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Turns the action maps of one <see cref="InputActionAsset"/> on and off with the states of a
    /// <see cref="StateMachineHost"/>, from the Inspector and without code. Each entry of <see cref="Entries"/> says:
    /// while one of these states is active, this action map is enabled.
    /// <para>Active is the rule of <see cref="StateMachine.Subscribe"/> for the states of the entry: the map is
    /// enabled when a state of the entry becomes the current state and disabled when none of them is on the stack
    /// any more, before the Switch, Push or Pop call returns. (A paused state keeps its map enabled for now; see
    /// <see cref="WhenPaused"/>.) Moving between two states of the same entry does not
    /// touch the map, so a key held across that change is not cancelled. To keep a map enabled across several states,
    /// list them all in one entry.</para>
    /// <para>Maps that are in no entry are never touched: their enabled state is whatever other code set.</para>
    /// <para>Lives in the assembly <c>ALittleBB.StateMachines.InputSystem</c>, which compiles only when the project has
    /// the Input System package (com.unity.inputsystem). The core of the package does not need it.</para>
    /// <para><see cref="Host"/>, <see cref="Actions"/> and the states of <see cref="Entries"/> are read each time the
    /// component is enabled.</para>
    /// </summary>
    [AddComponentMenu("ALittleBB/State Machine/State Input Binding")]
    public class StateInputBinding : MonoBehaviour
    {
        /// <summary>
        /// What should happen to the map of an entry while its state is paused (another state is pushed on top).
        /// Not applied yet: for now a map stays enabled while its state is paused, whatever this option says, and is
        /// disabled when the state leaves the stack.
        /// </summary>
        public enum WhenPaused
        {
            /// <summary>The map is to be disabled while the state is covered, and enabled again when it is resumed.</summary>
            DisableMap,

            /// <summary>The map is to stay enabled while the state is on the stack, covered or not.</summary>
            KeepMapEnabled,
        }

        /// <summary>One line of <see cref="Entries"/>: a set of states and the action map enabled while one of them is active.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("The map is enabled while one of these states is the current state. Moving between them keeps it enabled.")]
            [SerializeField] List<StateId> _states = new List<StateId>();

            [Tooltip("Name of the action map of the Input Action Asset that is enabled while one of the states is active.")]
            [SerializeField] string _actionMap = "";

            [Tooltip("Disable Map: the map is to be disabled while another state is pushed on top. Keep Map Enabled: it is to stay enabled while the state is on the stack. Not applied yet: for now the map stays enabled while its state is paused.")]
            [SerializeField] WhenPaused _whenPaused;

            /// <summary>The states that enable <see cref="ActionMap"/>. Empty slots are ignored.</summary>
            public IReadOnlyList<StateId> States
            {
                get => _states;
                set => _states = value != null ? new List<StateId>(value) : new List<StateId>();
            }

            /// <summary>Name of the action map in <see cref="StateInputBinding.Actions"/>.</summary>
            public string ActionMap
            {
                get => _actionMap;
                set => _actionMap = value;
            }

            /// <summary>What happens to the map while its state is paused. Default: <see cref="StateInputBinding.WhenPaused.DisableMap"/>.</summary>
            public WhenPaused WhenPaused
            {
                get => _whenPaused;
                set => _whenPaused = value;
            }
        }

        [Tooltip("Host whose state machine drives the action maps.")]
        [SerializeField] StateMachineHost _host;

        [Tooltip("Input Action Asset whose action maps are enabled and disabled.")]
        [SerializeField] InputActionAsset _actions;

        [Tooltip("Which action map is enabled while which states are active.")]
        [SerializeField] List<Entry> _entries = new List<Entry>();

        readonly List<StateSubscription> _subscriptions = new List<StateSubscription>();

        /// <summary>Host whose machine drives the action maps. Read each time the component is enabled.</summary>
        public StateMachineHost Host
        {
            get => _host;
            set => _host = value;
        }

        /// <summary>The asset whose action maps are enabled and disabled. Read each time the component is enabled.</summary>
        public InputActionAsset Actions
        {
            get => _actions;
            set => _actions = value;
        }

        /// <summary>
        /// The entries. Empty slots and entries without a state are ignored. Read each time the component is enabled:
        /// to change the list from code, do it while the component is disabled or before its GameObject is first activated.
        /// </summary>
        public IReadOnlyList<Entry> Entries
        {
            get => _entries;
            set => _entries = value != null ? new List<Entry>(value) : new List<Entry>();
        }

        void OnEnable()
        {
            if (_host == null || _actions == null)
                return;

            // One listener per entry: the machine tells it when the set of the entry becomes active and when it is
            // left, and nothing while the current state moves between states of the set.
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null)
                    continue;

                var states = new List<StateId>(entry.States.Count);
                foreach (StateId state in entry.States)
                {
                    if (state != null)
                        states.Add(state);
                }

                if (states.Count == 0)
                    continue;

                _subscriptions.Add(_host.Machine.Subscribe(
                    states,
                    onEnter: () => FindMap(entry)?.Enable(),
                    onExit: () => FindMap(entry)?.Disable()));
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Dispose();

            _subscriptions.Clear();
        }

        InputActionMap FindMap(Entry entry) =>
            _actions != null ? _actions.FindActionMap(entry.ActionMap ?? "", throwIfNotFound: false) : null;
    }
}
