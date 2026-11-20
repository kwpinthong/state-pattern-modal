namespace ALittleBB.StateMachines
{
    /// <summary>What a binding of a <see cref="StateAnimatorBinding"/> does to the Animator.</summary>
    public enum StateAnimatorAction
    {
        /// <summary>Sets a Trigger parameter.</summary>
        SetTrigger = 0,

        /// <summary>Sets a Bool parameter to the binding's Bool Value.</summary>
        SetBool = 1,

        /// <summary>Sets an Int parameter to the binding's Int Value.</summary>
        SetInteger = 2,

        /// <summary>Cross fades to a state of the Animator Controller over the binding's Duration, in seconds.</summary>
        CrossFade = 3,
    }
}
