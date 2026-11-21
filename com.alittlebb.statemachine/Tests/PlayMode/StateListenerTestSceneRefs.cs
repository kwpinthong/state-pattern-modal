using UnityEngine;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Sits on a root object of <c>Scenes/StateListenerTestScene.unity</c> and hands the scene's objects to the tests.
    /// In that scene the host has no Initial State, and <see cref="panel"/> is a <see cref="StateListener"/> for
    /// <see cref="stateA"/> with its Host set in the Inspector. Its events are wired with persistent entries:
    /// On Enter → its own GameObject.SetActive(true), On Exit → its own GameObject.SetActive(false), and every event
    /// → the matching Record method of <see cref="panelRecorder"/> (on the same GameObject).
    /// </summary>
    [AddComponentMenu("")] // test only: keep it out of the Add Component menu
    public class StateListenerTestSceneRefs : MonoBehaviour
    {
        public StateMachineHost host;
        public StateId stateA;
        public StateId stateB;

        [Tooltip("Listener for stateA that hides its own GameObject in On Exit and shows it in On Enter.")]
        public StateListener panel;

        public StateListenerTestRecorder panelRecorder;

        [Tooltip("Prefab wired like the panel, but with Host left empty: it finds the host in its parents.")]
        public GameObject panelPrefab;
    }
}
