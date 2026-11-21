using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Drives the Animator on its GameObject from the states of a <see cref="StateMachineHost"/>, from the Inspector
    /// and without code. Each entry of <see cref="Bindings"/> says: when this <see cref="StateId"/> is entered,
    /// exited, paused or resumed, set a trigger, a bool or an integer of the Animator, or cross fade to one of its
    /// states.
    /// <para>The moments are those of <see cref="StateMachine.Subscribe"/> for that one state: Enter when it becomes
    /// the current state, Paused when another state is pushed on top of it, Resumed when it is the current state
    /// again after a Pop, Exit when it leaves the stack. Within one transition, every Exit and Paused action is
    /// applied before any Enter and Resumed action, whatever their order in the list. The transition does not wait
    /// for the Animator: an action only starts the animation.</para>
    /// <para>Lifetime: it follows the host while it is enabled on an active GameObject, from OnEnable to OnDisable.
    /// Unity's Animator forgets its parameters and goes back to its default states while its GameObject is inactive,
    /// so each time this component is enabled it is synced with the stack like a new listener: every Enter action of
    /// the states that are on the stack is applied again (triggers and cross fades included), followed by the Paused
    /// actions of the states that are covered by another. What happens while it is disabled is not applied, then or
    /// later: an Exit or a Paused that comes while the GameObject is inactive is dropped. An Animator with Keep
    /// Animator State On Disable keeps what it had, so there the Enter actions are applied on top of it.</para>
    /// <para>A binding whose parameter or state is not in the Animator Controller, or whose parameter is of another
    /// type, logs one warning the first time it is needed and does nothing; the other bindings still work.
    /// Without an Animator on this GameObject or without a host, one error is logged and nothing is driven.</para>
    /// <para><see cref="Host"/> and the states of <see cref="Bindings"/> are read each time the component is
    /// enabled.</para>
    /// </summary>
    [AddComponentMenu("ALittleBB/State Machine/State Animator Binding")]
    public class StateAnimatorBinding : MonoBehaviour
    {
        /// <summary>One line of <see cref="Bindings"/>: a state, a moment of that state, and what to do to the Animator then.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("The state this binding reacts to. A binding without a state does nothing.")]
            [SerializeField] StateId _state;

            [Tooltip("Enter: State becomes the current state. Exit: State leaves the stack. Paused: another state is pushed on top of State. Resumed: State is the current state again after a Pop.")]
            [SerializeField] StateMoment _moment;

            [Tooltip("What to do to the Animator at that moment.")]
            [SerializeField] StateAnimatorAction _action;

            [Tooltip("Name of the Animator parameter, or of the Animator state for Cross Fade.")]
            [SerializeField] string _name = "";

            [Tooltip("Value for Set Bool.")]
            [SerializeField] bool _boolValue;

            [Tooltip("Value for Set Integer.")]
            [SerializeField] int _intValue;

            [Tooltip("Length of the Cross Fade, in seconds. 0 switches at once.")]
            [SerializeField, Min(0f)] float _duration;

            /// <summary>The state this binding reacts to. A binding without a state does nothing.</summary>
            public StateId State
            {
                get => _state;
                set => _state = value;
            }

            /// <summary>The moment of <see cref="State"/> at which <see cref="Action"/> is applied.</summary>
            public StateMoment Moment
            {
                get => _moment;
                set => _moment = value;
            }

            /// <summary>What to do to the Animator.</summary>
            public StateAnimatorAction Action
            {
                get => _action;
                set => _action = value;
            }

            /// <summary>
            /// Name of the Animator parameter (a Trigger for <see cref="StateAnimatorAction.SetTrigger"/>, a Bool for
            /// <see cref="StateAnimatorAction.SetBool"/>, an Int for <see cref="StateAnimatorAction.SetInteger"/>),
            /// or of the Animator state for <see cref="StateAnimatorAction.CrossFade"/>. The state is looked up on
            /// every layer and the first layer that has it is used.
            /// </summary>
            public string Name
            {
                get => _name;
                set => _name = value;
            }

            /// <summary>Value set by <see cref="StateAnimatorAction.SetBool"/>.</summary>
            public bool BoolValue
            {
                get => _boolValue;
                set => _boolValue = value;
            }

            /// <summary>Value set by <see cref="StateAnimatorAction.SetInteger"/>.</summary>
            public int IntValue
            {
                get => _intValue;
                set => _intValue = value;
            }

            /// <summary>Length of the <see cref="StateAnimatorAction.CrossFade"/>, in seconds. 0 switches at once.</summary>
            public float Duration
            {
                get => _duration;
                set => _duration = value;
            }
        }

        [Tooltip("Host whose state machine drives the Animator. Leave empty to use the State Machine Host on this GameObject or on its nearest parent.")]
        [SerializeField] StateMachineHost _host;

        [Tooltip("What to do to the Animator on this GameObject, and at which moment of which state.")]
        [SerializeField] List<Entry> _bindings = new List<Entry>();

        readonly List<StateSubscription> _subscriptions = new List<StateSubscription>();
        readonly HashSet<Entry> _warned = new HashSet<Entry>();
        Animator _animator;
        bool _setupErrorLogged;

        /// <summary>
        /// Host whose machine drives the Animator, as set in the Inspector. When it is null, the host on this
        /// GameObject or on its nearest parent is used instead (a prefab cannot point at a host in the scene);
        /// when there is none either, one error is logged and nothing is driven.
        /// Read each time the component is enabled.
        /// </summary>
        public StateMachineHost Host
        {
            get => _host;
            set => _host = value;
        }

        /// <summary>
        /// The bindings, applied in this order when several share a state and a moment. Empty slots and bindings
        /// without a state are ignored. The states to follow are read each time the component is enabled:
        /// to change the list from code, do it while the component is disabled or before its GameObject is first
        /// activated.
        /// </summary>
        public IReadOnlyList<Entry> Bindings
        {
            get => _bindings;
            set => _bindings = value != null ? new List<Entry>(value) : new List<Entry>();
        }

        void OnEnable()
        {
            _animator = GetComponent<Animator>();
            StateMachineHost host = _host != null ? _host : GetComponentInParent<StateMachineHost>(true);
            if (_animator == null || host == null)
            {
                LogSetupErrorOnce(_animator == null, host == null);
                return;
            }

            // One listener per state, so that the machine syncs each of them with the stack here (Enter, then Paused
            // when the state is covered) and tells every Exit and Paused before any Enter and Resumed later on.
            var subscribed = new HashSet<StateId>();
            for (int i = 0; i < _bindings.Count; i++)
            {
                StateId state = _bindings[i]?.State;
                if (state == null || !subscribed.Add(state))
                    continue;

                _subscriptions.Add(host.Machine.Subscribe(
                    new[] { state },
                    () => Apply(state, StateMoment.Enter),
                    () => Apply(state, StateMoment.Exit),
                    () => Apply(state, StateMoment.Paused),
                    () => Apply(state, StateMoment.Resumed)));
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Dispose();

            _subscriptions.Clear();
        }

        void LogSetupErrorOnce(bool noAnimator, bool noHost)
        {
            if (_setupErrorLogged)
                return;

            _setupErrorLogged = true;

            var problems = new List<string>(2);
            if (noAnimator)
                problems.Add("there is no Animator on this GameObject");
            if (noHost)
                problems.Add("Host is not set and there is no StateMachineHost on this GameObject or its parents");

            Debug.LogError(
                $"StateAnimatorBinding on '{name}': {string.Join(", and ", problems)}. It will not drive anything. " +
                "Put it on the GameObject of the Animator and set Host in the Inspector.",
                this);
        }

        /// <summary>Applies, in list order, the bindings of <paramref name="state"/> for <paramref name="moment"/>.</summary>
        void Apply(StateId state, StateMoment moment)
        {
            // The Animator can be removed while this component stays enabled.
            if (_animator == null)
                return;

            for (int i = 0; i < _bindings.Count; i++)
            {
                Entry entry = _bindings[i];
                if (entry == null || entry.Moment != moment || !ReferenceEquals(entry.State, state))
                    continue;

                switch (entry.Action)
                {
                    case StateAnimatorAction.SetTrigger:
                        if (FindParameter(entry, i, AnimatorControllerParameterType.Trigger, out int trigger))
                            _animator.SetTrigger(trigger);
                        break;

                    case StateAnimatorAction.SetBool:
                        if (FindParameter(entry, i, AnimatorControllerParameterType.Bool, out int boolean))
                            _animator.SetBool(boolean, entry.BoolValue);
                        break;

                    case StateAnimatorAction.SetInteger:
                        if (FindParameter(entry, i, AnimatorControllerParameterType.Int, out int integer))
                            _animator.SetInteger(integer, entry.IntValue);
                        break;

                    case StateAnimatorAction.CrossFade:
                        if (FindState(entry, i, out int animatorState, out int layer))
                            _animator.CrossFadeInFixedTime(animatorState, entry.Duration, layer);
                        break;
                }
            }
        }

        /// <summary>
        /// Checks the binding's parameter against the Animator Controller, so that Unity does not log its own
        /// warning for every signal. False, after one warning for this binding, when it is missing or of another type.
        /// </summary>
        bool FindParameter(Entry entry, int index, AnimatorControllerParameterType type, out int id)
        {
            id = Animator.StringToHash(entry.Name ?? "");

            RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
            if (controller == null)
            {
                WarnOnce(entry, index, "the Animator has no Controller");
                return false;
            }

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash != id)
                    continue;

                if (parameter.type == type)
                    return true;

                WarnOnce(entry, index, $"the parameter '{entry.Name}' of the Animator Controller '{controller.name}' is of type {parameter.type}, not {type}");
                return false;
            }

            WarnOnce(entry, index, $"the Animator Controller '{controller.name}' has no {type} parameter '{entry.Name}'");
            return false;
        }

        /// <summary>Finds the first layer that has the binding's Animator state. False, after one warning for this binding, when none has it.</summary>
        bool FindState(Entry entry, int index, out int id, out int layer)
        {
            id = Animator.StringToHash(entry.Name ?? "");
            layer = 0;

            RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
            if (controller == null)
            {
                WarnOnce(entry, index, "the Animator has no Controller");
                return false;
            }

            for (; layer < _animator.layerCount; layer++)
            {
                if (_animator.HasState(layer, id))
                    return true;
            }

            WarnOnce(entry, index, $"the Animator Controller '{controller.name}' has no state '{entry.Name}' on any of its layers");
            return false;
        }

        void WarnOnce(Entry entry, int index, string problem)
        {
            if (!_warned.Add(entry))
                return;

            Debug.LogWarning(
                $"StateAnimatorBinding on '{name}': Bindings[{index}] ({entry.State.name}, {entry.Moment}, {entry.Action} '{entry.Name}'): " +
                $"{problem}. This binding does nothing.",
                this);
        }
    }
}
