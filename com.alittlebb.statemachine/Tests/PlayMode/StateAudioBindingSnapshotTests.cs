#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-016: the TransitionToSnapshot action of StateAudioBinding. The test Audio Mixer
    /// (AudioMixers/StateAudioBindingSnapshotTest.mixer) has two snapshots, Normal and Muffled, and one exposed
    /// parameter, MusicVolume: the Master volume, 0 dB in Normal and -20 dB in Muffled. It is read with
    /// AudioMixer.GetFloat.
    /// <para>Measured on Unity 6000.6.9f1 with this mixer: a transition follows the mixer's Update Mode, which is
    /// Normal (scaled time) here: it stands still while Time.timeScale is 0 and, with Time.captureFramerate set,
    /// moves by game time, not real time. The value moves in a straight line in dB and ends exactly on the snapshot's
    /// value. GetFloat right after TransitionTo still gives the old value; a 0 second transition shows on the next
    /// frame. The mixer moves with nothing playing through it. So the tests run at a fixed 60 frames a second and
    /// count frames: 0.5 s is 30 frames.</para>
    /// </summary>
    public class StateAudioBindingSnapshotTests
    {
        const string MixerPath = "Packages/com.alittlebb.statemachine/Tests/PlayMode/AudioMixers/StateAudioBindingSnapshotTest.mixer";
        const string Parameter = "MusicVolume";
        const float NormalDb = 0f;
        const float MuffledDb = -20f;
        const float Tolerance = 0.01f;
        const int FramesPerSecond = 60;

        readonly List<Object> _created = new List<Object>();
        AudioMixer _mixer;
        AudioMixerSnapshot _normal;
        AudioMixerSnapshot _muffled;
        AudioMixerUpdateMode _updateMode;
        StateId _a;
        StateId _b;
        StateId _x;
        StateId _seed;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.That(_mixer, Is.Not.Null, MixerPath);
            _normal = _mixer.FindSnapshot("Normal");
            _muffled = _mixer.FindSnapshot("Muffled");
            Assert.That(_normal, Is.Not.Null, "snapshot Normal");
            Assert.That(_muffled, Is.Not.Null, "snapshot Muffled");
            _updateMode = _mixer.updateMode;

            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _x = CreateStateId("X");
            _seed = CreateStateId("Seed");

            Time.captureFramerate = FramesPerSecond;
            _normal.TransitionTo(0f);
            yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "Given: the mixer is at Normal");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.Destroy(created);
            }

            _created.Clear();
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (_mixer != null)
            {
                _mixer.updateMode = _updateMode;
                _normal.TransitionTo(0f);
            }

            yield return null;
        }

        // ---------------------------------------------------------------- C1

        [UnityTest]
        public IEnumerator C1_EnterSeed_TransitionToMuffled05_WhenPushSeed_TheParameterIsAtMuffledWithinHalfASecond()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_seed, StateMoment.Enter, _muffled, 0.5f));
            yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "Given: Normal");

            host.Push(_seed);

            var trace = new StringBuilder();
            int reached = -1;
            float halfway = float.NaN;
            for (int frame = 1; frame <= FramesPerSecond && reached < 0; frame++)
            {
                yield return null;
                float volume = Volume();
                trace.Append($" {frame}:{volume:F2}");
                if (frame == FramesPerSecond / 4)
                    halfway = volume;
                if (Mathf.Abs(volume - MuffledDb) <= Tolerance)
                    reached = frame;
            }

            Assert.That(reached, Is.InRange(1, FramesPerSecond / 2), $"at Muffled within 0.5 s (30 frames). Trace:{trace}");
            Assert.That(halfway, Is.LessThan(NormalDb - 1f).And.GreaterThan(MuffledDb + 1f),
                $"it is a transition over 0.5 s, not a jump: halfway between Normal and Muffled at 0.25 s. Trace:{trace}");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C1_AfterTheTransition_TheParameterStaysAtMuffled()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_seed, StateMoment.Enter, _muffled, 0.5f));
            yield return null;

            host.Push(_seed);
            yield return Frames(FramesPerSecond / 2);
            yield return Frames(FramesPerSecond / 2);

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "still at Muffled 1 s later");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C1_ThePushReturnsAndTheSignalIsSent_BeforeTheTransitionEnds()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_seed, StateMoment.Enter, _muffled, 0.5f));
            var signals = new List<StateChanged>();
            host.Machine.Changed += signals.Add;
            yield return null;

            host.Push(_seed);

            Assert.That(signals, Has.Count.EqualTo(1), "the signal was sent when Push returned");
            yield return null;
            Assert.That(Volume(), Is.GreaterThan(MuffledDb + 1f), "while the mixer has only just started to move");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_PausedAMuffled_ResumedANormal_WhenPushXThenPop_ItIsMuffledThenNormalAgain()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Paused, _muffled, 0f),
                ToSnapshot(_a, StateMoment.Resumed, _normal, 0f));
            yield return null;
            host.Switch(_a);
            yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "Given: Stack = [A], Normal");

            host.Push(_x);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "X pushed on top of A: Muffled");

            host.Pop();
            yield return null;

            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "X popped, A resumed: Normal again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C2_WithSeconds_PushXThenPop_ReachesMuffledThenNormal_WithinTheirSeconds()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Paused, _muffled, 0.25f),
                ToSnapshot(_a, StateMoment.Resumed, _normal, 0.25f));
            yield return null;
            host.Switch(_a);

            host.Push(_x);
            yield return WaitForVolume(MuffledDb, FramesPerSecond / 4, "Muffled within 0.25 s of the Push");

            host.Pop();
            yield return WaitForVolume(NormalDb, FramesPerSecond / 4, "Normal within 0.25 s of the Pop");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C2_PopDuringTheTransitionToMuffled_GoesBackToNormal()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Paused, _muffled, 0.5f),
                ToSnapshot(_a, StateMoment.Resumed, _normal, 0.25f));
            yield return null;
            host.Switch(_a);
            host.Push(_x);
            yield return Frames(FramesPerSecond / 4);
            Assert.That(Volume(), Is.LessThan(NormalDb - 1f).And.GreaterThan(MuffledDb + 1f), "Given: on the way to Muffled");

            host.Pop();

            yield return WaitForVolume(NormalDb, FramesPerSecond / 4, "Normal within 0.25 s of the Pop");
            yield return Frames(FramesPerSecond / 2);
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "and it stays at Normal: the first transition does not come back");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_TransitionToMuffled0_WhenTheSignalComes_ItIsMuffledOnTheNextFrame()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0f));
            yield return null;

            host.Switch(_a);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "Muffled on the frame after the Switch");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_TransitionToMuffled0_WhileTimeScaleIs0_ItIsStillMuffledOnTheNextFrame()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0f));
            yield return null;
            Time.timeScale = 0f;

            host.Switch(_a);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "a 0 second transition does not wait for time");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_ComponentStartsWhileAIsCurrent_TheEnterSnapshotOfAIsAppliedAtOnce()
        {
            StateMachineHost host = CreateHost();
            host.Switch(_a);

            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0f));
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "late sync: Muffled on the next frame");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_BindingsWithoutASnapshot_WarnOnceEach_NamingTheGameObject_AndTheOtherBindingStillWorks()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Enter, null, 0f),
                ToSnapshot(_a, StateMoment.Enter, null, 0.5f),
                ToSnapshot(_a, StateMoment.Enter, _muffled, 0f),
                ToSnapshot(_a, StateMoment.Exit, null, 0f),
                ToSnapshot(_b, StateMoment.Enter, _normal, 0f));
            yield return null;
            LogAssert.NoUnexpectedReceived(); // Given: nothing is logged before the signal comes

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Mixer binding': Bindings\[0\] \(A, Enter, TransitionToSnapshot\): Snapshot is not set"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Mixer binding': Bindings\[1\] \(A, Enter, TransitionToSnapshot\): Snapshot is not set"));
            Assert.DoesNotThrow(() => host.Switch(_a), "the signal comes");
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "the binding after the two broken ones still works");
            LogAssert.NoUnexpectedReceived(); // the Exit binding has not been needed yet

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Mixer binding': Bindings\[3\] \(A, Exit, TransitionToSnapshot\): Snapshot is not set"));
            Assert.DoesNotThrow(() => host.Switch(_b));
            yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "the binding after the broken Exit one still works");

            // The same signals again: the three warnings above are the only ones.
            for (int i = 0; i < 3; i++)
            {
                Assert.DoesNotThrow(() => host.Switch(_a));
                Assert.DoesNotThrow(() => host.Switch(_b));
            }

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_BindingWithoutASnapshot_DoesNotWarnAgain_WhenItsComponentIsDisabledAndEnabledAgain()
        {
            StateMachineHost host = CreateHost();
            StateAudioBinding binding = CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, null, 0f));
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Mixer binding': Bindings\[0\] \(A, Enter, TransitionToSnapshot\): Snapshot is not set"));
            host.Switch(_a);

            // Enter A is applied again each time it is enabled (late sync): it must not warn again.
            binding.enabled = false;
            binding.enabled = true;
            binding.enabled = false;
            binding.enabled = true;

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_WithoutAudioSource_TheSnapshotBindingsStillWork_AndLogNothing()
        {
            // TransitionToSnapshot does not use the BGM Audio Source: a binding that only moves the mixer
            // needs none.
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0f));
            yield return null;

            host.Switch(_a);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance));
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- other rules

        [UnityTest]
        public IEnumerator Rule_ExitA_TransitionToNormal_WhenSwitchToB_ItIsNormal()
        {
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Enter, _muffled, 0f),
                ToSnapshot(_a, StateMoment.Exit, _normal, 0f));
            yield return null;
            host.Switch(_a);
            yield return null;
            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "Given: Muffled in A");

            host.Switch(_b);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_TheTransitionFollowsTheMixersUpdateMode_Normal_StandsStillWhileTimeScaleIs0()
        {
            _mixer.updateMode = AudioMixerUpdateMode.Normal;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0.25f));
            yield return null;
            Time.timeScale = 0f;

            host.Switch(_a);
            yield return Frames(FramesPerSecond);

            Assert.That(Volume(), Is.GreaterThan(MuffledDb + 1f), "Update Mode Normal: game time, which is not moving");

            Time.timeScale = 1f;
            yield return WaitForVolume(MuffledDb, FramesPerSecond / 4 + 1, "it goes on once time moves again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_TheTransitionFollowsTheMixersUpdateMode_UnscaledTime_GoesOnWhileTimeScaleIs0()
        {
            _mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            Time.captureFramerate = 0;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0.25f));
            yield return null;
            Time.timeScale = 0f;

            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            while (Mathf.Abs(Volume() - MuffledDb) > Tolerance && Time.realtimeSinceStartup - start < 2f)
                yield return null;
            float took = Time.realtimeSinceStartup - start;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "Update Mode Unscaled Time: it gets there while Time.timeScale is 0");
            Assert.That(took, Is.GreaterThan(0.2f), "over its 0.25 s of real time");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- helpers

        float Volume()
        {
            Assert.That(_mixer.GetFloat(Parameter, out float value), Is.True, $"exposed parameter {Parameter}");
            return value;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        /// <summary>Waits at most <paramref name="frames"/> frames for the parameter to reach <paramref name="target"/>.</summary>
        IEnumerator WaitForVolume(float target, int frames, string what)
        {
            var trace = new StringBuilder();
            for (int frame = 1; frame <= frames; frame++)
            {
                yield return null;
                float volume = Volume();
                trace.Append($" {frame}:{volume:F2}");
                if (Mathf.Abs(volume - target) <= Tolerance)
                    yield break;
            }

            Assert.Fail($"{what}: not at {target} dB after {frames} frames. Trace:{trace}");
        }

        static StateAudioBinding.Entry ToSnapshot(StateId state, StateMoment moment, AudioMixerSnapshot snapshot, float seconds)
        {
            return new StateAudioBinding.Entry
            {
                State = state, Moment = moment, Action = StateAudioAction.TransitionToSnapshot, Snapshot = snapshot, Seconds = seconds,
            };
        }

        StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            _created.Add(id);
            return id;
        }

        /// <summary>A host without Initial State on its own active object: the test decides the state.</summary>
        StateMachineHost CreateHost()
        {
            var gameObject = new GameObject("Host");
            _created.Add(gameObject);
            return gameObject.AddComponent<StateMachineHost>();
        }

        /// <summary>A binding with no Audio Source, set up before its object is first activated. Active when this returns.</summary>
        StateAudioBinding CreateBinding(string name, StateMachineHost host, params StateAudioBinding.Entry[] bindings)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _created.Add(gameObject);
            StateAudioBinding binding = gameObject.AddComponent<StateAudioBinding>();
            binding.Host = host;
            binding.Bindings = bindings;
            gameObject.SetActive(true);
            return binding;
        }
    }
}
#endif
