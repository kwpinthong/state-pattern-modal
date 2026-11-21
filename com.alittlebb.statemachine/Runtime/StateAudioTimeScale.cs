namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Which clock a <see cref="StateAudioAction.TransitionToSnapshot"/> binding of a <see cref="StateAudioBinding"/>
    /// runs on, whatever the Update Mode of the Audio Mixer.
    /// </summary>
    public enum StateAudioTimeScale
    {
        /// <summary>
        /// Real time, like the BGM crossfades: Time.timeScale does not slow the transition, and it goes on while
        /// Time.timeScale is 0. The default.
        /// </summary>
        IgnoreTimeScale = 0,

        /// <summary>
        /// Game time: Time.timeScale slows the transition (0.5 makes it last twice as long in real time), and it
        /// stands still while Time.timeScale is 0.
        /// </summary>
        FollowTimeScale = 1,
    }
}
