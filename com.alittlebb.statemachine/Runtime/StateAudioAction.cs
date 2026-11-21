namespace ALittleBB.StateMachines
{
    /// <summary>What a binding of a <see cref="StateAudioBinding"/> does to the background music.</summary>
    public enum StateAudioAction
    {
        /// <summary>
        /// Plays the binding's Clip on the BGM Audio Source, looped. When that clip is already playing, it goes on
        /// from where it is instead of starting again.
        /// </summary>
        PlayBgm = 0,

        /// <summary>Stops the music of the BGM Audio Source.</summary>
        StopBgm = 1,

        /// <summary>
        /// Moves the Audio Mixer to the binding's Snapshot over the binding's Seconds
        /// (<see cref="UnityEngine.Audio.AudioMixerSnapshot.TransitionTo"/>). The mixer's own Update Mode sets the
        /// clock: Normal follows Time.timeScale (it stands still at 0), Unscaled Time does not. 0 changes it on the
        /// next frame. It does not need the BGM Audio Source.
        /// </summary>
        TransitionToSnapshot = 2,
    }
}
