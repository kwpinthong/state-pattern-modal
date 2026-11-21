using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Runs the Transition To Snapshot bindings of <see cref="StateAudioBinding"/> on the clock of their
    /// <see cref="StateAudioTimeScale"/>, whatever the Update Mode set on the Audio Mixer.
    /// <para>The mixer's own transition is used (<see cref="AudioMixerSnapshot.TransitionTo"/>): it starts from the
    /// values the mixer has, so a transition during a transition, or after one started by other code, never jumps.
    /// Its clock is the mixer's Update Mode, so while the transition runs the mixer is set to Unscaled Time (Ignore
    /// Time Scale) or Normal (Follow Time Scale), and the Update Mode it had is put back when the transition ends.
    /// The other way, blending the snapshots with AudioMixer.TransitionToSnapshots each frame, needs to know where
    /// the mixer starts from, which Unity does not tell.</para>
    /// <para>A transition belongs to the binding that started it, which advances it from its Update and ends it
    /// at once on its snapshot when it is disabled or destroyed. A new transition of the same mixer, from any
    /// binding, takes it over.</para>
    /// </summary>
    static class StateAudioSnapshotTransitions
    {
        sealed class Running
        {
            public AudioMixerUpdateMode UpdateModeBefore;
            public StateAudioBinding Owner;
            public AudioMixerSnapshot Snapshot;
            public StateAudioTimeScale TimeScale;
            public float Seconds;
            public float Elapsed;
        }

        static readonly Dictionary<AudioMixer, Running> s_running = new Dictionary<AudioMixer, Running>();
        static readonly List<AudioMixer> s_ending = new List<AudioMixer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => s_running.Clear(); // with Enter Play Mode Options that keep the domain

        /// <summary>Moves the mixer of <paramref name="snapshot"/> to it over <paramref name="seconds"/> of the clock of <paramref name="timeScale"/>.</summary>
        internal static void Start(StateAudioBinding owner, AudioMixerSnapshot snapshot, float seconds, StateAudioTimeScale timeScale)
        {
            AudioMixer mixer = snapshot.audioMixer;
            s_running.TryGetValue(mixer, out Running running);

            if (seconds <= 0f)
            {
                // Lands on the next frame in either Update Mode, even while Time.timeScale is 0.
                snapshot.TransitionTo(0f);
                if (running != null)
                {
                    s_running.Remove(mixer);
                    mixer.updateMode = running.UpdateModeBefore;
                }

                return;
            }

            if (running == null)
            {
                running = new Running { UpdateModeBefore = mixer.updateMode };
                s_running.Add(mixer, running);
            }

            running.Owner = owner;
            running.Snapshot = snapshot;
            running.TimeScale = timeScale;
            running.Seconds = seconds;
            running.Elapsed = 0f;
            mixer.updateMode = timeScale == StateAudioTimeScale.FollowTimeScale ? AudioMixerUpdateMode.Normal : AudioMixerUpdateMode.UnscaledTime;
            snapshot.TransitionTo(seconds);
        }

        /// <summary>Advances the transitions of <paramref name="owner"/> by one frame and ends those that are done.</summary>
        internal static void Update(StateAudioBinding owner)
        {
            if (s_running.Count == 0)
                return;

            foreach (KeyValuePair<AudioMixer, Running> pair in s_running)
            {
                Running running = pair.Value;
                if (running.Owner != owner)
                    continue;

                running.Elapsed += running.TimeScale == StateAudioTimeScale.FollowTimeScale ? Time.deltaTime : Time.unscaledDeltaTime;
                if (running.Elapsed >= running.Seconds)
                    s_ending.Add(pair.Key);
            }

            EndAll();
        }

        /// <summary>Ends the transitions of <paramref name="owner"/> at once on their snapshots.</summary>
        internal static void Finish(StateAudioBinding owner)
        {
            if (s_running.Count == 0)
                return;

            foreach (KeyValuePair<AudioMixer, Running> pair in s_running)
            {
                if (pair.Value.Owner == owner)
                    s_ending.Add(pair.Key);
            }

            EndAll();
        }

        static void EndAll()
        {
            for (int i = 0; i < s_ending.Count; i++)
            {
                AudioMixer mixer = s_ending[i];
                Running running = s_running[mixer];
                s_running.Remove(mixer);
                if (mixer == null)
                    continue; // the mixer was destroyed: nothing to end

                running.Snapshot.TransitionTo(0f);
                mixer.updateMode = running.UpdateModeBefore;
            }

            s_ending.Clear();
        }
    }
}
