using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Plays the background music of the states of a <see cref="StateMachineHost"/> on one Audio Source, from the
    /// Inspector and without code. Each entry of <see cref="Bindings"/> says: when this <see cref="StateId"/> is
    /// entered, exited, paused or resumed, play a clip as the BGM, stop the BGM, or move the Audio Mixer to a
    /// snapshot.
    /// <para>The moments are those of <see cref="StateMachine.Subscribe"/> for that one state, as for
    /// <see cref="StateAnimatorBinding"/>: Enter when it becomes the current state, Paused when another state is
    /// pushed on top of it, Resumed when it is the current state again after a Pop (not Enter: a Pop back to a state
    /// does not run its Enter bindings), Exit when it leaves the stack. Within one transition, every Exit and Paused
    /// action is applied before any Enter and Resumed action, whatever their order in the list.</para>
    /// <para>Play BGM plays the clip looped on <see cref="AudioSource"/>, at the volume set on that Audio Source.
    /// When that clip is already playing there, it goes on from where it is: two states with the same music can
    /// switch back and forth without a break. Stop BGM stops it.</para>
    /// <para>Lifetime: it follows the host while it is enabled on an active GameObject, from OnEnable to OnDisable.
    /// Each time it is enabled it is synced with the stack as if the states on it had been entered one by one, bottom
    /// first: the Enter actions of each state, with the Paused actions of a state applied before the Enter actions of
    /// the state above it. So the music of the current state plays as soon as the component starts, even when its
    /// state was entered before (the Initial State of the host). A clip that is already playing goes on. What happens
    /// while it is disabled is not applied, then or later. Disabling or destroying it does not stop the music.</para>
    /// <para>A binding without its Audio Source, Clip or Snapshot logs one warning the first time it is needed and
    /// does nothing; the other bindings still work. Without a host, one warning is logged and nothing is played.</para>
    /// <para><see cref="Host"/> and the states of <see cref="Bindings"/> are read each time the component is
    /// enabled.</para>
    /// </summary>
    [AddComponentMenu("ALittleBB/State Machine/State Audio Binding")]
    public class StateAudioBinding : MonoBehaviour
    {
        /// <summary>One line of <see cref="Bindings"/>: a state, a moment of that state, and what to do to the music then.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("The state this binding reacts to. A binding without a state does nothing.")]
            [SerializeField] StateId _state;

            [Tooltip("Enter: State becomes the current state. Exit: State leaves the stack. Paused: another state is pushed on top of State. Resumed: State is the current state again after a Pop.")]
            [SerializeField] StateMoment _moment;

            [Tooltip("What to do to the music at that moment.")]
            [SerializeField] StateAudioAction _action;

            [Tooltip("Clip for Play Bgm, played looped on the Audio Source.")]
            [SerializeField] AudioClip _clip;

            [Tooltip("Snapshot for Transition To Snapshot.")]
            [SerializeField] AudioMixerSnapshot _snapshot;

            [Tooltip("Fade of Play Bgm and Stop Bgm, or length of Transition To Snapshot, in seconds. 0 changes at once.")]
            [SerializeField, Min(0f)] float _seconds;

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

            /// <summary>What to do to the music.</summary>
            public StateAudioAction Action
            {
                get => _action;
                set => _action = value;
            }

            /// <summary>Clip played by <see cref="StateAudioAction.PlayBgm"/>.</summary>
            public AudioClip Clip
            {
                get => _clip;
                set => _clip = value;
            }

            /// <summary>Snapshot of <see cref="StateAudioAction.TransitionToSnapshot"/>.</summary>
            public AudioMixerSnapshot Snapshot
            {
                get => _snapshot;
                set => _snapshot = value;
            }

            /// <summary>
            /// Fade of <see cref="StateAudioAction.PlayBgm"/> and <see cref="StateAudioAction.StopBgm"/>, or length
            /// of <see cref="StateAudioAction.TransitionToSnapshot"/>, in seconds. 0 changes at once.
            /// </summary>
            public float Seconds
            {
                get => _seconds;
                set => _seconds = value;
            }
        }

        [Tooltip("Host whose state machine drives the music. Leave empty to use the State Machine Host on this GameObject or on its nearest parent.")]
        [SerializeField] StateMachineHost _host;

        [Tooltip("Audio Source that plays the background music. Its volume is the volume of the music.")]
        [SerializeField] AudioSource _audioSource;

        [Tooltip("What to do to the music, and at which moment of which state.")]
        [SerializeField] List<Entry> _bindings = new List<Entry>();

        readonly List<StateSubscription> _subscriptions = new List<StateSubscription>();
        readonly HashSet<Entry> _warned = new HashSet<Entry>();

        /// <summary>Moments told while subscribing, applied once every state is subscribed (see OnEnable).</summary>
        readonly List<(StateId state, StateMoment moment)> _sync = new List<(StateId, StateMoment)>();
        bool _syncing;
        bool _noHostLogged;

        /// <summary>
        /// Host whose machine drives the music, as set in the Inspector. When it is null, the host on this
        /// GameObject or on its nearest parent is used instead (a prefab cannot point at a host in the scene);
        /// when there is none either, one warning is logged and nothing is played.
        /// Read each time the component is enabled.
        /// </summary>
        public StateMachineHost Host
        {
            get => _host;
            set => _host = value;
        }

        /// <summary>Audio Source that plays the background music. Its volume is the volume of the music.</summary>
        public AudioSource AudioSource
        {
            get => _audioSource;
            set => _audioSource = value;
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
            StateMachineHost host = _host != null ? _host : GetComponentInParent<StateMachineHost>(true);
            if (host == null)
            {
                LogNoHostOnce();
                return;
            }

            // One listener per state, as in StateAnimatorBinding. Each listener is synced with the stack while it
            // subscribes; those moments are held back and applied afterwards in stack order, so that the music of
            // the top state wins whatever the order of the list.
            _syncing = true;
            var subscribed = new HashSet<StateId>();
            for (int i = 0; i < _bindings.Count; i++)
            {
                StateId state = _bindings[i]?.State;
                if (state == null || !subscribed.Add(state))
                    continue;

                _subscriptions.Add(host.Machine.Subscribe(
                    new[] { state },
                    () => OnMoment(state, StateMoment.Enter),
                    () => OnMoment(state, StateMoment.Exit),
                    () => OnMoment(state, StateMoment.Paused),
                    () => OnMoment(state, StateMoment.Resumed)));
            }

            _syncing = false;
            ApplySync(host.Machine.Stack);
        }

        void OnDisable()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Dispose();

            _subscriptions.Clear();
            _sync.Clear();
        }

        void OnMoment(StateId state, StateMoment moment)
        {
            if (_syncing)
                _sync.Add((state, moment));
            else
                Apply(state, moment);
        }

        /// <summary>
        /// Applies the moments held back while subscribing as if the states had been entered one by one, bottom
        /// first: a state's Enter, then its Paused (when it is covered), then the next state's Enter.
        /// </summary>
        void ApplySync(IReadOnlyList<StateId> stack)
        {
            // Stable sort by place in the stack keeps each state's Enter before its Paused.
            var ordered = new List<(StateId state, StateMoment moment)>(_sync);
            _sync.Clear();
            for (int i = 1; i < ordered.Count; i++)
            {
                (StateId state, StateMoment moment) item = ordered[i];
                int place = IndexOf(stack, item.state);
                int j = i - 1;
                while (j >= 0 && IndexOf(stack, ordered[j].state) > place)
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }

                ordered[j + 1] = item;
            }

            for (int i = 0; i < ordered.Count; i++)
                Apply(ordered[i].state, ordered[i].moment);
        }

        static int IndexOf(IReadOnlyList<StateId> stack, StateId state)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i], state))
                    return i;
            }

            return stack.Count;
        }

        void LogNoHostOnce()
        {
            if (_noHostLogged)
                return;

            _noHostLogged = true;
            Debug.LogWarning(
                $"StateAudioBinding on '{name}': Host is not set and there is no StateMachineHost on this GameObject or its parents. " +
                "It will not play anything. Set Host in the Inspector.",
                this);
        }

        /// <summary>Applies, in list order, the bindings of <paramref name="state"/> for <paramref name="moment"/>.</summary>
        void Apply(StateId state, StateMoment moment)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                Entry entry = _bindings[i];
                if (entry == null || entry.Moment != moment || !ReferenceEquals(entry.State, state))
                    continue;

                switch (entry.Action)
                {
                    case StateAudioAction.PlayBgm:
                        if (_audioSource == null)
                            WarnOnce(entry, i, "Audio Source is not set");
                        else if (entry.Clip == null)
                            WarnOnce(entry, i, "Clip is not set");
                        else
                            PlayBgm(entry.Clip, entry.Seconds);
                        break;

                    case StateAudioAction.StopBgm:
                        if (_audioSource == null)
                            WarnOnce(entry, i, "Audio Source is not set");
                        else
                            StopBgm(entry.Seconds);
                        break;

                    case StateAudioAction.TransitionToSnapshot:
                        if (entry.Snapshot == null)
                            WarnOnce(entry, i, "Snapshot is not set");
                        else
                            entry.Snapshot.TransitionTo(entry.Seconds);
                        break;
                }
            }
        }

        /// <summary>
        /// Plays <paramref name="clip"/> looped, or lets it go on when it is already playing.
        /// The fade is not done yet: any <paramref name="fadeSeconds"/> changes the music at once.
        /// </summary>
        void PlayBgm(AudioClip clip, float fadeSeconds)
        {
            _audioSource.loop = true;
            if (_audioSource.clip == clip && _audioSource.isPlaying)
                return;

            _audioSource.clip = clip;
            _audioSource.Play();
        }

        /// <summary>Stops the music. The fade is not done yet: any <paramref name="fadeSeconds"/> stops it at once.</summary>
        void StopBgm(float fadeSeconds)
        {
            _audioSource.Stop();
        }

        void WarnOnce(Entry entry, int index, string problem)
        {
            if (!_warned.Add(entry))
                return;

            Debug.LogWarning(
                $"StateAudioBinding on '{name}': Bindings[{index}] ({entry.State.name}, {entry.Moment}, {entry.Action}): " +
                $"{problem}. This binding does nothing.",
                this);
        }
    }
}
