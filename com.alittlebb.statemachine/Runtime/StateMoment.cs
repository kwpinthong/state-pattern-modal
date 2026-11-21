namespace ALittleBB.StateMachines
{
    /// <summary>
    /// A moment in the life of one state on the stack, as seen by a listener of that single state
    /// (the four callbacks of <see cref="StateMachine.Subscribe"/>).
    /// </summary>
    public enum StateMoment
    {
        /// <summary>The state became the current state, and it was not on the stack before.</summary>
        Enter = 0,

        /// <summary>The state left the stack, whether it was the current state or covered by another.</summary>
        Exit = 1,

        /// <summary>Another state was pushed on top of the state.</summary>
        Paused = 2,

        /// <summary>The state on top was popped and the state is the current state again.</summary>
        Resumed = 3,
    }
}
