namespace ALittleBB.StateMachines
{
    /// <summary>Where a listener stands, given the set of <see cref="StateId"/> it subscribed with.</summary>
    public enum StateListenerStatus
    {
        /// <summary>No state of the set is on the stack.</summary>
        Inactive = 0,

        /// <summary>The top of the stack is in the set.</summary>
        Active = 1,

        /// <summary>A state of the set is on the stack, but the top is not in the set.</summary>
        Paused = 2,
    }
}
