using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-015: Play BGM and Stop BGM with Seconds > 0 fade the music in real time. Play BGM crossfades: the old
    /// clip fades out while the new one fades in on a second Audio Source copied from the one that is set. The
    /// music is checked on the Audio Sources of the music's GameObject (clip, isPlaying, volume, timeSamples), with
    /// clips made in code. Fades are short and waited for by polling real time with a timeout, never by a fixed
    /// sleep; "during the fade" is one sample taken a little after the fade started.
    /// </summary>
    public class StateAudioBindingCrossfadeTests
    {
        const int SampleRate = 44100;

        /// <summary>The volume set on the Audio Source: the volume the music must end at. Not 1, so a fade that ends at 1 is seen.</summary>
        const float V = 0.6f;

        /// <summary>Fade of the tests, in seconds.</summary>
        const float Fade = 0.5f;

        /// <summary>When the "during the fade" sample is taken, in real seconds after the fade started.</summary>
        const float MidSample = 0.1f;

        /// <summary>Longest wait for a fade to end, in real seconds after it started.</summary>
        const float Timeout = Fade + 1.5f;

        /// <summary>A position in the clips, far from the start, set by hand to see whether the music restarted.</summary>
        const int Midway = 30000;

        const float Tolerance = 1e-4f;

        readonly List<Object> _created = new List<Object>();
        StateId _a;
        StateId _b;
        StateId _c;
        StateId _s;
        StateId _a0;
        StateId _n;
        AudioClip _clipA;
        AudioClip _clipB;
        AudioClip _clipC;

        [SetUp]
        public void SetUp()
        {
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _c = CreateStateId("C");
            _s = CreateStateId("S");
            _a0 = CreateStateId("A0");
            _n = CreateStateId("N");
            _clipA = CreateClip("Clip A");
            _clipB = CreateClip("Clip B");
            _clipC = CreateClip("Clip C");

            // Unity logs "There are no audio listeners in the scene" when a source plays without one.
            _created.Add(new GameObject("Audio Listener", typeof(AudioListener)));
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.Destroy(created);
            }

            _created.Clear();
        }

        // ---------------------------------------------------------------- C1

        [UnityTest]
        public IEnumerator C1_ClipAPlayingAtV_WhenPlayBgmClipBWithFade_BothPlayDuringTheFade_AFallsBRises_ThenOnlyClipBAtV_AndClipAStopped()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);

            host.Switch(_b);
            float start = Time.realtimeSinceStartup;

            yield return WaitReal(start, MidSample);
            AudioSource a = PlayingWith(music, _clipA);
            AudioSource b = PlayingWith(music, _clipB);
            Assert.That(a, Is.Not.Null, "during the fade clip A still plays");
            Assert.That(b, Is.Not.Null, "during the fade clip B already plays");
            Assert.That(a, Is.Not.SameAs(b), "on two Audio Sources");
            Assert.That(a.volume, Is.LessThan(V).And.GreaterThan(0f), "A fades out");
            Assert.That(b.volume, Is.LessThan(V).And.GreaterThan(0f), "B fades in");
            float aMid = a.volume;
            float bMid = b.volume;

            yield return WaitReal(start, Fade * 0.6f);
            if (a.isPlaying && b.volume < V)
            {
                Assert.That(a.volume, Is.LessThan(aMid), "A keeps falling");
                Assert.That(b.volume, Is.GreaterThan(bMid), "B keeps rising");
            }

            yield return WaitUntilOneClipAtV(music, start);
            Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThanOrEqualTo(Fade * 0.8f), "the fade took about its Seconds");
            yield return ExpectOnly(music, _clipB, "after the fade");
            Assert.That(a.isPlaying, Is.False, "clip A stopped");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C1_AfterACrossfadeToB_PlayBgmClipBAgain_FindsTheSourceThatPlaysIt_AndGoesOnWithoutStartingAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);
            AudioSource b = PlayingWith(music, _clipB);
            yield return SeekMidway(b);

            host.Switch(_n); // N plays nothing
            host.Switch(_b); // PlayBgm(clip B, fade) again

            Assert.That(PlayingWith(music, _clipB), Is.SameAs(b), "clip B goes on on the source that plays it");
            Assert.That(b.timeSamples, Is.GreaterThanOrEqualTo(Midway), "clip B was not started again");
            yield return ExpectOnly(music, _clipB, "nothing else starts");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C1_AfterACrossfadeToB_PlayBgmClipAWithNoFade_ReplacesBAtOnce_AtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);

            host.Switch(_a0); // PlayBgm(clip A, 0)

            Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { _clipA }), "at once, one clip");
            Assert.That(PlayingWith(music, _clipA).volume, Is.EqualTo(V).Within(Tolerance));
            yield return ExpectOnly(music, _clipA, "a moment later");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_CrossfadeBinding_WhenSwitch_SwitchReturnsAndStateChangedIsSent_InTheSameFrame_BeforeTheFadeEnds()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            var signals = new List<StateChanged>();
            host.Machine.Changed += signal => signals.Add(signal);
            int frame = Time.frameCount;

            host.Switch(_b);

            Assert.That(Time.frameCount, Is.EqualTo(frame), "same frame");
            Assert.That(host.Machine.Current, Is.SameAs(_b), "Switch has returned with B current");
            Assert.That(signals.Count, Is.EqualTo(1), "StateChanged was sent");
            Assert.That(signals[0].To, Is.SameAs(_b));
            Assert.That(PlayingWith(music, _clipA), Is.Not.Null, "the fade has not ended: clip A still plays");
            Assert.That(PlayingWith(music, _clipB), Is.Not.Null, "clip B has started");
            Assert.That(PlayingWith(music, _clipB).volume, Is.LessThan(V), "clip B is not at V yet");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_TimeScale0_WhenPlayBgmClipBWithFade_TheFadeRunsToTheEnd_InRealTime()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            Time.timeScale = 0f;

            host.Switch(_b);
            float start = Time.realtimeSinceStartup;

            yield return WaitReal(start, MidSample);
            Assert.That(PlayingWith(music, _clipA), Is.Not.Null, "Given: it fades, it does not cut");
            Assert.That(PlayingWith(music, _clipB).volume, Is.LessThan(V).And.GreaterThan(0f), "clip B rises while Time.timeScale = 0");
            yield return WaitUntilOneClipAtV(music, start);
            yield return ExpectOnly(music, _clipB, "the fade ended in real time");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "Given: time stayed stopped");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_TimeScale0_WhenStopBgmWithFade_TheMusicFadesOutAndStops_InRealTime()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            Time.timeScale = 0f;

            host.Switch(_s);
            float start = Time.realtimeSinceStartup;

            yield return WaitReal(start, MidSample);
            Assert.That(music.isPlaying, Is.True, "Given: it fades, it does not cut");
            Assert.That(music.volume, Is.LessThan(V).And.GreaterThan(0f), "clip A fades out while Time.timeScale = 0");
            yield return WaitUntilSilent(music, start);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_CrossfadeAToBRunning_WhenPlayBgmClipCWithFade_EndsWithOnlyClipC_AtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: A → B is running");

            host.Switch(_c);
            float start = Time.realtimeSinceStartup;

            Assert.That(Playing(music).Count, Is.LessThanOrEqualTo(2), "never more than two clips at once");
            yield return WaitUntilOneClipAtV(music, start);
            yield return ExpectOnly(music, _clipC, "the last clip asked for");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_CrossfadeAToBRunning_WhenPlayBgmClipAAgain_EndsWithOnlyClipA_AtV_GoingOnFromWhereItWas()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            yield return SeekMidway(music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            AudioSource a = PlayingWith(music, _clipA);
            Assert.That(a, Is.Not.Null, "Given: A → B is running");

            host.Switch(_a); // back to clip A, with a fade
            float start = Time.realtimeSinceStartup;

            Assert.That(PlayingWith(music, _clipA), Is.SameAs(a), "clip A goes on on its source");
            Assert.That(a.timeSamples, Is.GreaterThanOrEqualTo(Midway), "clip A was not started again");
            yield return WaitUntilOneClipAtV(music, start);
            yield return ExpectOnly(music, _clipA, "back to clip A");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_CrossfadeAToBRunning_WhenPlayBgmClipCWithNoFade_EndsAtOnceWithOnlyClipC_AtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings(), Play(_c, StateMoment.Exit, _clipC));
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            host.Switch(_c);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: a fade to clip C is running");

            host.Switch(_n); // Exit of C: PlayBgm(clip C, 0); N plays nothing

            Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { _clipC }), "at once, one clip");
            Assert.That(PlayingWith(music, _clipC).volume, Is.EqualTo(V).Within(Tolerance));
            yield return ExpectOnly(music, _clipC, "a moment later");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_ManyPlayBgmInARow_EachFrame_EndsWithOnlyTheLastClip_AtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);

            StateId[] order = { _b, _c, _a, _b, _c, _b, _a, _c };
            foreach (StateId state in order)
            {
                host.Switch(state);
                Assert.That(Playing(music).Count, Is.LessThanOrEqualTo(2), "never more than two clips at once");
                yield return null;
            }

            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);
            yield return ExpectOnly(music, _clipC, "the last clip asked for");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C5

        [UnityTest]
        public IEnumerator C5_MusicPlaying_WhenStopBgmWithFade_ItFadesToSilenceWithinTheFade_ThenStops_AndTheNextPlayBgmIsAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);

            host.Switch(_s);
            float start = Time.realtimeSinceStartup;

            Assert.That(music.isPlaying, Is.True, "it does not stop at once");
            yield return WaitReal(start, MidSample);
            Assert.That(music.isPlaying, Is.True, "during the fade it still plays");
            Assert.That(music.volume, Is.LessThan(V).And.GreaterThan(0f), "more quietly");
            yield return WaitUntilSilent(music, start);
            Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThanOrEqualTo(Fade * 0.8f), "the fade took about its Seconds");

            host.Switch(_a0); // PlayBgm(clip A, 0)

            Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { _clipA }));
            Assert.That(PlayingWith(music, _clipA).volume, Is.EqualTo(V).Within(Tolerance), "the next Play BGM is at V");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_AfterStopBgmWithFade_TheNextPlayBgmWithFade_EndsAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_s);
            yield return WaitUntilSilent(music, Time.realtimeSinceStartup);

            host.Switch(_b);

            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);
            yield return ExpectOnly(music, _clipB, "the next Play BGM ends at V");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_CrossfadeRunning_WhenStopBgmWithFade_BothFadeOut_AndStop()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: A → B is running");

            host.Switch(_s);

            yield return WaitUntilSilent(music, Time.realtimeSinceStartup);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_StopBgmFadeRunning_WhenPlayBgmTheSameClip_ItComesBackToV_WithoutStartingAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            yield return SeekMidway(music);
            host.Switch(_s);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(music.volume, Is.LessThan(V), "Given: clip A is fading out");

            host.Switch(_a);

            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(Midway), "clip A was not started again");
            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);
            yield return ExpectOnly(music, _clipA, "back at V");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C6

        [UnityTest]
        public IEnumerator C6_SourceWithMixerGroupSpatialBlendAndPriority_WhenCrossfade_TheSecondSourceUsesTheSameSettings()
        {
            AudioMixerGroup group = LoadMixerGroup();
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            music.outputAudioMixerGroup = group;
            music.spatialBlend = 0.3f;
            music.priority = 17;
            music.pitch = 1.25f;
            music.panStereo = -0.4f;
            yield return StartClipA(host, music);

            host.Switch(_b);

            AudioSource b = PlayingWith(music, _clipB);
            Assert.That(b, Is.Not.Null.And.Not.SameAs(music), "Given: clip B fades in on the second Audio Source");
            Assert.That(b.gameObject, Is.SameAs(music.gameObject), "next to the Audio Source that is set");
            Assert.That(b.outputAudioMixerGroup, Is.SameAs(group), "the same mixer group");
            Assert.That(b.spatialBlend, Is.EqualTo(0.3f), "spatialBlend");
            Assert.That(b.priority, Is.EqualTo(17), "priority");
            Assert.That(b.pitch, Is.EqualTo(1.25f), "pitch");
            Assert.That(b.panStereo, Is.EqualTo(-0.4f).Within(Tolerance), "panStereo");
            Assert.That(b.loop, Is.True, "looped");
            Assert.That(b.playOnAwake, Is.False, "does not play on its own");
            Assert.That(b.hideFlags & HideFlags.HideInInspector, Is.EqualTo(HideFlags.HideInInspector), "hidden in the Inspector");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C6_SettingsChangedOnTheSourceThatIsSet_AreCopiedAgain_OnTheNextCrossfade()
        {
            AudioMixerGroup group = LoadMixerGroup();
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);
            AudioSource second = PlayingWith(music, _clipB);
            Assert.That(second, Is.Not.SameAs(music), "Given: clip B plays on the second source");

            music.outputAudioMixerGroup = group;
            music.priority = 33;
            host.Switch(_c); // clip C fades in on the source that is set, clip B fades out on the second

            Assert.That(second.outputAudioMixerGroup, Is.SameAs(group));
            Assert.That(second.priority, Is.EqualTo(33));
            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);
            Assert.That(music.GetComponents<AudioSource>().Length, Is.EqualTo(2), "the second source is used again, not added again");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C7

        [UnityTest]
        public IEnumerator C7_Crossfading_WhenTheBindingIsDisabled_TheFadeEndsAtOnce_ClipBAtV_AndKeepsPlaying()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: A → B is running");

            binding.enabled = false;

            Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { _clipB }), "at once, only clip B");
            Assert.That(PlayingWith(music, _clipB).volume, Is.EqualTo(V).Within(Tolerance), "at V");
            yield return HoldOnly(music, _clipB, Fade + 0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_Crossfading_WhenTheBindingComponentIsDestroyed_OnTheSourcesGameObject_ClipBGoesOnAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: A → B is running");

            Object.Destroy(binding);
            yield return null;

            Assert.That(binding == null, Is.True, "Given: the component is gone");
            yield return HoldOnly(music, _clipB, Fade + 0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_Crossfading_WhenTheBindingsGameObjectIsDestroyed_TheSourceBeingElsewhere_ClipBGoesOnAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusicElsewhere(host, out GameObject bindingObject, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: A → B is running");

            Object.Destroy(bindingObject);
            yield return null;

            Assert.That(bindingObject == null, Is.True, "Given: the binding's GameObject is gone");
            yield return HoldOnly(music, _clipB, Fade + 0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_Crossfading_WhenTheBindingsGameObjectIsDeactivated_TheSourceBeingElsewhere_ClipBGoesOnAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusicElsewhere(host, out GameObject bindingObject, Bindings());
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(Playing(music).Count, Is.EqualTo(2), "Given: A → B is running");

            bindingObject.SetActive(false);

            Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { _clipB }), "at once, only clip B");
            yield return HoldOnly(music, _clipB, Fade + 0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_StopBgmFading_WhenTheBindingIsDisabled_ItEndsSilentAtOnce_AndTheSourceIsBackAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();
            yield return StartClipA(host, music);
            host.Switch(_s);
            yield return WaitReal(Time.realtimeSinceStartup, MidSample);
            Assert.That(music.isPlaying, Is.True, "Given: clip A is fading out");

            binding.enabled = false;

            Assert.That(Playing(music), Is.Empty, "the Stop BGM ended at once");
            Assert.That(music.volume, Is.EqualTo(V).Within(Tolerance), "with the volume of the source back at V");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_MusicPlaying_WhenTheBindingIsDisabledThenDestroyed_TheMusicGoesOnAtV()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Bindings());
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();
            yield return StartClipA(host, music);
            host.Switch(_b);
            yield return WaitUntilOneClipAtV(music, Time.realtimeSinceStartup);

            binding.enabled = false;
            yield return HoldOnly(music, _clipB, 0.1f);
            Object.Destroy(binding);
            yield return null;

            yield return HoldOnly(music, _clipB, 0.2f);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// A: Play BGM clip A with a fade; B: clip B with a fade; C: clip C with a fade; S: Stop BGM with a fade;
        /// A0: Play BGM clip A with no fade. Each on Enter.
        /// </summary>
        StateAudioBinding.Entry[] Bindings()
        {
            return new[]
            {
                Play(_a, StateMoment.Enter, _clipA, Fade),
                Play(_b, StateMoment.Enter, _clipB, Fade),
                Play(_c, StateMoment.Enter, _clipC, Fade),
                Stop(_s, StateMoment.Enter, Fade),
                Play(_a0, StateMoment.Enter, _clipA),
            };
        }

        /// <summary>Clip A plays alone at V on the source that is set: Switch(A0), with no fade.</summary>
        IEnumerator StartClipA(StateMachineHost host, AudioSource music)
        {
            music.volume = V;
            host.Switch(_a0);
            yield return null;
            Assert.That(Playing(music), Is.EqualTo(new[] { music }), "Given: clip A plays on the source that is set");
            Assert.That(music.clip, Is.SameAs(_clipA));
            Assert.That(music.volume, Is.EqualTo(V).Within(Tolerance), "Given: at V");
        }

        static List<AudioSource> Playing(AudioSource music)
        {
            return music.GetComponents<AudioSource>().Where(source => source.isPlaying).ToList();
        }

        static AudioSource PlayingWith(AudioSource music, AudioClip clip)
        {
            return Playing(music).SingleOrDefault(source => source.clip == clip);
        }

        static IEnumerator WaitReal(float start, float seconds)
        {
            while (Time.realtimeSinceStartup - start < seconds)
                yield return null;
        }

        /// <summary>Waits until one clip plays alone at V: the fade has ended.</summary>
        static IEnumerator WaitUntilOneClipAtV(AudioSource music, float start)
        {
            yield return null;
            while (!OneClipAtV(music) && Time.realtimeSinceStartup - start < Timeout)
                yield return null;

            Assert.That(Playing(music).Count, Is.EqualTo(1), $"one clip plays within {Timeout} s");
            Assert.That(Playing(music)[0].volume, Is.EqualTo(V).Within(Tolerance), $"at V within {Timeout} s");
        }

        static bool OneClipAtV(AudioSource music)
        {
            List<AudioSource> playing = Playing(music);
            return playing.Count == 1 && Mathf.Abs(playing[0].volume - V) <= Tolerance;
        }

        static IEnumerator WaitUntilSilent(AudioSource music, float start)
        {
            while (Playing(music).Count > 0 && Time.realtimeSinceStartup - start < Timeout)
            {
                foreach (AudioSource source in Playing(music))
                    Assert.That(source.volume, Is.LessThanOrEqualTo(V + Tolerance), "never louder than V");
                yield return null;
            }

            Assert.That(Playing(music), Is.Empty, $"silent within {Timeout} s");
            Assert.That(Time.realtimeSinceStartup - start, Is.LessThanOrEqualTo(Fade + 0.5f), "within about the fade");
            foreach (AudioSource source in music.GetComponents<AudioSource>())
                Assert.That(source.volume, Is.EqualTo(V).Within(Tolerance), "the stopped sources are back at V");
        }

        /// <summary>Only <paramref name="clip"/> plays, at V; and it is still so a few frames later (no fade is left half way).</summary>
        static IEnumerator ExpectOnly(AudioSource music, AudioClip clip, string when)
        {
            for (int frame = 0; frame < 5; frame++)
            {
                Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { clip }), $"{when}: only {clip.name} plays");
                Assert.That(PlayingWith(music, clip).volume, Is.EqualTo(V).Within(Tolerance), $"{when}: at V");
                yield return null;
            }
        }

        /// <summary>Only <paramref name="clip"/> plays at V, for <paramref name="seconds"/> of real time.</summary>
        static IEnumerator HoldOnly(AudioSource music, AudioClip clip, float seconds)
        {
            float start = Time.realtimeSinceStartup;
            do
            {
                Assert.That(Playing(music).Select(source => source.clip), Is.EqualTo(new[] { clip }), $"only {clip.name} plays");
                Assert.That(PlayingWith(music, clip).volume, Is.EqualTo(V).Within(Tolerance), "at V");
                yield return null;
            }
            while (Time.realtimeSinceStartup - start < seconds);
        }

        /// <summary>Waits until the music has really started, then moves it to <see cref="Midway"/>.</summary>
        static IEnumerator SeekMidway(AudioSource source)
        {
            for (int frame = 0; frame < 120 && source.timeSamples == 0; frame++)
                yield return null;

            Assert.That(source.timeSamples, Is.GreaterThan(0), "Given: the music has started");
            source.timeSamples = Midway;
        }

        static AudioMixerGroup LoadMixerGroup()
        {
#if UNITY_EDITOR
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(
                "Packages/com.alittlebb.statemachine/Tests/PlayMode/Audio/StateAudioBindingTest.mixer");
            Assert.That(mixer, Is.Not.Null, "the test mixer");
            return mixer.FindMatchingGroups("Master").Single();
#else
            Assert.Ignore("needs the test mixer asset of the Editor");
            return null;
#endif
        }

        static StateAudioBinding.Entry Play(StateId state, StateMoment moment, AudioClip clip, float fadeSeconds = 0f)
        {
            return new StateAudioBinding.Entry
            {
                State = state, Moment = moment, Action = StateAudioAction.PlayBgm, Clip = clip, Seconds = fadeSeconds,
            };
        }

        static StateAudioBinding.Entry Stop(StateId state, StateMoment moment, float fadeSeconds = 0f)
        {
            return new StateAudioBinding.Entry
            {
                State = state, Moment = moment, Action = StateAudioAction.StopBgm, Seconds = fadeSeconds,
            };
        }

        StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            _created.Add(id);
            return id;
        }

        /// <summary>A 4 second near-silent clip made in code: longer than every fade, so a clip never ends on its own.</summary>
        AudioClip CreateClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, SampleRate * 4, 1, SampleRate, false);
            var data = new float[SampleRate * 4];
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Sin(i * 0.05f) * 0.001f;
            clip.SetData(data, 0);
            _created.Add(clip);
            return clip;
        }

        GameObject CreateInactiveObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _created.Add(gameObject);
            return gameObject;
        }

        StateMachineHost CreateHost()
        {
            var gameObject = new GameObject("Host");
            _created.Add(gameObject);
            return gameObject.AddComponent<StateMachineHost>();
        }

        /// <summary>An Audio Source with the binding on the same GameObject. Active when this returns.</summary>
        AudioSource CreateMusic(string name, StateMachineHost host, StateAudioBinding.Entry[] bindings, params StateAudioBinding.Entry[] more)
        {
            GameObject gameObject = CreateInactiveObject(name);
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            AddBinding(gameObject, host, source, bindings.Concat(more).ToArray());
            gameObject.SetActive(true);
            return source;
        }

        /// <summary>An Audio Source on one GameObject and the binding that uses it on another. Both active when this returns.</summary>
        AudioSource CreateMusicElsewhere(StateMachineHost host, out GameObject bindingObject, StateAudioBinding.Entry[] bindings)
        {
            var sourceObject = new GameObject("Music source");
            _created.Add(sourceObject);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            bindingObject = CreateInactiveObject("Music binding");
            AddBinding(bindingObject, host, source, bindings);
            bindingObject.SetActive(true);
            return source;
        }

        static void AddBinding(GameObject inactive, StateMachineHost host, AudioSource source, StateAudioBinding.Entry[] bindings)
        {
            Assert.That(inactive.activeInHierarchy, Is.False, "the binding must be set up before it is enabled");

            StateAudioBinding binding = inactive.AddComponent<StateAudioBinding>();
            binding.Host = host;
            binding.AudioSource = source;
            binding.Bindings = bindings;
        }
    }
}
