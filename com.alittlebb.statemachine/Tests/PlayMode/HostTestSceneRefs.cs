using UnityEngine;
using UnityEngine.UI;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Sits on a root object of <c>Scenes/StateMachineHostTestScene.unity</c> and hands the scene's objects to the tests.
    /// In that scene the host has Initial State = <see cref="stateA"/> and each button calls the host through a
    /// persistent On Click entry, the way it is wired in the Inspector.
    /// </summary>
    [AddComponentMenu("")] // test only: keep it out of the Add Component menu
    public class HostTestSceneRefs : MonoBehaviour
    {
        public StateMachineHost host;
        public StateId stateA;
        public StateId stateB;

        [Tooltip("On Click: host.Switch(stateB)")]
        public Button switchToB;

        [Tooltip("On Click: host.Push(stateB)")]
        public Button pushB;

        [Tooltip("On Click: host.Pop()")]
        public Button pop;

        [Tooltip("Listeners for stateA: one that never unsubscribes and one that unsubscribes in OnDestroy.")]
        public HostTestListener[] listeners;
    }
}
