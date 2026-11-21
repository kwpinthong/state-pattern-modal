using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ALittleBB.StateMachines.Tests
{
    public class StateListenerTests
    {
        /// <summary>
        /// Host (no Initial State) and a panel: a StateListener for A set up in the Inspector that hides its own
        /// GameObject in On Exit and shows it in On Enter. See <see cref="StateListenerTestSceneRefs"/>.
        /// The scene is not in Build Settings; the Editor loads it by path.
        /// </summary>
        const string TestScenePath =
            "Packages/com.alittlebb.statemachine/Tests/PlayMode/Scenes/StateListenerTestScene.unity";

        const string OnEnter = StateListenerTestRecorder.OnEnter;
        const string OnExit = StateListenerTestRecorder.OnExit;
        const string OnPaused = StateListenerTestRecorder.OnPaused;
        const string OnResumed = StateListenerTestRecorder.OnResumed;

        /// <summary>Host left empty in the Inspector.</summary>
        const StateMachineHost NoHost = null;

        readonly List<Object> _created = new List<Object>();
        StateId _a;
        StateId _b;
        StateId _x;
        Scene _testScene;

        [SetUp]
        public void SetUp()
        {
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _x = CreateStateId("X");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.Destroy(created);
            }

            _created.Clear();

            // Do not leave the test scene's host and panel behind for the next test.
            if (_testScene.IsValid() && _testScene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene($"StateListenerTests empty {Time.frameCount}");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(_testScene);
            }

            _testScene = default;
        }

        // ---------------------------------------------------------------- C1

        [UnityTest]
        public IEnumerator C1_Listener_ForA_WhenSwitchToA_InvokesOnEnter_Once()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {A}", host, signals, _a);
            yield return null;
            Assert.That(signals, Is.Empty, "Given: no state yet");

            host.Switch(_a);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }));
        }

        [UnityTest]
        public IEnumerator C1_Listener_SetUpInTheInspector_WhenSwitchToA_InvokesOnEnter_Once()
        {
            yield return LoadTestScene();
            StateListenerTestSceneRefs scene = FindSceneRefs();
            Assert.That(scene.panel.Host, Is.SameAs(scene.host), "Given: Host set in the Inspector");
            Assert.That(scene.panel.States, Is.EqualTo(new[] { scene.stateA }), "Given: States = {A} in the Inspector");
            AssertPersistentCall(scene.panel.OnEnter, scene.panelRecorder, nameof(StateListenerTestRecorder.RecordEnter));
            Assert.That(scene.panelRecorder.Signals, Is.Empty, "Given: no state yet");

            scene.host.Switch(scene.stateA);

            Assert.That(scene.panelRecorder.Signals, Is.EqualTo(new[] { OnEnter }));
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_WhenSwitchToB_InvokesOnExit()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {A}", host, signals, _a);
            yield return null;
            host.Switch(_a);
            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "Given: Current = A");

            host.Switch(_b);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnExit }));
        }

        [UnityTest]
        public IEnumerator C2_WhenPushX_InvokesOnPaused_AndWhenPop_InvokesOnResumed()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {A}", host, signals, _a);
            yield return null;
            host.Switch(_a);
            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "Given: Current = A");

            host.Push(_x);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnPaused }), "after Push(X): paused, not exited");

            host.Pop();

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnPaused, OnResumed }), "after Pop(): resumed, not entered again");
        }

        [UnityTest]
        public IEnumerator C2_WhenSwitchToB_WhileAIsPaused_InvokesOnExit_WithoutOnResumed()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {A}", host, signals, _a);
            yield return null;
            host.Switch(_a);
            host.Push(_x);
            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnPaused }), "Given: A is paused under X");

            host.Switch(_b);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnPaused, OnExit }));
        }

        [UnityTest]
        public IEnumerator C2_Listener_ForAAndB_WhenSwitchingBetweenThem_InvokesNothing()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {A, B}", host, signals, _a, _b);
            yield return null;
            host.Switch(_a);
            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "Given: Current = A");

            host.Switch(_b);
            host.Switch(_a);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "still inside the set");

            host.Switch(_x);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnExit }), "left the set");
        }

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_PanelThatHidesItselfInOnExit_WhenAThenBThenA_GetsOnEnterAgain_AndIsActiveAgain()
        {
            yield return LoadTestScene();
            StateListenerTestSceneRefs scene = FindSceneRefs();
            GameObject panel = scene.panel.gameObject;
            AssertPersistentCall(scene.panel.OnExit, panel, nameof(GameObject.SetActive));
            AssertPersistentCall(scene.panel.OnEnter, panel, nameof(GameObject.SetActive));

            scene.host.Switch(scene.stateA);
            yield return null;
            Assert.That(scene.panelRecorder.Signals, Is.EqualTo(new[] { OnEnter }), "Given: Current = A");
            Assert.That(panel.activeSelf, Is.True, "Given: the panel is shown in A");

            scene.host.Switch(scene.stateB);
            yield return null;
            Assert.That(scene.panelRecorder.Signals, Is.EqualTo(new[] { OnEnter, OnExit }), "A → B");
            Assert.That(panel.activeSelf, Is.False, "A → B: On Exit hid the panel's own GameObject");

            scene.host.Switch(scene.stateA);

            Assert.That(scene.panelRecorder.Signals, Is.EqualTo(new[] { OnEnter, OnExit, OnEnter }),
                "B → A: the hidden panel still gets On Enter");
            Assert.That(panel.activeSelf, Is.True, "B → A: the panel is active again");
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_PrefabInstantiated_WhileCurrentIsA_GetsOnEnter_InAwake()
        {
            yield return LoadTestScene();
            StateListenerTestSceneRefs scene = FindSceneRefs();
            StateListener inPrefab = scene.panelPrefab.GetComponent<StateListener>();
            Assert.That(inPrefab.States, Is.EqualTo(new[] { scene.stateA }), "Given: the prefab listens to {A}");
            Assert.That(inPrefab.Host == null, Is.True, "Given: a prefab cannot point at a host in the scene");
            scene.host.Switch(scene.stateA);
            yield return null;
            Assert.That(scene.host.Machine.Current, Is.SameAs(scene.stateA), "Given: Current = A");

            GameObject instance = Object.Instantiate(scene.panelPrefab, scene.host.transform);

            // No frame has passed: Awake ran inside Instantiate.
            Assert.That(instance.GetComponent<StateListenerTestRecorder>().Signals, Is.EqualTo(new[] { OnEnter }));
        }

        [UnityTest]
        public IEnumerator C4_PrefabInstantiated_WhileAIsPausedUnderB_GetsOnEnter_ThenOnPaused_InAwake()
        {
            yield return LoadTestScene();
            StateListenerTestSceneRefs scene = FindSceneRefs();
            scene.host.Switch(scene.stateA);
            scene.host.Push(scene.stateB);
            yield return null;
            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateA, scene.stateB }), "Given: A is covered by B");

            GameObject instance = Object.Instantiate(scene.panelPrefab, scene.host.transform);
            List<string> signals = instance.GetComponent<StateListenerTestRecorder>().Signals;

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnPaused }));

            scene.host.Pop();

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnPaused, OnResumed }));
        }

        // ---------------------------------------------------------------- C5

        [UnityTest]
        public IEnumerator C5_DestroyedListener_WhenSwitch_ThrowsNoMissingReferenceException_AndInvokesNothing()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            StateListener listener = CreateListener("Destroyed listener", host, signals, _a);
            TouchItsGameObjectInEveryEvent(listener);
            yield return null;
            host.Switch(_a);
            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "Given: Current = A");

            Object.Destroy(listener.gameObject);
            yield return null;
            Assert.That(listener == null, Is.True, "Given: the listener is destroyed");

            Assert.DoesNotThrow(() => host.Switch(_b), "Switch(B)");
            Assert.DoesNotThrow(() => host.Switch(_a), "Switch(A)");
            Assert.DoesNotThrow(() => host.Push(_x), "Push(X)");
            Assert.DoesNotThrow(() => host.Pop(), "Pop()");

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "no event after the listener was destroyed");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C5_ListenerDestroyedWhileItsGameObjectIsInactive_WhenSwitch_ThrowsNoMissingReferenceException()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            StateListener listener = CreateListener("Hidden then destroyed listener", host, signals, _a);
            TouchItsGameObjectInEveryEvent(listener);
            yield return null;
            host.Switch(_a);
            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "Given: Current = A");

            listener.gameObject.SetActive(false);
            yield return null;
            Object.Destroy(listener.gameObject);
            yield return null;
            Assert.That(listener == null, Is.True, "Given: the listener is destroyed");

            Assert.DoesNotThrow(() => host.Switch(_b), "Switch(B)");
            Assert.DoesNotThrow(() => host.Switch(_a), "Switch(A)");

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "no event after the listener was destroyed");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C6

        [UnityTest]
        public IEnumerator C6_Listener_WithNoHost_LogsOneError_AndDoesNotThrow()
        {
            StateMachineHost unrelatedHost = CreateHost("Host the listener does not know");
            GameObject root = CreateInactiveObject("No host listener");
            var signals = new List<string>();
            AddListener(root, NoHost, signals, _a);

            // Exactly one: a second error would be an unexpected log, which fails the test.
            LogAssert.Expect(LogType.Error, new Regex("^StateListener on 'No host listener': Host is not set"));

            root.SetActive(true);
            yield return null;
            yield return null;

            // Not once per frame, not once per activation, and transitions elsewhere do not reach it.
            root.SetActive(false);
            root.SetActive(true);
            unrelatedHost.Switch(_a);
            unrelatedHost.Switch(_b);
            yield return null;

            Assert.That(signals, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C7

        [UnityTest]
        public IEnumerator C7_Listener_WithNoStates_WhenAnyTransition_InvokesNothing()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {}", host, signals);
            yield return null;

            host.Switch(_a);
            host.Push(_x);
            host.Pop();
            host.Switch(_b);

            Assert.That(host.Machine.Current, Is.SameAs(_b), "the transitions did happen");
            Assert.That(signals, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- decided in GEN-007

        [UnityTest]
        public IEnumerator Listener_OnAGameObjectThatStartsInactive_GetsNothing_UntilItIsFirstActivated_ThenGetsOnEnter()
        {
            StateMachineHost host = CreateHost();
            GameObject startsInactive = CreateInactiveObject("Starts inactive");
            var signals = new List<string>();
            AddListener(startsInactive, host, signals, _a);
            yield return null;

            host.Switch(_a);
            yield return null;

            Assert.That(signals, Is.Empty, "Awake has not run yet: the listener is not subscribed");

            startsInactive.SetActive(true);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }), "first activation: Awake subscribes and syncs with Current = A");

            host.Switch(_b);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnExit }));
        }

        [UnityTest]
        public IEnumerator Listener_WhoseComponentIsDisabled_StillGetsSignals()
        {
            StateMachineHost host = CreateHost();
            GameObject root = CreateInactiveObject("Disabled listener");
            var signals = new List<string>();
            StateListener listener = AddListener(root, host, signals, _a);
            listener.enabled = false;
            root.SetActive(true);
            yield return null;

            host.Switch(_a);
            host.Switch(_b);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnExit }));
        }

        [UnityTest]
        public IEnumerator Listener_WithNoHostSet_UsesTheHostInItsParents()
        {
            StateMachineHost host = CreateHost();
            GameObject child = CreateInactiveObject("Child listener");
            child.transform.SetParent(host.transform, false);
            var signals = new List<string>();
            AddListener(child, NoHost, signals, _a);
            child.SetActive(true);
            yield return null;

            host.Switch(_a);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Listener_WithHostSet_UsesIt_NotTheHostInItsParents()
        {
            StateMachineHost parentHost = CreateHost("Parent host");
            StateMachineHost chosenHost = CreateHost("Chosen host");
            GameObject child = CreateInactiveObject("Child listener");
            child.transform.SetParent(parentHost.transform, false);
            var signals = new List<string>();
            AddListener(child, chosenHost, signals, _a);
            child.SetActive(true);
            yield return null;

            parentHost.Switch(_a);

            Assert.That(signals, Is.Empty, "the host in the parents is not the one that was set");

            chosenHost.Switch(_a);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter }));
        }

        [UnityTest]
        public IEnumerator Listener_WithAnEmptySlotInStates_IgnoresTheSlot()
        {
            StateMachineHost host = CreateHost();
            var signals = new List<string>();
            CreateListener("Listener {none, A}", host, signals, null, _a);
            yield return null;

            host.Switch(_a);
            host.Switch(_b);

            Assert.That(signals, Is.EqualTo(new[] { OnEnter, OnExit }));
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- helpers

        StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            _created.Add(id);
            return id;
        }

        GameObject CreateInactiveObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _created.Add(gameObject);
            return gameObject;
        }

        /// <summary>A host without Initial State on its own active object: the test decides the state.</summary>
        StateMachineHost CreateHost(string name = "Host")
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject.AddComponent<StateMachineHost>();
        }

        /// <summary>A listener on its own object, set up before its Awake runs. Awake has run when this returns.</summary>
        StateListener CreateListener(string name, StateMachineHost host, List<string> signals, params StateId[] states)
        {
            GameObject root = CreateInactiveObject(name);
            StateListener listener = AddListener(root, host, signals, states);
            root.SetActive(true);
            return listener;
        }

        /// <summary>
        /// Adds a listener to an object that is still inactive, so Host and States are in place before Awake,
        /// and writes every event it invokes into <paramref name="signals"/>.
        /// </summary>
        static StateListener AddListener(GameObject inactive, StateMachineHost host, List<string> signals, params StateId[] states)
        {
            Assert.That(inactive.activeInHierarchy, Is.False, "the listener must be set up before its Awake runs");

            StateListener listener = inactive.AddComponent<StateListener>();
            listener.Host = host;
            listener.States = states;
            listener.OnEnter.AddListener(() => signals.Add(OnEnter));
            listener.OnExit.AddListener(() => signals.Add(OnExit));
            listener.OnPaused.AddListener(() => signals.Add(OnPaused));
            listener.OnResumed.AddListener(() => signals.Add(OnResumed));
            return listener;
        }

        /// <summary>
        /// Handlers that throw MissingReferenceException when they run after the GameObject was destroyed,
        /// like a script that shows or hides its panel from these events.
        /// </summary>
        static void TouchItsGameObjectInEveryEvent(StateListener listener)
        {
            GameObject gameObject = listener.gameObject;
            listener.OnEnter.AddListener(() => gameObject.SetActive(gameObject.activeSelf));
            listener.OnExit.AddListener(() => gameObject.SetActive(gameObject.activeSelf));
            listener.OnPaused.AddListener(() => gameObject.SetActive(gameObject.activeSelf));
            listener.OnResumed.AddListener(() => gameObject.SetActive(gameObject.activeSelf));
        }

        /// <summary>The event calls the target from a persistent entry, as set up in the Inspector.</summary>
        static void AssertPersistentCall(UnityEvent unityEvent, Object target, string method)
        {
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == method)
                    return;
            }

            Assert.Fail($"Given: no persistent entry calls {method} on '{target.name}' ({target.GetType().Name}).");
        }

        /// <summary>Loads the test scene as the only scene, then waits one frame so that every Start in it has run.</summary>
        IEnumerator LoadTestScene()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneInPlayMode(TestScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _testScene = SceneManager.GetActiveScene();
            Assert.That(_testScene.path, Is.EqualTo(TestScenePath), "the test scene is the active scene");
            yield return null;
#else
            Assert.Ignore("Needs the Editor: the test scene is not in Build Settings and is loaded by its path.");
            yield break;
#endif
        }

        StateListenerTestSceneRefs FindSceneRefs()
        {
            foreach (GameObject root in _testScene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out StateListenerTestSceneRefs refs))
                    return refs;
            }

            Assert.Fail($"No {nameof(StateListenerTestSceneRefs)} on a root object of '{TestScenePath}'.");
            return null;
        }
    }
}
