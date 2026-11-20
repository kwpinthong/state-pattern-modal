using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Identifies one state of a <see cref="StateMachine"/>. Create one asset per state;
    /// two ids are the same state only when they are the same asset.
    /// </summary>
    [CreateAssetMenu(fileName = "StateId", menuName = "ALittleBB/State Machine/State Id")]
    public class StateId : ScriptableObject
    {
    }
}
