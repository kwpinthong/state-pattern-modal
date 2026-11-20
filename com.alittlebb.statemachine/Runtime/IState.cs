namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Logic of one state, bound to a <see cref="StateId"/> with <see cref="StateMachine.Register"/>.
    /// A <see cref="StateId"/> does not need one.
    /// </summary>
    public interface IState
    {
        void Enter();
        void Exit();
    }
}
