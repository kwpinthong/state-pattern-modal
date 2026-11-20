using System.Collections.Generic;
using UnityEngine;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Test listener: subscribes to the machine of a <see cref="StateMachineHost"/> and writes down every callback
    /// it is given. The list stays readable after the component was destroyed with its scene.
    /// </summary>
    [AddComponentMenu("")] // test only: keep it out of the Add Component menu
    public class HostTestListener : MonoBehaviour
    {
        public const string OnEnter = "OnEnter";
        public const string OnExit = "OnExit";
        public const string OnPaused = "OnPaused";
        public const string OnResumed = "OnResumed";
        public const string OnChanged = "OnChanged";

        public StateMachineHost host;
        public StateId[] states;

        [Tooltip("Subscribe in Awake instead of Start.")]
        public bool subscribeInAwake;

        [Tooltip("Dispose the subscription in OnDestroy. Off = a listener that never unsubscribes.")]
        public bool unsubscribeOnDestroy;

        public readonly List<string> Signals = new List<string>();

        public StateSubscription Subscription { get; private set; }

        void Awake()
        {
            if (subscribeInAwake)
                Subscribe();
        }

        void Start()
        {
            if (!subscribeInAwake)
                Subscribe();
        }

        void OnDestroy()
        {
            if (unsubscribeOnDestroy)
                Subscription?.Dispose();
        }

        void Subscribe()
        {
            Subscription = host.Machine.Subscribe(
                states,
                onEnter: () => Signals.Add(OnEnter),
                onExit: () => Signals.Add(OnExit),
                onPaused: () => Signals.Add(OnPaused),
                onResumed: () => Signals.Add(OnResumed),
                onChanged: _ => Signals.Add(OnChanged));
        }
    }
}
