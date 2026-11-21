#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-018: the Time Scale choice of a Transition To Snapshot binding. Ignore Time Scale (the default) runs the
    /// transition on real time, Follow Time Scale on game time, whatever the Update Mode of the Audio Mixer.
    /// The mixer is AudioMixers/StateAudioBindingSnapshotTest.mixer: Normal 0 dB, Muffled -20 dB, exposed MusicVolume.
    /// <para>Measured on Unity 6000.6.9f1: with Time.captureFramerate set, Time.deltaTime is fixed (game time) but
    /// Time.unscaledDeltaTime stays real time, and so does a mixer in Unscaled Time. So the tests of Ignore measure
    /// real time (Time.realtimeSinceStartup), and those of Follow count frames at 60 frames a second.</para>
    /// </summary>
    public class StateAudioBindingSnapshotTimeScaleTests
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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.That(_mixer, Is.Not.Null, MixerPath);
            _normal = _mixer.FindSnapshot("Normal");
            _muffled = _mixer.FindSnapshot("Muffled");
            _updateMode = _mixer.updateMode;
            _a = CreateStateId("A");
            _b = CreateStateId("B");

            Time.captureFramerate = 0;
            Time.timeScale = 1f;
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
            yield return null;
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (_mixer != null)
            {
                _mixer.updateMode = _updateMode;
                _normal.TransitionTo(0f);
            }

            yield return null;
        }

        // ---------------------------------------------------------------- C2: Ignore Time Scale

        [UnityTest]
        public IEnumerator GEN018_C2_Ignore_NormalMixer_TimeScale0_ReachesMuffledWithinHalfASecondOfRealTime()
        {
            yield return IgnoreReachesMuffledOnRealTime(AudioMixerUpdateMode.Normal, 0f, 0.5f);
        }

        [UnityTest]
        public IEnumerator GEN018_C2_Ignore_UnscaledTimeMixer_TimeScale0_ReachesMuffledWithinHalfASecondOfRealTime()
        {
            yield return IgnoreReachesMuffledOnRealTime(AudioMixerUpdateMode.UnscaledTime, 0f, 0.5f);
        }

        [UnityTest]
        public IEnumerator GEN018_C2_Ignore_NormalMixer_TimeScaleHalf_StillTakesHalfASecondOfRealTime()
        {
            yield return IgnoreReachesMuffledOnRealTime(AudioMixerUpdateMode.Normal, 0.5f, 0.5f);
        }

        IEnumerator IgnoreReachesMuffledOnRealTime(AudioMixerUpdateMode mode, float timeScale, float seconds)
        {
            _mixer.updateMode = mode;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, seconds, StateAudioTimeScale.IgnoreTimeScale));
            yield return null;
            Time.timeScale = timeScale;

            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            float halfway = float.NaN;
            float longestFrame = 0f;
            float previous = 0f;
            var trace = new StringBuilder();
            while (Mathf.Abs(Volume() - MuffledDb) > Tolerance && Time.realtimeSinceStartup - start < 2f)
            {
                yield return null;
                float now = Time.realtimeSinceStartup - start;
                longestFrame = Mathf.Max(longestFrame, now - previous);
                previous = now;
                if (float.IsNaN(halfway) && now >= seconds / 2f)
                    halfway = Volume();
                trace.Append($" {now:F2}:{Volume():F1}");
            }

            float took = Time.realtimeSinceStartup - start;
            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), $"at Muffled with Time.timeScale {timeScale} on a {mode} mixer. Trace:{trace}");
            Assert.That(took, Is.LessThanOrEqualTo(seconds + 0.1f + Hitch(longestFrame)), $"within {seconds} s of real time. Trace:{trace}");
            Assert.That(took, Is.GreaterThanOrEqualTo(seconds * 0.8f), $"a transition over {seconds} s, not a jump. Trace:{trace}");
            Assert.That(halfway, Is.LessThan(NormalDb - 1f).And.GreaterThan(MuffledDb + 1f), $"between Normal and Muffled half way. Trace:{trace}");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3: Follow Time Scale while time stands still

        [UnityTest]
        public IEnumerator GEN018_C3_Follow_UnscaledTimeMixer_TimeScale0_StandsStill_ThenReachesMuffledAfterHalfASecondOfGameTime()
        {
            yield return FollowStandsStillAtTimeScale0(AudioMixerUpdateMode.UnscaledTime);
        }

        [UnityTest]
        public IEnumerator GEN018_C3_Follow_NormalMixer_TimeScale0_StandsStill_ThenReachesMuffledAfterHalfASecondOfGameTime()
        {
            yield return FollowStandsStillAtTimeScale0(AudioMixerUpdateMode.Normal);
        }

        IEnumerator FollowStandsStillAtTimeScale0(AudioMixerUpdateMode mode)
        {
            _mixer.updateMode = mode;
            Time.captureFramerate = FramesPerSecond;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0.5f, StateAudioTimeScale.FollowTimeScale));
            yield return null;
            Time.timeScale = 0f;
            yield return null; // Time.deltaTime of the frame Time.timeScale is set in was computed before

            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.6f)
                yield return null;

            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), $"{mode} mixer: it does not move while Time.timeScale is 0 (0.6 s of real time)");

            Time.timeScale = 1f;
            var trace = new StringBuilder();
            int reached = -1;
            float halfway = float.NaN;
            for (int frame = 1; frame <= 2 * FramesPerSecond && reached < 0; frame++)
            {
                yield return null;
                float volume = Volume();
                trace.Append($" {frame}:{volume:F2}");
                if (frame == FramesPerSecond / 4)
                    halfway = volume;
                if (Mathf.Abs(volume - MuffledDb) <= Tolerance)
                    reached = frame;
            }

            Assert.That(reached, Is.InRange(FramesPerSecond / 2 - 1, FramesPerSecond / 2 + 1), $"at Muffled after 0.5 s of game time (30 frames). Trace:{trace}");
            Assert.That(halfway, Is.LessThan(NormalDb - 1f).And.GreaterThan(MuffledDb + 1f), $"between Normal and Muffled at 0.25 s. Trace:{trace}");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C4: Follow Time Scale at half speed

        [UnityTest]
        public IEnumerator GEN018_C4_Follow_UnscaledTimeMixer_TimeScaleHalf_ReachesMuffledAfterAboutOneSecondOfRealTime()
        {
            yield return FollowAtHalfSpeed(AudioMixerUpdateMode.UnscaledTime);
        }

        [UnityTest]
        public IEnumerator GEN018_C4_Follow_NormalMixer_TimeScaleHalf_ReachesMuffledAfterAboutOneSecondOfRealTime()
        {
            yield return FollowAtHalfSpeed(AudioMixerUpdateMode.Normal);
        }

        IEnumerator FollowAtHalfSpeed(AudioMixerUpdateMode mode)
        {
            _mixer.updateMode = mode;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0.5f, StateAudioTimeScale.FollowTimeScale));
            yield return null;
            Time.timeScale = 0.5f;

            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            float at08 = float.NaN;
            float longestFrame = 0f;
            float previous = 0f;
            var trace = new StringBuilder();
            while (Mathf.Abs(Volume() - MuffledDb) > Tolerance && Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                float now = Time.realtimeSinceStartup - start;
                longestFrame = Mathf.Max(longestFrame, now - previous);
                previous = now;
                if (float.IsNaN(at08) && now >= 0.8f)
                    at08 = Volume();
                trace.Append($" {now:F2}:{Volume():F1}");
            }

            float took = Time.realtimeSinceStartup - start;
            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), $"{mode} mixer: at Muffled. Trace:{trace}");
            Assert.That(took, Is.InRange(0.9f, 1.2f + Hitch(longestFrame)), $"0.5 s of game time at Time.timeScale 0.5: about 1 s of real time. Trace:{trace}");
            Assert.That(at08, Is.GreaterThan(MuffledDb + 1f), $"not at Muffled yet after 0.8 s of real time. Trace:{trace}");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C5: a transition during a transition

        [UnityTest]
        public IEnumerator GEN018_C5_Follow_ToNormalDuringTheTransitionToMuffled_StartsFromTheCurrentValue_AndEndsAtNormal()
        {
            Time.captureFramerate = FramesPerSecond;
            yield return Interrupt(StateAudioTimeScale.FollowTimeScale, StateAudioTimeScale.FollowTimeScale, AudioMixerUpdateMode.UnscaledTime);
        }

        [UnityTest]
        public IEnumerator GEN018_C5_Ignore_ToNormalDuringTheTransitionToMuffled_StartsFromTheCurrentValue_AndEndsAtNormal()
        {
            yield return Interrupt(StateAudioTimeScale.IgnoreTimeScale, StateAudioTimeScale.IgnoreTimeScale, AudioMixerUpdateMode.Normal);
        }

        [UnityTest]
        public IEnumerator GEN018_C5_IgnoreThenFollow_ToNormalDuringTheTransitionToMuffled_StartsFromTheCurrentValue_AndEndsAtNormal()
        {
            yield return Interrupt(StateAudioTimeScale.IgnoreTimeScale, StateAudioTimeScale.FollowTimeScale, AudioMixerUpdateMode.Normal);
        }

        [UnityTest]
        public IEnumerator GEN018_C5_FollowThenIgnore_ToNormalDuringTheTransitionToMuffled_StartsFromTheCurrentValue_AndEndsAtNormal()
        {
            yield return Interrupt(StateAudioTimeScale.FollowTimeScale, StateAudioTimeScale.IgnoreTimeScale, AudioMixerUpdateMode.UnscaledTime);
        }

        /// <summary>
        /// A to Muffled over 0.5 s, then B to Normal over 0.5 s once A's transition is past -8 dB. Each frame the
        /// parameter may move by what 20 dB in 0.5 s gives for the frame's own delta time, plus 1 dB: a jump to
        /// Normal (8 dB) or back to Muffled (12 dB) is more.
        /// </summary>
        IEnumerator Interrupt(StateAudioTimeScale first, StateAudioTimeScale second, AudioMixerUpdateMode mode)
        {
            _mixer.updateMode = mode;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Enter, _muffled, 0.5f, first),
                ToSnapshot(_b, StateMoment.Enter, _normal, 0.5f, second));
            yield return null;

            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            int frames = 0;
            while (Volume() > -8f && frames < 10 * FramesPerSecond)
            {
                yield return null;
                frames++;
            }

            float before = Volume();
            Assert.That(before, Is.LessThan(NormalDb - 5f).And.GreaterThan(MuffledDb + 5f), "Given: on the way to Muffled");

            host.Switch(_b);
            var trace = new StringBuilder($" before:{before:F2}");
            float last = before;
            float lowest = before;
            float tooBig = 0f;
            frames = 0;
            start = Time.realtimeSinceStartup;
            while (Mathf.Abs(Volume() - NormalDb) > Tolerance && frames < 10 * FramesPerSecond && Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                frames++;
                float volume = Volume();
                trace.Append($" {frames}:{volume:F2}");
                float delta = second == StateAudioTimeScale.FollowTimeScale ? Time.deltaTime : Time.unscaledDeltaTime;
                float step = Mathf.Abs(volume - last);
                if (step > 1f + 40f * delta)
                    tooBig = Mathf.Max(tooBig, step);
                lowest = Mathf.Min(lowest, volume);
                last = volume;
            }

            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), $"it ends at Normal. Trace:{trace}");
            Assert.That(tooBig, Is.EqualTo(0f), $"no jump: from the value it had when the second transition started. Trace:{trace}");
            Assert.That(lowest, Is.GreaterThanOrEqualTo(before - 1f), $"it does not go on towards Muffled first. Trace:{trace}");
            Assert.That(frames, Is.GreaterThan(3), $"a transition, not a jump to Normal. Trace:{trace}");

            float after = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - after < 0.6f)
                yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "it stays at Normal: the first transition does not come back");
            Assert.That(_mixer.updateMode, Is.EqualTo(mode), "the mixer's Update Mode is as it was");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C6: Seconds 0

        [UnityTest]
        public IEnumerator GEN018_C6_Seconds0_Ignore_NormalMixer_TimeScale0_AtMuffledOnTheNextFrame()
        {
            yield return Seconds0(StateAudioTimeScale.IgnoreTimeScale, AudioMixerUpdateMode.Normal);
        }

        [UnityTest]
        public IEnumerator GEN018_C6_Seconds0_Ignore_UnscaledTimeMixer_TimeScale0_AtMuffledOnTheNextFrame()
        {
            yield return Seconds0(StateAudioTimeScale.IgnoreTimeScale, AudioMixerUpdateMode.UnscaledTime);
        }

        [UnityTest]
        public IEnumerator GEN018_C6_Seconds0_Follow_NormalMixer_TimeScale0_AtMuffledOnTheNextFrame()
        {
            yield return Seconds0(StateAudioTimeScale.FollowTimeScale, AudioMixerUpdateMode.Normal);
        }

        [UnityTest]
        public IEnumerator GEN018_C6_Seconds0_Follow_UnscaledTimeMixer_TimeScale0_AtMuffledOnTheNextFrame()
        {
            yield return Seconds0(StateAudioTimeScale.FollowTimeScale, AudioMixerUpdateMode.UnscaledTime);
        }

        IEnumerator Seconds0(StateAudioTimeScale timeScale, AudioMixerUpdateMode mode)
        {
            _mixer.updateMode = mode;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0f, timeScale));
            yield return null;
            Time.timeScale = 0f;

            host.Switch(_a);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "on the frame after the Switch");
            Assert.That(_mixer.updateMode, Is.EqualTo(mode), "the mixer's Update Mode is as it was");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GEN018_C6_Seconds0_DuringAFollowTransitionThatStandsStill_AtNormalOnTheNextFrame_AndStaysThere()
        {
            _mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            Time.captureFramerate = FramesPerSecond;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host,
                ToSnapshot(_a, StateMoment.Enter, _muffled, 0.5f, StateAudioTimeScale.IgnoreTimeScale),
                ToSnapshot(_b, StateMoment.Enter, _normal, 0f, StateAudioTimeScale.FollowTimeScale));
            yield return null;
            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.2f)
                yield return null;
            Assert.That(Volume(), Is.LessThan(NormalDb - 1f).And.GreaterThan(MuffledDb + 1f), "Given: on the way to Muffled");
            Time.timeScale = 0f;

            host.Switch(_b);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "Normal on the next frame");
            start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.6f)
                yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "and it stays at Normal");
            Assert.That(_mixer.updateMode, Is.EqualTo(AudioMixerUpdateMode.UnscaledTime), "the mixer's Update Mode is as it was");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C7: disabled or destroyed during a transition

        [UnityTest]
        public IEnumerator GEN018_C7_Follow_TimeScale0_DisableTheComponent_AtMuffledOnTheNextFrame()
        {
            yield return EndsWhenGone(StateAudioTimeScale.FollowTimeScale, binding => binding.enabled = false);
        }

        [UnityTest]
        public IEnumerator GEN018_C7_Follow_TimeScale0_DestroyTheComponent_AtMuffledOnTheNextFrame()
        {
            yield return EndsWhenGone(StateAudioTimeScale.FollowTimeScale, binding => Object.Destroy(binding));
        }

        [UnityTest]
        public IEnumerator GEN018_C7_Ignore_DestroyItsGameObject_AtMuffledOnTheNextFrame()
        {
            yield return EndsWhenGone(StateAudioTimeScale.IgnoreTimeScale, binding => Object.Destroy(binding.gameObject));
        }

        [UnityTest]
        public IEnumerator GEN018_C7_Ignore_DeactivateItsGameObject_AtMuffledOnTheNextFrame()
        {
            yield return EndsWhenGone(StateAudioTimeScale.IgnoreTimeScale, binding => binding.gameObject.SetActive(false));
        }

        IEnumerator EndsWhenGone(StateAudioTimeScale timeScale, System.Action<StateAudioBinding> gone)
        {
            _mixer.updateMode = AudioMixerUpdateMode.Normal;
            StateMachineHost host = CreateHost();
            StateAudioBinding binding = CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 2f, timeScale));
            yield return null;
            Time.timeScale = timeScale == StateAudioTimeScale.FollowTimeScale ? 0f : 1f;
            host.Switch(_a);
            yield return null;
            yield return null;
            Assert.That(Volume(), Is.GreaterThan(MuffledDb + 1f), "Given: the transition is under way, not at Muffled yet");

            gone(binding);
            yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "it ends at once at Muffled, not stuck half way");
            float start = Time.realtimeSinceStartup;
            Time.timeScale = 1f;
            while (Time.realtimeSinceStartup - start < 0.3f)
                yield return null;
            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "and stays there");
            Assert.That(_mixer.updateMode, Is.EqualTo(AudioMixerUpdateMode.Normal), "the mixer's Update Mode is as it was");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GEN018_C7_DisablingABindingWhoseTransitionWasReplacedByAnotherBinding_LeavesTheNewTransitionAlone()
        {
            _mixer.updateMode = AudioMixerUpdateMode.Normal;
            Time.captureFramerate = FramesPerSecond;
            StateMachineHost host = CreateHost();
            StateAudioBinding first = CreateBinding("First", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0.5f, StateAudioTimeScale.FollowTimeScale));
            CreateBinding("Second", host, ToSnapshot(_b, StateMoment.Enter, _normal, 0.5f, StateAudioTimeScale.FollowTimeScale));
            yield return null;
            host.Switch(_a);
            for (int i = 0; i < FramesPerSecond / 4; i++)
                yield return null;
            host.Switch(_b);
            yield return null;
            float before = Volume();
            Assert.That(before, Is.LessThan(NormalDb - 1f).And.GreaterThan(MuffledDb + 1f), "Given: on the way back to Normal");

            first.enabled = false;
            yield return null;

            Assert.That(Volume(), Is.GreaterThanOrEqualTo(before - Tolerance).And.LessThan(NormalDb - Tolerance),
                "the transition of the second binding goes on: no jump to Muffled, no jump to Normal");
            for (int i = 0; i < FramesPerSecond / 2; i++)
                yield return null;
            Assert.That(Volume(), Is.EqualTo(NormalDb).Within(Tolerance), "and it ends at Normal");
            Assert.That(_mixer.updateMode, Is.EqualTo(AudioMixerUpdateMode.Normal), "the mixer's Update Mode is as it was");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- rules

        [UnityTest]
        public IEnumerator GEN018_Rule_AfterATransition_TheMixersUpdateModeIsAsItWas([Values] AudioMixerUpdateMode mode, [Values] StateAudioTimeScale timeScale)
        {
            _mixer.updateMode = mode;
            StateMachineHost host = CreateHost();
            CreateBinding("Mixer binding", host, ToSnapshot(_a, StateMoment.Enter, _muffled, 0.1f, timeScale));
            yield return null;

            host.Switch(_a);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.3f)
                yield return null;

            Assert.That(Volume(), Is.EqualTo(MuffledDb).Within(Tolerance), "at Muffled");
            Assert.That(_mixer.updateMode, Is.EqualTo(mode), "the Update Mode set on the mixer asset is back");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Extra real time to allow when the Editor stalled for a frame (seen once: a 0.53 s frame at the end of a
        /// transition). Lower bounds are not loosened.
        /// </summary>
        static float Hitch(float longestFrame) => longestFrame > 0.1f ? longestFrame : 0f;

        float Volume()
        {
            Assert.That(_mixer.GetFloat(Parameter, out float value), Is.True, $"exposed parameter {Parameter}");
            return value;
        }

        static StateAudioBinding.Entry ToSnapshot(StateId state, StateMoment moment, AudioMixerSnapshot snapshot, float seconds, StateAudioTimeScale timeScale)
        {
            return new StateAudioBinding.Entry
            {
                State = state, Moment = moment, Action = StateAudioAction.TransitionToSnapshot, Snapshot = snapshot, Seconds = seconds, TimeScale = timeScale,
            };
        }

        StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            _created.Add(id);
            return id;
        }

        StateMachineHost CreateHost()
        {
            var gameObject = new GameObject("Host");
            _created.Add(gameObject);
            return gameObject.AddComponent<StateMachineHost>();
        }

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
