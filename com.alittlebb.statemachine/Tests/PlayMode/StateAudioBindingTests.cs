using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-014: StateAudioBinding plays and stops the BGM with no fade. The music is checked on the AudioSource
    /// (clip, loop, isPlaying, timeSamples, volume) with clips made in code. On this machine Unity has an audio
    /// device: isPlaying is true as soon as Play() returns, time goes on with real time, a timeSamples set by hand
    /// stays, and Play() on a clip that is playing starts it again at 0 — which is what the C3 tests look for.
    /// Each test has an AudioListener: without one Unity logs "There are no audio listeners in the scene".
    /// </summary>
    public class StateAudioBindingTests
    {
        const int SampleRate = 44100;

        /// <summary>A position in the clips, far from the start, set by hand to see whether the music restarted.</summary>
        const int Midway = 30000;

        /// <summary>Host left empty in the Inspector.</summary>
        const StateMachineHost NoHost = null;

        /// <summary>Audio Source left empty in the Inspector.</summary>
        const AudioSource NoSource = null;

        readonly List<Object> _created = new List<Object>();
        StateId _a;
        StateId _b;
        StateId _x;
        AudioClip _clipA;
        AudioClip _clipB;

        [SetUp]
        public void SetUp()
        {
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _x = CreateStateId("X");
            _clipA = CreateClip("Clip A");
            _clipB = CreateClip("Clip B");

            // Unity logs "There are no audio listeners in the scene" when a source plays without one.
            var listener = new GameObject("Audio Listener", typeof(AudioListener));
            _created.Add(listener);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.Destroy(created);
            }

            _created.Clear();
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_EnterA_PlayBgmClipA0_WhenSwitchToA_TheSourcePlaysClipA_Looped_AsSoonAsTheSwitchReturns()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, _clipA));
            yield return null;
            Assert.That(music.isPlaying, Is.False, "Given: no music yet");

            host.Switch(_a);

            Assert.That(music.clip, Is.SameAs(_clipA), "the BGM source has clip A");
            Assert.That(music.loop, Is.True, "looped");
            Assert.That(music.isPlaying, Is.True, "and plays it when Switch returns");

            yield return null;
            yield return null;

            Assert.That(music.isPlaying, Is.True, "still playing a few frames later");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C2_SourceSetToNotLoop_PlayBgm_LoopsTheMusicAnyway()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, _clipA));
            music.loop = false;
            yield return null;

            host.Switch(_a);

            Assert.That(music.loop, Is.True);
            Assert.That(music.isPlaying, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_ClipAPlaying_WhenAnotherStatePlaysClipA_TheMusicGoesOnFromWhereItWas()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Play(_b, StateMoment.Enter, _clipA));
            yield return null;
            host.Switch(_a);
            yield return SeekMidway(music);
            Assert.That(music.isPlaying, Is.True, "Given: clip A plays");
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(Midway), "Given: clip A is midway");

            host.Switch(_b);

            Assert.That(music.clip, Is.SameAs(_clipA));
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(Midway), "clip A was not started again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_ClipAPlaying_WhenAnotherStatePlaysClipB_ClipBStartsFromTheBeginning()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Play(_b, StateMoment.Enter, _clipB));
            yield return null;
            host.Switch(_a);
            yield return SeekMidway(music);

            host.Switch(_b);

            Assert.That(music.clip, Is.SameAs(_clipB));
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.LessThan(Midway), "clip B starts from its beginning");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_ClipAStopped_WhenPlayBgmClipA_ItPlaysAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Stop(_b, StateMoment.Enter));
            yield return null;
            host.Switch(_a);
            host.Switch(_b);
            Assert.That(music.isPlaying, Is.False, "Given: clip A was stopped");

            host.Switch(_a);

            Assert.That(music.clip, Is.SameAs(_clipA));
            Assert.That(music.isPlaying, Is.True, "the same clip, stopped, plays again");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_EnterA_PlayBgm_StackAX_WhenPop_TheEnterBindingOfADoesNotRunAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Stop(_x, StateMoment.Enter));
            yield return null;
            host.Switch(_a);
            host.Push(_x);
            Assert.That(music.isPlaying, Is.False, "Given: Stack = [A, X] and the Enter binding of X stopped the music");

            host.Pop();

            Assert.That(music.isPlaying, Is.False, "Pop back to A is Resumed, not Enter: PlayBgm of A did not run again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_EnterA_PlayBgm_StackAX_WhenPop_TheMusicGoesOnWithoutStartingAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, _clipA));
            yield return null;
            host.Switch(_a);
            host.Push(_x);
            yield return SeekMidway(music);
            Assert.That(music.isPlaying, Is.True, "Given: Stack = [A, X] and clip A plays");

            host.Pop();

            Assert.That(music.clip, Is.SameAs(_clipA));
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(Midway), "clip A was not started again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C4_ResumedA_PlayBgm_WhenPop_RunsOnPop()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Resumed, _clipA),
                Play(_x, StateMoment.Enter, _clipB));
            yield return null;
            host.Switch(_a);
            host.Push(_x);
            Assert.That(music.clip, Is.SameAs(_clipB), "Given: X plays clip B");

            host.Pop();

            Assert.That(music.clip, Is.SameAs(_clipA), "the Resumed binding of A is the one that runs on Pop");
            Assert.That(music.isPlaying, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C5

        [UnityTest]
        public IEnumerator C5_CurrentIsAlreadyA_WhenTheComponentStarts_TheEnterBindingOfARunsAtOnce()
        {
            StateMachineHost host = CreateHost();
            host.Switch(_a);
            Assert.That(host.Machine.Current, Is.SameAs(_a), "Given: Current = A");

            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, _clipA));

            Assert.That(music.clip, Is.SameAs(_clipA), "as soon as the component is enabled");
            Assert.That(music.isPlaying, Is.True);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_HostWithInitialStateA_AndTheBindingCreatedWithIt_TheMusicOfAPlaysFromTheFirstFrame()
        {
            GameObject hostObject = CreateInactiveObject("Host");
            StateMachineHost host = hostObject.AddComponent<StateMachineHost>();
            host.InitialState = _a;
            GameObject musicObject = CreateInactiveObject("Music");
            AudioSource music = musicObject.AddComponent<AudioSource>();
            AddBinding(musicObject, host, music, Play(_a, StateMoment.Enter, _clipA));

            hostObject.SetActive(true);
            musicObject.SetActive(true);
            yield return null;

            Assert.That(host.Machine.Current, Is.SameAs(_a), "Given: the host entered its Initial State");
            Assert.That(music.clip, Is.SameAs(_clipA));
            Assert.That(music.isPlaying, Is.True, "after the first frame");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_StackAX_WhenTheComponentStarts_ItPlaysTheMusicOfTheTopState_WhateverTheOrderOfTheBindings()
        {
            StateMachineHost host = CreateHost();
            host.Switch(_a);
            host.Push(_x);

            // X is listed first: X's Enter must still not be covered by A's Enter.
            AudioSource music = CreateMusic("Music", host,
                Play(_x, StateMoment.Enter, _clipB),
                Play(_a, StateMoment.Enter, _clipA));

            Assert.That(music.clip, Is.SameAs(_clipB), "Stack = [A, X]: the music of X, on top");
            Assert.That(music.isPlaying, Is.True);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_StackAX_WhenTheComponentStarts_PausedOfAComesBeforeEnterOfX()
        {
            StateMachineHost host = CreateHost();
            host.Switch(_a);
            host.Push(_x);

            AudioSource music = CreateMusic("Music", host,
                Play(_x, StateMoment.Enter, _clipB),
                Stop(_a, StateMoment.Paused));

            Assert.That(music.clip, Is.SameAs(_clipB));
            Assert.That(music.isPlaying, Is.True, "as on a real Push: A was paused before X was entered");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_DisabledThenEnabledAgain_InTheSameState_TheMusicGoesOnWithoutStartingAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, _clipA));
            yield return null;
            host.Switch(_a);
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();
            yield return SeekMidway(music);

            binding.enabled = false;
            binding.enabled = true;

            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(Midway), "Enter of A is applied again, and clip A is already playing");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_DisabledWhileTheStateChanges_WhenEnabledAgain_ItPlaysTheMusicOfTheCurrentState()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Play(_b, StateMoment.Enter, _clipB));
            yield return null;
            host.Switch(_a);
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();

            binding.enabled = false;
            host.Switch(_b);
            Assert.That(music.clip, Is.SameAs(_clipA), "Given: nothing is applied while it is disabled");
            binding.enabled = true;

            Assert.That(music.clip, Is.SameAs(_clipB));
            Assert.That(music.isPlaying, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C6

        [UnityTest]
        public IEnumerator C6_MusicPlaying_WhenStopBgm0_ItStops_AndTheNextPlayBgmIsAtTheVolumeSetOnTheSource()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Stop(_b, StateMoment.Enter),
                Play(_x, StateMoment.Enter, _clipB));
            music.volume = 0.6f;
            yield return null;
            host.Switch(_a);
            Assert.That(music.isPlaying, Is.True, "Given: the music plays");

            host.Switch(_b);

            Assert.That(music.isPlaying, Is.False, "StopBgm(0) stopped it when Switch returned");

            host.Switch(_x);

            Assert.That(music.clip, Is.SameAs(_clipB));
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.volume, Is.EqualTo(0.6f), "the volume set on the Audio Source");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C6_NothingPlaying_StopBgm_ThrowsNothing_AndLogsNothing()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Stop(_a, StateMoment.Enter));
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_a));

            Assert.That(music.isPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C7

        [UnityTest]
        public IEnumerator C7_NoClip_And_NoSnapshot_WarnOncePerBinding_NamingTheGameObject_AndTheOtherBindingsStillWork()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, null),
                Snapshot(_a, StateMoment.Enter),
                Play(_a, StateMoment.Enter, _clipA));
            yield return null;
            LogAssert.NoUnexpectedReceived(); // Given: nothing is logged before the signal comes

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Music': Bindings\[0\] \(A, Enter, PlayBgm\): Clip is not set"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Music': Bindings\[1\] \(A, Enter, TransitionToSnapshot\): Snapshot is not set"));

            Assert.DoesNotThrow(() => host.Switch(_a), "the signal comes");

            Assert.That(music.clip, Is.SameAs(_clipA), "the binding after the two broken ones still works");
            Assert.That(music.isPlaying, Is.True);

            // The same signal again: the two warnings above are the only ones.
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_NoAudioSource_PlayAndStopBindings_WarnOnceEach_NamingTheGameObject_AndNothingThrows()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("No source");
            AddBinding(gameObject, host, NoSource,
                Play(_a, StateMoment.Enter, _clipA),
                Stop(_a, StateMoment.Exit));
            gameObject.SetActive(true);
            yield return null;
            LogAssert.NoUnexpectedReceived(); // Given: nothing is logged before the signal comes

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'No source': Bindings\[0\] \(A, Enter, PlayBgm\): Audio Source is not set"));
            Assert.DoesNotThrow(() => host.Switch(_a));
            LogAssert.NoUnexpectedReceived(); // the Exit binding has not been needed yet

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'No source': Bindings\[1\] \(A, Exit, StopBgm\): Audio Source is not set"));
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_AudioSourceDestroyed_WarnsOncePerBinding_AndNothingThrows()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, _clipA));
            yield return null;

            Object.Destroy(music);
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Music': Bindings\[0\] \(A, Enter, PlayBgm\): Audio Source is not set"));
            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_NoHost_WarnsOnce_NamingTheGameObject_AndNothingThrows()
        {
            StateMachineHost unrelatedHost = CreateHost("Host the binding does not know");
            GameObject gameObject = CreateInactiveObject("No host");
            AudioSource music = gameObject.AddComponent<AudioSource>();
            AddBinding(gameObject, NoHost, music, Play(_a, StateMoment.Enter, _clipA));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'No host': Host is not set and there is no StateMachineHost on this GameObject or its parents"));

            gameObject.SetActive(true);
            yield return null;

            Assert.DoesNotThrow(() => unrelatedHost.Switch(_a));
            gameObject.SetActive(false);
            gameObject.SetActive(true);
            yield return null;

            Assert.That(music.isPlaying, Is.False, "it does not follow a host it was not given");
            LogAssert.NoUnexpectedReceived(); // one warning only, not one per activation
        }

        [UnityTest]
        public IEnumerator C7_BrokenBinding_StillWarnsOnlyOnce_WhenItsComponentIsDisabledAndEnabledAgain()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host, Play(_a, StateMoment.Enter, null));
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAudioBinding on 'Music': Bindings\[0\] \(A, Enter, PlayBgm\): Clip is not set"));
            host.Switch(_a);

            // Enter A is applied again each time it is enabled (C5): the broken binding must not warn again.
            StateAudioBinding binding = music.GetComponent<StateAudioBinding>();
            binding.enabled = false;
            binding.enabled = true;
            binding.enabled = false;
            binding.enabled = true;

            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- other rules

        [UnityTest]
        public IEnumerator Rule_HostLeftEmpty_UsesTheHostOnAParent_AndLogsNothing()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("Music under the host");
            gameObject.transform.SetParent(host.transform, false);
            AudioSource music = gameObject.AddComponent<AudioSource>();
            AddBinding(gameObject, NoHost, music, Play(_a, StateMoment.Enter, _clipA));
            gameObject.SetActive(true);
            yield return null;

            host.Switch(_a);

            Assert.That(music.isPlaying, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_SlotsWithoutAState_AreIgnored_AndLogNothing()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                null,
                Play(null, StateMoment.Enter, null),
                Play(_a, StateMoment.Enter, _clipA));
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_a));

            Assert.That(music.isPlaying, Is.True, "the binding after the empty slots still works");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_SeveralBindingsOnTheSameMoment_AreAppliedInListOrder()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Play(_a, StateMoment.Enter, _clipB));
            yield return null;

            host.Switch(_a);

            Assert.That(music.clip, Is.SameAs(_clipB), "the last one in the list wins");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_AfterItsGameObjectIsDestroyed_ASignalThrowsNothing_AndLogsNothing()
        {
            StateMachineHost host = CreateHost();
            AudioSource music = CreateMusic("Music", host,
                Play(_a, StateMoment.Enter, _clipA),
                Stop(_a, StateMoment.Exit));
            yield return null;
            host.Switch(_a);

            Object.Destroy(music.gameObject);
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Waits until the music has really started, then moves it to <see cref="Midway"/>. A Play() in the frame
        /// the music was started, before the audio thread picked it up, does not take it back to 0.
        /// </summary>
        static IEnumerator SeekMidway(AudioSource music)
        {
            for (int frame = 0; frame < 120 && music.timeSamples == 0; frame++)
                yield return null;

            Assert.That(music.timeSamples, Is.GreaterThan(0), "Given: the music has started");
            music.timeSamples = Midway;
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

        /// <summary>A TransitionToSnapshot binding with no snapshot set.</summary>
        static StateAudioBinding.Entry Snapshot(StateId state, StateMoment moment)
        {
            return new StateAudioBinding.Entry
            {
                State = state, Moment = moment, Action = StateAudioAction.TransitionToSnapshot, Seconds = 0f,
            };
        }

        StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            _created.Add(id);
            return id;
        }

        /// <summary>A 2 second silent-ish clip made in code.</summary>
        AudioClip CreateClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, SampleRate * 2, 1, SampleRate, false);
            var data = new float[SampleRate * 2];
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

        /// <summary>A host without Initial State on its own active object: the test decides the state.</summary>
        StateMachineHost CreateHost(string name = "Host")
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject.AddComponent<StateMachineHost>();
        }

        /// <summary>
        /// An Audio Source with a binding next to it that uses it, on its own object, set up before the object is
        /// first activated. It is active when this returns.
        /// </summary>
        AudioSource CreateMusic(string name, StateMachineHost host, params StateAudioBinding.Entry[] bindings)
        {
            GameObject gameObject = CreateInactiveObject(name);
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            AddBinding(gameObject, host, source, bindings);
            gameObject.SetActive(true);
            return source;
        }

        /// <summary>Adds a binding to an object that is still inactive, so its fields are in place before it is enabled.</summary>
        static StateAudioBinding AddBinding(GameObject inactive, StateMachineHost host, AudioSource source, params StateAudioBinding.Entry[] bindings)
        {
            Assert.That(inactive.activeInHierarchy, Is.False, "the binding must be set up before it is enabled");

            StateAudioBinding binding = inactive.AddComponent<StateAudioBinding>();
            binding.Host = host;
            binding.AudioSource = source;
            binding.Bindings = bindings;
            return binding;
        }
    }
}
