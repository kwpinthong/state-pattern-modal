namespace ALittleBB.StateMachines
{
    /// <summary>What a binding of a <see cref="StateAudioBinding"/> does to the background music.</summary>
    public enum StateAudioAction
    {
        /// <summary>
        /// Plays the binding's Clip on the BGM Audio Source, looped. When that clip is already playing, it goes on
        /// from where it is instead of starting again. With Seconds &gt; 0 it crossfades from the music that plays.
        /// </summary>
        PlayBgm = 0,

        /// <summary>Stops the music of the BGM Audio Source, fading it out first when Seconds &gt; 0.</summary>
        StopBgm = 1,

        /// <summary>Moves the Audio Mixer to the binding's Snapshot over the binding's Seconds.</summary>
        TransitionToSnapshot = 2,
    }
}
