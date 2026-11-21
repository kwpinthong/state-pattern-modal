namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Signal a <see cref="StateMachine"/> sends to its own listeners after a transition has finished.
    /// </summary>
    public readonly struct StateChanged
    {
        public StateChanged(StateId from, StateId to, StateChangeKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        /// <summary>The state that was current before the transition, or null when there was none.</summary>
        public StateId From { get; }

        /// <summary>The state that is current after the transition.</summary>
        public StateId To { get; }

        public StateChangeKind Kind { get; }
    }
}
