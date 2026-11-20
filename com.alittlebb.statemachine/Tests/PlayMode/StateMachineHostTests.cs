using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ALittleBB.StateMachines.Tests
{
    public class StateMachineHostTests
    {
        /// <summary>
        /// Host (Initial State = A), two listeners for A, and three buttons wired to the host in On Click.
        /// See <see cref="HostTestSceneRefs"/>. The scene is not in Build Settings; the Editor loads it by path.
        /// </summary>
        const string TestScenePath =
            "Packages/com.alittlebb.statemachine/Tests/PlayMode/Scenes/StateMachineHostTestScene.unity";

        readonly List<Object> _created = new List<Object>();
        StateId _a;
        StateId _b;
        Scene _testScene;

        [SetUp]
        public void SetUp()
        {
            _a = CreateStateId("A");
            _b = CreateStateId("B");
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

            // Do not leave the test scene's host, listeners and buttons behind for the next test.
            if (_testScene.IsValid() && _testScene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene($"StateMachineHostTests empty {Time.frameCount}");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(_testScene);
            }

            _testScene = default;
        }

        // ---------------------------------------------------------------- C1

        [UnityTest]
        public IEnumerator C1_Host_WithInitialState_IsInThatState_AfterStart()
        {
            StateMachineHost host = CreateHost(initialState: _a);

            yield return null;

            Assert.That(host.Machine.Current, Is.SameAs(_a));
            Assert.That(host.Machine.Stack, Is.EqualTo(new[] { _a }));
        }

        [UnityTest]
        public IEnumerator C1_Listeners_ThatSubscribeBeforeTheHostStarts_GetOnEnter_Once()
        {
            GameObject root = CreateInactiveObject("Host and listeners");
            HostTestListener startsFirst = root.AddComponent<HostTestListener>();
            StateMachineHost host = AddHost(root, initialState: _a);
            HostTestListener inAwake = root.AddComponent<HostTestListener>();
            HostTestListener startsLast = root.AddComponent<HostTestListener>();
            Listen(startsFirst, host, _a);
            Listen(inAwake, host, _a);
            inAwake.subscribeInAwake = true;
            Listen(startsLast, host, _a);

            root.SetActive(true);
            yield return null;

            Assert.That(host.Machine.Current, Is.SameAs(_a));
            AssertEnteredOnce(startsFirst, "listener whose Start is added before the host's");
            AssertEnteredOnce(inAwake, "listener that subscribes in Awake");
            AssertEnteredOnce(startsLast, "listener whose Start is added after the host's");
        }

        [UnityTest]
        public IEnumerator C1_Listener_ThatSubscribesAfterTheHostStarted_GetsOnEnter_Once()
        {
            StateMachineHost host = CreateHost(initialState: _a);
            yield return null;

            GameObject late = CreateInactiveObject("Late listener");
            HostTestListener listener = late.AddComponent<HostTestListener>();
            Listen(listener, host, _a);
            late.SetActive(true);
            yield return null;

            Assert.That(listener.Signals, Is.EqualTo(new[] { HostTestListener.OnEnter }));
            Assert.That(listener.Subscription.Status, Is.EqualTo(StateListenerStatus.Active));
        }

        [UnityTest]
        public IEnumerator C1_Host_InALoadedScene_WithInitialStateSetInTheInspector_IsInThatState_AfterStart()
        {
            yield return LoadTestScene();
            HostTestSceneRefs scene = FindSceneRefs();

            Assert.That(scene.host.InitialState, Is.SameAs(scene.stateA), "Given: Initial State = A in the scene");
            Assert.That(scene.host.Machine.Current, Is.SameAs(scene.stateA));
            foreach (HostTestListener listener in scene.listeners)
                AssertEnteredOnce(listener, listener.name);
        }

        [UnityTest]
        public IEnumerator InitialState_IsNotEntered_WhenAStateWasAlreadyEntered_BeforeStart()
        {
            StateMachineHost host = CreateHost(initialState: _a);
            host.Switch(_b);

            yield return null;

            Assert.That(host.Machine.Stack, Is.EqualTo(new[] { _b }));
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_Host_WithoutInitialState_HasNoCurrentState_SendsNoSignal_AndLogsNothing()
        {
            GameObject root = CreateInactiveObject("Host without initial state");
            StateMachineHost host = AddHost(root, initialState: null);
            HostTestListener listener = root.AddComponent<HostTestListener>();
            Listen(listener, host, _a, _b);
            int changed = 0;
            host.Machine.Changed += _ => changed++;

            root.SetActive(true);
            yield return null;
            yield return null;

            Assert.That(host.Machine.Current, Is.Null);
            Assert.That(host.Machine.Stack, Is.Empty);
            Assert.That(listener.Signals, Is.Empty);
            Assert.That(changed, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_Button_WithHostSwitchInOnClick_WhenClicked_MakesThatStateCurrent()
        {
            yield return LoadTestScene();
            HostTestSceneRefs scene = FindSceneRefs();
            AssertOnClickCalls(scene.switchToB, scene.host, nameof(StateMachineHost.Switch));
            Assert.That(scene.host.Machine.Current, Is.SameAs(scene.stateA), "Given: the host starts in A");

            Click(scene.switchToB);

            Assert.That(scene.host.Machine.Current, Is.SameAs(scene.stateB));
            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateB }));
        }

        [UnityTest]
        public IEnumerator C3_Buttons_WithHostPushAndPopInOnClick_WhenClicked_PushAndPopTheStack()
        {
            yield return LoadTestScene();
            HostTestSceneRefs scene = FindSceneRefs();
            AssertOnClickCalls(scene.pushB, scene.host, nameof(StateMachineHost.Push));
            AssertOnClickCalls(scene.pop, scene.host, nameof(StateMachineHost.Pop));
            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateA }), "Given: the host starts in A");

            Click(scene.pushB);

            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateA, scene.stateB }));

            Click(scene.pop);

            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateA }));
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_SceneReloadedTwice_LogsNoError_AndListenersOfTheOldScenes_GetNoSignal()
        {
            var oldListeners = new List<HostTestListener>();
            var signalsBeforeTheirSceneWasUnloaded = new List<string[]>();

            yield return LoadTestScene();
            HostTestSceneRefs scene = FindSceneRefs();
            AssertSceneStartedInA(scene, "first load");

            for (int reload = 1; reload <= 2; reload++)
            {
                foreach (HostTestListener listener in scene.listeners)
                {
                    oldListeners.Add(listener);
                    signalsBeforeTheirSceneWasUnloaded.Add(listener.Signals.ToArray());
                }

                StateMachineHost oldHost = scene.host;

                yield return LoadTestScene();
                scene = FindSceneRefs();

                Assert.That(oldHost == null, Is.True, $"reload {reload}: the old host was destroyed with its scene");
                AssertSceneStartedInA(scene, $"reload {reload}");
            }

            // The new host works, and its own listeners hear it...
            Click(scene.switchToB);
            scene.host.Switch(scene.stateA);
            Click(scene.pushB);
            Click(scene.pop);

            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateA }));
            foreach (HostTestListener listener in scene.listeners)
            {
                Assert.That(listener.Signals, Does.Contain(HostTestListener.OnExit), $"{listener.name} of the current scene");
                Assert.That(listener.Signals, Does.Contain(HostTestListener.OnResumed), $"{listener.name} of the current scene");
            }

            // ...but nothing reached the listeners of the two scenes that were unloaded, not even an OnExit on the way out.
            Assert.That(oldListeners, Has.Count.EqualTo(4));
            for (int i = 0; i < oldListeners.Count; i++)
            {
                Assert.That(oldListeners[i].Signals, Is.EqualTo(signalsBeforeTheirSceneWasUnloaded[i]),
                    $"old listener {i} ({(oldListeners[i].unsubscribeOnDestroy ? "unsubscribes in OnDestroy" : "never unsubscribes")}) got a signal after its scene was unloaded");
            }

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

        /// <summary>A host on its own active object; its Start runs on the next frame.</summary>
        StateMachineHost CreateHost(StateId initialState)
        {
            GameObject root = CreateInactiveObject("Host");
            StateMachineHost host = AddHost(root, initialState);
            root.SetActive(true);
            return host;
        }

        static StateMachineHost AddHost(GameObject root, StateId initialState)
        {
            StateMachineHost host = root.AddComponent<StateMachineHost>();
            host.InitialState = initialState;
            return host;
        }

        static void Listen(HostTestListener listener, StateMachineHost host, params StateId[] states)
        {
            listener.host = host;
            listener.states = states;
        }

        static void AssertEnteredOnce(HostTestListener listener, string who)
        {
            Assert.That(listener.Signals.Count(signal => signal == HostTestListener.OnEnter), Is.EqualTo(1),
                $"OnEnter count of the {who}. Signals: [{string.Join(", ", listener.Signals)}]");
            Assert.That(listener.Subscription.Status, Is.EqualTo(StateListenerStatus.Active), $"status of the {who}");
        }

        static void AssertSceneStartedInA(HostTestSceneRefs scene, string when)
        {
            Assert.That(scene.host.Machine.Stack, Is.EqualTo(new[] { scene.stateA }), $"{when}: stack of the host");
            foreach (HostTestListener listener in scene.listeners)
                AssertEnteredOnce(listener, $"{listener.name} ({when})");
        }

        /// <summary>The button calls the host from a persistent On Click entry, as set up in the Inspector.</summary>
        static void AssertOnClickCalls(Button button, StateMachineHost host, string method)
        {
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), $"On Click entries of '{button.name}'");
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(host), $"On Click target of '{button.name}'");
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(method), $"On Click method of '{button.name}'");
        }

        /// <summary>A left click delivered to the button the way the EventSystem delivers it.</summary>
        static void Click(Button button)
        {
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, click, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>
        /// Loads the test scene as the only scene (a second call is a reload: the first one's objects are destroyed),
        /// then waits one frame so that every Start in it has run.
        /// </summary>
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

        HostTestSceneRefs FindSceneRefs()
        {
            foreach (GameObject root in _testScene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out HostTestSceneRefs refs))
                    return refs;
            }

            Assert.Fail($"No {nameof(HostTestSceneRefs)} on a root object of '{TestScenePath}'.");
            return null;
        }
    }
}
