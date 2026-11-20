using UnityEngine;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Sits on a root object of <c>Scenes/StateAnimatorBindingTestScene.unity</c> and hands the scene's objects to
    /// the tests. In that scene the host has no Initial State, and <see cref="door"/> is on a GameObject with an
    /// Animator that plays <c>Animators/StateAnimatorBindingTest.controller</c>. Its Host and its Bindings are set
    /// in the Inspector:
    /// <list type="number">
    /// <item><description>stateA, Enter, Set Trigger "Open"</description></item>
    /// <item><description>stateA, Enter, Set Bool "IsOpen" = true</description></item>
    /// <item><description>stateA, Exit, Set Bool "IsOpen" = false</description></item>
    /// <item><description>stateA, Paused, Set Integer "Mode" = 7</description></item>
    /// <item><description>stateB, Enter, Cross Fade "Idle" over 0.2 s</description></item>
    /// </list>
    /// </summary>
    [AddComponentMenu("")] // test only: keep it out of the Add Component menu
    public class StateAnimatorBindingTestSceneRefs : MonoBehaviour
    {
        public StateMachineHost host;
        public StateId stateA;
        public StateId stateB;

        [Tooltip("The binding under test, on the same GameObject as doorAnimator.")]
        public StateAnimatorBinding door;

        public Animator doorAnimator;
    }
}
