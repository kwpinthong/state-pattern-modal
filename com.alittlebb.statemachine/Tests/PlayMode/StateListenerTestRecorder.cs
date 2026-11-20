using System.Collections.Generic;
using UnityEngine;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Test target for the UnityEvents of a <see cref="StateListener"/>: each event calls one of the Record methods
    /// through a persistent entry, the way it is wired in the Inspector, and the call is written down.
    /// </summary>
    [AddComponentMenu("")] // test only: keep it out of the Add Component menu
    public class StateListenerTestRecorder : MonoBehaviour
    {
        public const string OnEnter = "OnEnter";
        public const string OnExit = "OnExit";
        public const string OnPaused = "OnPaused";
        public const string OnResumed = "OnResumed";

        public readonly List<string> Signals = new List<string>();

        public void RecordEnter() => Signals.Add(OnEnter);

        public void RecordExit() => Signals.Add(OnExit);

        public void RecordPaused() => Signals.Add(OnPaused);

        public void RecordResumed() => Signals.Add(OnResumed);
    }
}
