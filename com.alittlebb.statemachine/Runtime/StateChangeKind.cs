namespace ALittleBB.StateMachines
{
    /// <summary>Which transition produced a <see cref="StateChanged"/> signal.</summary>
    public enum StateChangeKind
    {
        /// <summary>Every state on the stack was exited and only the new state is left.</summary>
        Switch = 0,

        /// <summary>A state was put on top; the state underneath was not exited (it is paused).</summary>
        Push = 1,

        /// <summary>The top state was exited; the state underneath is current again without a new Enter (it is resumed).</summary>
        Pop = 2,
    }
}
