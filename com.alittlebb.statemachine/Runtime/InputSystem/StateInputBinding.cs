using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Turns the action maps of one <see cref="InputActionAsset"/> on and off with the states of a
    /// <see cref="StateMachineHost"/>, from the Inspector and without code. Each entry of <see cref="Entries"/> says:
    /// while one of these states is active, this action map is enabled.
    /// <para>Active is the rule of <see cref="StateMachine.Subscribe"/> for the states of the entry, applied before the
    /// Switch, Push or Pop call returns: the map is enabled when a state of the entry becomes the current state and
    /// disabled when none of them is on the stack any more. While a state of the entry is paused (another state is
    /// pushed on top of it), <see cref="Entry.WhenPaused"/> decides: <see cref="WhenPaused.DisableMap"/> (the default)
    /// disables the map until the state is resumed by a Pop, <see cref="WhenPaused.KeepMapEnabled"/> leaves it enabled.
    /// Transitions requested inside a transition are queued by the machine, so once the queue is empty the maps
    /// match the last state. Moving between two states of the same entry does not touch the map, so a key held across
    /// that change is not cancelled. To keep a map enabled across several states, list them all in one entry.</para>
    /// <para>Maps that are in no entry are never touched: their enabled state is whatever other code set.</para>
    /// <para>Lifetime: each time the component is enabled (first activation, after Instantiate, or enabled again) it
    /// is synced with the stack: the maps of the entries that are active now are enabled, and every other map of the
    /// list is disabled. When it is disabled or destroyed, every map of the list is disabled, and nothing that happens
    /// while it is disabled is applied.</para>
    /// <para>Wrong settings log one warning per problem, naming the GameObject and the maps, and throw nothing: without
    /// <see cref="Host"/> or <see cref="Actions"/> no map is driven; an entry whose map is not in the asset does nothing
    /// while the other entries still work.</para>
    /// <para>Lives in the assembly <c>ALittleBB.StateMachines.InputSystem</c>, which compiles only when the project has
    /// the Input System package (com.unity.inputsystem). The core of the package does not need it.</para>
    /// <para><see cref="Host"/>, <see cref="Actions"/> and <see cref="Entries"/> are read each time the
    /// component is enabled.</para>
    /// </summary>
    [AddComponentMenu("ALittleBB/State Machine/State Input Binding")]
    public class StateInputBinding : MonoBehaviour
    {
        /// <summary>What happens to the map of an entry while its state is paused (another state is pushed on top).</summary>
        public enum WhenPaused
        {
            /// <summary>The map is disabled while the state is covered, and enabled again when it is resumed.</summary>
            DisableMap,

            /// <summary>The map stays enabled while the state is on the stack, covered or not.</summary>
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

            [Tooltip("Disable Map: the map is disabled while another state is pushed on top, and enabled again after the Pop. Keep Map Enabled: it stays enabled while the state is on the stack.")]
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

        /// <summary>The entries that drive a map while the component is enabled.</summary>
        readonly List<Bound> _bound = new List<Bound>();
        readonly HashSet<Entry> _warnedEntries = new HashSet<Entry>();
        bool _warnedNoHost;
        bool _warnedNoActions;

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
            {
                WarnSetupOnce();
                return;
            }

            // One listener per entry: the machine tells it when the set of the entry becomes active, paused, resumed
            // and left, and nothing while the current state moves between states of the set. Subscribing syncs it with
            // the stack right away (Enter, then Paused when its state is covered).
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                List<StateId> states = StatesOf(entry);
                if (states.Count == 0)
                    continue;

                InputActionMap map = _actions.FindActionMap(entry.ActionMap ?? "", throwIfNotFound: false);
                if (map == null)
                {
                    WarnMissingMapOnce(entry, i);
                    continue;
                }

                StateSubscription subscription = _host.Machine.Subscribe(
                    states,
                    onEnter: map.Enable,
                    onExit: map.Disable,
                    onPaused: () =>
                    {
                        if (entry.WhenPaused == WhenPaused.DisableMap)
                            map.Disable();
                    },
                    onResumed: map.Enable);

                _bound.Add(new Bound(entry, map, subscription));
            }

            // A map of the list that no entry wants now is disabled, whatever other code set before.
            foreach (Bound bound in _bound)
            {
                if (!_bound.Any(other => other.Map == bound.Map && other.WantsMapEnabled))
                    bound.Map.Disable();
            }
        }

        void OnDisable()
        {
            foreach (Bound bound in _bound)
                bound.Subscription.Dispose();

            // The asset may be destroyed before this component.
            if (_actions != null)
            {
                foreach (Bound bound in _bound)
                    bound.Map.Disable();
            }

            _bound.Clear();
        }

        static List<StateId> StatesOf(Entry entry)
        {
            var states = new List<StateId>();
            if (entry == null)
                return states;

            foreach (StateId state in entry.States)
            {
                if (state != null)
                    states.Add(state);
            }

            return states;
        }

        void WarnSetupOnce()
        {
            string maps = string.Join(", ", _entries
                .Where(entry => StatesOf(entry).Count > 0)
                .Select(entry => $"'{entry.ActionMap}'"));
            if (maps.Length == 0)
                maps = "(no entry)";

            if (_host == null && !_warnedNoHost)
            {
                _warnedNoHost = true;
                Debug.LogWarning(
                    $"StateInputBinding on '{name}': Host is not set, so the action maps {maps} are not driven. " +
                    "Set Host in the Inspector.",
                    this);
            }

            if (_actions == null && !_warnedNoActions)
            {
                _warnedNoActions = true;
                Debug.LogWarning(
                    $"StateInputBinding on '{name}': Actions (the Input Action Asset) is not set, so the action maps {maps} " +
                    "are not driven. Set Actions in the Inspector.",
                    this);
            }
        }

        void WarnMissingMapOnce(Entry entry, int index)
        {
            if (!_warnedEntries.Add(entry))
                return;

            Debug.LogWarning(
                $"StateInputBinding on '{name}': Entries[{index}]: the Input Action Asset '{_actions.name}' has no action map " +
                $"'{entry.ActionMap}'. This entry does nothing; the other entries still work.",
                this);
        }

        /// <summary>An entry with its map found in the asset, and the listener that drives it.</summary>
        readonly struct Bound
        {
            public readonly Entry Entry;
            public readonly InputActionMap Map;
            public readonly StateSubscription Subscription;

            public Bound(Entry entry, InputActionMap map, StateSubscription subscription)
            {
                Entry = entry;
                Map = map;
                Subscription = subscription;
            }

            /// <summary>Whether the entry wants its map enabled for the stack as it is now.</summary>
            public bool WantsMapEnabled =>
                Subscription.Status == StateListenerStatus.Active ||
                (Subscription.Status == StateListenerStatus.Paused && Entry.WhenPaused == WhenPaused.KeepMapEnabled);
        }
    }
}
