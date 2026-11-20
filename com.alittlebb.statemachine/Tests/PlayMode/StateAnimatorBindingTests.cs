using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ALittleBB.StateMachines.Tests
{
    public class StateAnimatorBindingTests
    {
        /// <summary>
        /// Test Animator Controller. Parameters: Trigger "Open", Bool "IsOpen", Bool "Paused", Int "Mode".
        /// Base Layer: "Closed" (default, a 2 second clip) → "Opened" on the trigger "Open" (no exit time,
        /// duration 0), and "Idle" with no transition to it. Layer "Overlay": "Rest" (default) and "Wave".
        /// </summary>
        const string ControllerPath =
            "Packages/com.alittlebb.statemachine/Tests/PlayMode/Animators/StateAnimatorBindingTest.controller";

        /// <summary>
        /// Host (no Initial State) and a door: an Animator with a StateAnimatorBinding whose Host and Bindings are
        /// set in the Inspector. See <see cref="StateAnimatorBindingTestSceneRefs"/>.
        /// The scene is not in Build Settings; the Editor loads it by path.
        /// </summary>
        const string TestScenePath =
            "Packages/com.alittlebb.statemachine/Tests/PlayMode/Scenes/StateAnimatorBindingTestScene.unity";

        // Parameters of the test controller.
        const string Open = "Open";
        const string IsOpen = "IsOpen";
        const string Paused = "Paused";
        const string Mode = "Mode";

        // States of the test controller.
        const string Closed = "Closed";
        const string Opened = "Opened";
        const string Idle = "Idle";
        const string Wave = "Wave";
        const int BaseLayer = 0;
        const int OverlayLayer = 1;

        /// <summary>Host left empty in the Inspector.</summary>
        const StateMachineHost NoHost = null;

        readonly List<Object> _created = new List<Object>();
        RuntimeAnimatorController _controller;
        StateId _a;
        StateId _b;
        StateId _x;
        Scene _testScene;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            Assert.That(_controller, Is.Not.Null, $"Given: the test Animator Controller at '{ControllerPath}'");
#else
            Assert.Ignore("Needs the Editor: the test Animator Controller is loaded by its path.");
#endif
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

            // Do not leave the test scene's host and door behind for the next test.
            if (_testScene.IsValid() && _testScene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene($"StateAnimatorBindingTests empty {Time.frameCount}");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(_testScene);
            }

            _testScene = default;
        }

        // ---------------------------------------------------------------- C1

        [UnityTest]
        public IEnumerator C1_EnterA_SetTriggerOpen_WhenSwitchToA_SetsTheTrigger_AndTheAnimatorIsInTheStateItLeadsTo_AfterItsUpdate()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host, Trigger(_a, StateMoment.Enter, Open));
            yield return null;
            Assert.That(door.GetBool(Open), Is.False, "Given: the trigger is not set");
            AssertIsIn(door, Closed, "Given");

            host.Switch(_a);

            Assert.That(door.GetBool(Open), Is.True, "the Animator got the trigger Open, in the same call");

            yield return null;
            yield return null;

            AssertIsIn(door, Opened, "after the Animator updated");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C1_BindingsSetUpInTheInspector_WhenSwitchToA_SetTheTrigger_AndTheAnimatorIsInTheStateItLeadsTo_AfterItsUpdate()
        {
            yield return LoadTestScene();
            StateAnimatorBindingTestSceneRefs scene = FindSceneRefs();
            Assert.That(scene.door.Host, Is.SameAs(scene.host), "Given: Host set in the Inspector");
            Assert.That(scene.door.gameObject, Is.SameAs(scene.doorAnimator.gameObject), "Given: the binding is on the Animator's GameObject");
            Assert.That(scene.doorAnimator.runtimeAnimatorController, Is.SameAs(_controller), "Given: the Animator plays the test controller");
            Assert.That(Describe(scene.door.Bindings), Is.EqualTo(new[]
            {
                $"{scene.stateA.name} Enter SetTrigger {Open}",
                $"{scene.stateA.name} Enter SetBool {IsOpen} True",
                $"{scene.stateA.name} Exit SetBool {IsOpen} False",
                $"{scene.stateA.name} Paused SetInteger {Mode} 7",
                $"{scene.stateB.name} Enter CrossFade {Idle} 0.2",
            }), "Given: the list of bindings set in the Inspector");
            AssertIsIn(scene.doorAnimator, Closed, "Given");

            scene.host.Switch(scene.stateA);

            Assert.That(scene.doorAnimator.GetBool(Open), Is.True, "the Animator got the trigger Open");
            Assert.That(scene.doorAnimator.GetBool(IsOpen), Is.True, "and the second Enter binding of A");

            yield return null;
            yield return null;

            AssertIsIn(scene.doorAnimator, Opened, "after the Animator updated");

            scene.host.Push(scene.stateB);

            Assert.That(scene.doorAnimator.GetInteger(Mode), Is.EqualTo(7), "Paused binding of A, when B is pushed on top");

            scene.doorAnimator.Update(0.25f);

            AssertIsIn(scene.doorAnimator, Idle, "0.25 s after the Enter binding of B cross faded to Idle over 0.2 s");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_EnterA_SetBoolTrue_And_ExitA_SetBoolFalse_WhenSwitchToA_ThenToB_IsOpenIsTrue_ThenFalse()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                Bool(_a, StateMoment.Exit, IsOpen, false));
            yield return null;
            Assert.That(door.GetBool(IsOpen), Is.False, "Given: no state yet");

            host.Switch(_a);

            Assert.That(door.GetBool(IsOpen), Is.True, "after Switch(A)");

            host.Switch(_b);

            Assert.That(door.GetBool(IsOpen), Is.False, "after Switch(B)");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C2_SetInteger_WhenSwitchToA_ThenToB_SetsTheValueOfEachBinding()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Integer(_a, StateMoment.Enter, Mode, 3),
                Integer(_a, StateMoment.Exit, Mode, -1));
            yield return null;
            Assert.That(door.GetInteger(Mode), Is.EqualTo(0), "Given: no state yet");

            host.Switch(_a);

            Assert.That(door.GetInteger(Mode), Is.EqualTo(3), "after Switch(A)");

            host.Switch(_b);

            Assert.That(door.GetInteger(Mode), Is.EqualTo(-1), "after Switch(B)");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_EnterA_CrossFadeIdle_Over02s_WhenSwitchToA_IsStillFadingAt01s_AndIsInIdleAfter02s()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host, CrossFade(_a, StateMoment.Enter, Idle, 0.2f));
            yield return null;
            AssertIsIn(door, Closed, "Given");

            host.Switch(_a);

            // The test moves the Animator's clock itself, so the times are exact.
            door.Update(0.1f);

            Assert.That(door.IsInTransition(BaseLayer), Is.True, "0.1 s after Switch(A): still fading, it is a cross fade and not a jump");
            Assert.That(door.GetNextAnimatorStateInfo(BaseLayer).IsName(Idle), Is.True, "0.1 s after Switch(A): fading to Idle");

            door.Update(0.15f);

            // Closed plays a 2 second clip: a duration of 0.2 in normalized time would take 0.4 s.
            AssertIsIn(door, Idle, "0.25 s after Switch(A): the duration is 0.2 seconds");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_EnterA_CrossFadeIdle_Over02s_WhenSwitchToA_AfterWaitingALittleOver02s_TheAnimatorIsInIdle()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host, CrossFade(_a, StateMoment.Enter, Idle, 0.2f));
            yield return null;
            AssertIsIn(door, Closed, "Given");

            host.Switch(_a);

            // Game time, the clock the Animator follows. One more frame: the Animator updates after the coroutines.
            yield return new WaitForSeconds(0.3f);
            yield return null;

            AssertIsIn(door, Idle, "a little over 0.2 s after Switch(A)");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C3_CrossFade_ToAStateOfAnotherLayer_FadesOnThatLayer()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host, CrossFade(_a, StateMoment.Enter, Wave, 0f));
            yield return null;
            Assert.That(door.GetCurrentAnimatorStateInfo(OverlayLayer).IsName(Wave), Is.False, "Given");

            host.Switch(_a);
            door.Update(0.01f);

            Assert.That(door.GetCurrentAnimatorStateInfo(OverlayLayer).IsName(Wave), Is.True, "the Overlay layer is in Wave");
            AssertIsIn(door, Closed, "the Base Layer did not move");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_PausedSetBoolTrue_And_ResumedSetBoolFalse_WhenPushXOnA_ThenPop_PausedIsTrue_ThenFalse()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Paused, Paused, true),
                Bool(_a, StateMoment.Resumed, Paused, false));
            yield return null;
            host.Switch(_a);
            Assert.That(door.GetBool(Paused), Is.False, "Given: Current = A, not paused");

            host.Push(_x);

            Assert.That(door.GetBool(Paused), Is.True, "after Push(X) on top of A");

            host.Pop();

            Assert.That(door.GetBool(Paused), Is.False, "after Pop()");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C5

        [UnityTest]
        public IEnumerator C5_SwitchFromAToB_AppliesTheExitActionOfA_BeforeTheEnterActionOfB_WhateverTheirOrderInTheList()
        {
            // Both bindings write the same parameter: the one applied last wins.
            StateMachineHost host = CreateHost();
            Animator exitListedFirst = CreateDoor("Door (Exit A listed first)", host,
                Integer(_a, StateMoment.Exit, Mode, 1),
                Integer(_b, StateMoment.Enter, Mode, 2));
            Animator enterListedFirst = CreateDoor("Door (Enter B listed first)", host,
                Integer(_b, StateMoment.Enter, Mode, 2),
                Integer(_a, StateMoment.Exit, Mode, 1));
            yield return null;
            host.Switch(_a);
            Assert.That(exitListedFirst.GetInteger(Mode), Is.EqualTo(0), "Given: Current = A, nothing applied yet");
            Assert.That(enterListedFirst.GetInteger(Mode), Is.EqualTo(0), "Given: Current = A, nothing applied yet");

            host.Switch(_b);

            Assert.That(exitListedFirst.GetInteger(Mode), Is.EqualTo(2), "Exit A (Mode = 1) then Enter B (Mode = 2)");
            Assert.That(enterListedFirst.GetInteger(Mode), Is.EqualTo(2), "Exit A (Mode = 1) then Enter B (Mode = 2), although Enter B is listed first");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C6

        [UnityTest]
        public IEnumerator C6_NamesThatAreNotInTheController_WhenTheSignalComes_WarnOncePerBinding_ThrowNothing_AndTheOtherBindingsStillWork()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Trigger(_a, StateMoment.Enter, "NoTrigger"),
                Bool(_a, StateMoment.Enter, "NoBool", true),
                Integer(_a, StateMoment.Enter, "NoInt", 1),
                CrossFade(_a, StateMoment.Enter, "NoState", 0.2f),
                Bool(_a, StateMoment.Enter, IsOpen, true));
            yield return null;
            LogAssert.NoUnexpectedReceived(); // Given: nothing is logged before the signal comes

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[0\] \(A, Enter, SetTrigger 'NoTrigger'\): .*'StateAnimatorBindingTest' has no Trigger parameter 'NoTrigger'"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[1\] \(A, Enter, SetBool 'NoBool'\): .*'StateAnimatorBindingTest' has no Bool parameter 'NoBool'"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[2\] \(A, Enter, SetInteger 'NoInt'\): .*'StateAnimatorBindingTest' has no Int parameter 'NoInt'"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[3\] \(A, Enter, CrossFade 'NoState'\): .*'StateAnimatorBindingTest' has no state 'NoState'"));

            Assert.DoesNotThrow(() => host.Switch(_a), "the signal comes");

            Assert.That(door.GetBool(IsOpen), Is.True, "the binding after the four broken ones still works");

            // The same signal twice more: the four warnings above are the only ones.
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            door.Update(0.01f);

            AssertIsIn(door, Closed, "no cross fade happened");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C6_TwoBindings_WithTheSameMissingName_WarnOnceEach()
        {
            StateMachineHost host = CreateHost();
            CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, "NoBool", true),
                Bool(_a, StateMoment.Exit, "NoBool", false));
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[0\] \(A, Enter, SetBool 'NoBool'\)"));
            host.Switch(_a);
            LogAssert.NoUnexpectedReceived(); // the Exit binding has not been needed yet

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[1\] \(A, Exit, SetBool 'NoBool'\)"));
            host.Switch(_b);
            host.Switch(_a);
            host.Switch(_b);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C6_ParameterOfAnotherType_WarnsOncePerBinding_AndIsNotSet()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, Open, true),
                Trigger(_a, StateMoment.Enter, IsOpen),
                Integer(_a, StateMoment.Enter, Paused, 1),
                Bool(_a, StateMoment.Enter, Mode, true));
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[0\] \(A, Enter, SetBool 'Open'\): .*the parameter 'Open' of .* is of type Trigger, not Bool"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[1\] \(A, Enter, SetTrigger 'IsOpen'\): .*the parameter 'IsOpen' of .* is of type Bool, not Trigger"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[2\] \(A, Enter, SetInteger 'Paused'\): .*the parameter 'Paused' of .* is of type Bool, not Int"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[3\] \(A, Enter, SetBool 'Mode'\): .*the parameter 'Mode' of .* is of type Int, not Bool"));

            host.Switch(_a);
            host.Switch(_b);
            host.Switch(_a);

            Assert.That(door.GetBool(Open), Is.False, "the trigger was not set as a bool");
            Assert.That(door.GetBool(IsOpen), Is.False, "the bool was not set as a trigger");
            Assert.That(door.GetBool(Paused), Is.False, "the bool was not set as an integer");
            Assert.That(door.GetInteger(Mode), Is.EqualTo(0), "the integer was not set as a bool");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C6_BrokenBinding_StillWarnsOnlyOnce_WhenItsGameObjectIsDeactivatedAndActivatedAgain()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host, Bool(_a, StateMoment.Enter, "NoBool", true));
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[0\] \(A, Enter, SetBool 'NoBool'\)"));
            host.Switch(_a);

            // Enter A is applied again each time (C7): the broken binding must not warn again.
            door.gameObject.SetActive(false);
            door.gameObject.SetActive(true);
            door.gameObject.SetActive(false);
            door.gameObject.SetActive(true);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C6_AnimatorWithoutAController_WarnsOncePerBinding_AndThrowsNothing()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("Door");
            gameObject.AddComponent<Animator>();
            AddBinding(gameObject, host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                CrossFade(_a, StateMoment.Enter, Idle, 0.2f));
            gameObject.SetActive(true);
            yield return null;
            LogAssert.NoUnexpectedReceived(); // Given: nothing is logged before the signal comes

            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[0\] \(A, Enter, SetBool 'IsOpen'\): the Animator has no Controller"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^StateAnimatorBinding on 'Door': Bindings\[1\] \(A, Enter, CrossFade 'Idle'\): the Animator has no Controller"));

            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));

            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C7

        [UnityTest]
        public IEnumerator C7_GameObjectInactive_WhenAIsEntered_ThenActivatedWhileStillInA_AppliesTheEnterActionOfA_InOnEnable()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                Trigger(_a, StateMoment.Enter, Open));
            yield return null;
            door.gameObject.SetActive(false);
            host.Switch(_a);
            Assert.That(host.Machine.Current, Is.SameAs(_a), "Given: A was entered while the GameObject was inactive");

            door.gameObject.SetActive(true);

            // No frame in between: it happened in OnEnable.
            Assert.That(door.GetBool(IsOpen), Is.True, "SetBool of Enter A, right after SetActive(true)");
            Assert.That(door.GetBool(Open), Is.True, "SetTrigger of Enter A, right after SetActive(true)");

            door.Update(0.01f);

            AssertIsIn(door, Opened, "after the Animator updated");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_GameObjectThatStartsInactive_ActivatedForTheFirstTimeWhileInA_AppliesTheEnterActionOfA()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("Door that starts inactive");
            Animator door = gameObject.AddComponent<Animator>();
            door.runtimeAnimatorController = _controller;
            AddBinding(gameObject, host, Bool(_a, StateMoment.Enter, IsOpen, true));
            yield return null;
            host.Switch(_a);
            host.Switch(_b);
            host.Switch(_a);
            Assert.That(gameObject.activeSelf, Is.False, "Given: never activated, so Awake has not run yet");

            gameObject.SetActive(true);

            Assert.That(door.GetBool(IsOpen), Is.True, "right after the first SetActive(true)");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_BindingAboveTheAnimatorInTheInspector_ActivatedWhileInA_AppliesTheEnterActionOfA()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("Door with the binding first");
            AddBinding(gameObject, host, Bool(_a, StateMoment.Enter, IsOpen, true));
            Animator door = gameObject.AddComponent<Animator>();
            door.runtimeAnimatorController = _controller;
            host.Switch(_a);
            yield return null;

            gameObject.SetActive(true);

            Assert.That(door.GetBool(IsOpen), Is.True, "right after the first SetActive(true)");

            gameObject.SetActive(false);
            gameObject.SetActive(true);

            Assert.That(door.GetBool(IsOpen), Is.True, "and after it is activated again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_DeactivatedAndActivatedAgain_WhileStillInA_AppliesEveryEnterActionOfA_Again()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                Integer(_a, StateMoment.Enter, Mode, 4),
                Trigger(_a, StateMoment.Enter, Open),
                CrossFade(_a, StateMoment.Enter, Wave, 0f));
            yield return null;
            host.Switch(_a);
            door.Update(0.01f);
            Assert.That(door.GetBool(IsOpen), Is.True, "Given: IsOpen set by Enter A");
            Assert.That(door.GetInteger(Mode), Is.EqualTo(4), "Given: Mode set by Enter A");
            AssertIsIn(door, Opened, "Given: Opened by the trigger of Enter A");
            Assert.That(door.GetCurrentAnimatorStateInfo(OverlayLayer).IsName(Wave), Is.True, "Given: Wave by the cross fade of Enter A");

            door.gameObject.SetActive(false);
            door.gameObject.SetActive(true);

            // The Animator lost its parameters and went back to its default states while it was inactive.
            Assert.That(door.GetBool(IsOpen), Is.True, "SetBool applied again");
            Assert.That(door.GetInteger(Mode), Is.EqualTo(4), "SetInteger applied again");
            Assert.That(door.GetBool(Open), Is.True, "SetTrigger applied again");

            door.Update(0.01f);

            AssertIsIn(door, Opened, "the trigger led to Opened again");
            Assert.That(door.GetCurrentAnimatorStateInfo(OverlayLayer).IsName(Wave), Is.True, "CrossFade applied again");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_ActivatedWhileAIsCoveredByX_AppliesTheEnterActionsOfA_ThenItsPausedActions()
        {
            // Enter and Paused write the same parameter, and Paused is listed first: the one applied last wins.
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Integer(_a, StateMoment.Paused, Mode, 2),
                Integer(_a, StateMoment.Enter, Mode, 1),
                Bool(_a, StateMoment.Enter, IsOpen, true),
                Bool(_x, StateMoment.Enter, Paused, true));
            yield return null;
            door.gameObject.SetActive(false);
            host.Switch(_a);
            host.Push(_x);

            door.gameObject.SetActive(true);

            Assert.That(door.GetBool(IsOpen), Is.True, "Enter A");
            Assert.That(door.GetInteger(Mode), Is.EqualTo(2), "Enter A (Mode = 1) then Paused A (Mode = 2)");
            Assert.That(door.GetBool(Paused), Is.True, "Enter X, the current state");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_ExitAndPausedThatHappenWhileInactive_AreNotApplied_NotEvenWhenActivated()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                Integer(_a, StateMoment.Paused, Mode, 2),
                Integer(_a, StateMoment.Exit, Mode, 5));
            yield return null;
            host.Switch(_a);
            Assert.That(door.GetBool(IsOpen), Is.True, "Given: Enter A applied while active");

            door.gameObject.SetActive(false);
            Assert.DoesNotThrow(() => host.Push(_x), "Paused A while inactive");
            Assert.DoesNotThrow(() => host.Switch(_b), "Exit A while inactive");
            door.gameObject.SetActive(true);

            // The Animator is back to its defaults, and A is not on the stack: nothing of A applies.
            Assert.That(door.GetInteger(Mode), Is.EqualTo(0), "neither Paused A nor Exit A was applied");
            Assert.That(door.GetBool(IsOpen), Is.False, "Enter A is not applied again: the current state is B");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator C7_ComponentDisabled_AppliesNothing_AndWhenEnabledWhileStillInA_AppliesTheEnterActionOfA()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host, Bool(_a, StateMoment.Enter, IsOpen, true));
            StateAnimatorBinding binding = door.GetComponent<StateAnimatorBinding>();
            yield return null;
            binding.enabled = false;

            host.Switch(_a);

            Assert.That(door.GetBool(IsOpen), Is.False, "while the component is disabled");

            binding.enabled = true;

            Assert.That(door.GetBool(IsOpen), Is.True, "right after the component is enabled");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C8

        [UnityTest]
        public IEnumerator C8_NoAnimatorOnTheGameObject_LogsOneError_AndNothingThrows()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("No animator");
            AddBinding(gameObject, host, Bool(_a, StateMoment.Enter, IsOpen, true));
            LogAssert.Expect(LogType.Error, new Regex("^StateAnimatorBinding on 'No animator': there is no Animator on this GameObject"));

            gameObject.SetActive(true);
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));
            gameObject.SetActive(false);
            gameObject.SetActive(true);
            Assert.DoesNotThrow(() => host.Switch(_a));
            yield return null;

            LogAssert.NoUnexpectedReceived(); // one error only, not one per activation or per signal
        }

        [UnityTest]
        public IEnumerator C8_NoHost_LogsOneError_AndNothingThrows()
        {
            StateMachineHost unrelatedHost = CreateHost("Host the binding does not know");
            GameObject gameObject = CreateInactiveObject("No host");
            Animator door = gameObject.AddComponent<Animator>();
            door.runtimeAnimatorController = _controller;
            AddBinding(gameObject, NoHost, Bool(_a, StateMoment.Enter, IsOpen, true));
            LogAssert.Expect(LogType.Error, new Regex("^StateAnimatorBinding on 'No host': Host is not set and there is no StateMachineHost on this GameObject or its parents"));

            gameObject.SetActive(true);
            yield return null;

            Assert.DoesNotThrow(() => unrelatedHost.Switch(_a));
            gameObject.SetActive(false);
            gameObject.SetActive(true);
            yield return null;

            Assert.That(door.GetBool(IsOpen), Is.False, "it does not follow a host it was not given");
            LogAssert.NoUnexpectedReceived(); // one error only
        }

        [UnityTest]
        public IEnumerator C8_NoAnimatorAndNoHost_LogsOneError()
        {
            GameObject gameObject = CreateInactiveObject("Nothing");
            AddBinding(gameObject, NoHost, Bool(_a, StateMoment.Enter, IsOpen, true));
            LogAssert.Expect(LogType.Error, new Regex("^StateAnimatorBinding on 'Nothing': there is no Animator on this GameObject, and Host is not set and there is no StateMachineHost on this GameObject or its parents"));

            gameObject.SetActive(true);
            yield return null;
            gameObject.SetActive(false);
            gameObject.SetActive(true);
            yield return null;

            LogAssert.NoUnexpectedReceived(); // one error for both, and only once
        }

        [UnityTest]
        public IEnumerator C8_HostLeftEmpty_UsesTheHostOnAParent_AndLogsNothing()
        {
            StateMachineHost host = CreateHost();
            GameObject gameObject = CreateInactiveObject("Door under the host");
            gameObject.transform.SetParent(host.transform, false);
            Animator door = gameObject.AddComponent<Animator>();
            door.runtimeAnimatorController = _controller;
            AddBinding(gameObject, NoHost, Bool(_a, StateMoment.Enter, IsOpen, true));
            gameObject.SetActive(true);
            yield return null;

            host.Switch(_a);

            Assert.That(door.GetBool(IsOpen), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- other rules

        [UnityTest]
        public IEnumerator Rule_SlotsWithoutAState_AreIgnored_AndLogNothing()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                null,
                Bool(null, StateMoment.Enter, "NoBool", true),
                Bool(_a, StateMoment.Enter, IsOpen, true));
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_a));
            Assert.DoesNotThrow(() => host.Switch(_b));

            Assert.That(door.GetBool(IsOpen), Is.True, "the binding after the empty slots still works");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_AfterItsGameObjectIsDestroyed_ASignalThrowsNothing_AndLogsNothing()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                Bool(_a, StateMoment.Exit, IsOpen, false),
                Bool(_b, StateMoment.Enter, Paused, true));
            yield return null;
            host.Switch(_a);
            Assert.That(door.GetBool(IsOpen), Is.True, "Given: it follows the host");

            Object.Destroy(door.gameObject);
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_b));
            Assert.DoesNotThrow(() => host.Switch(_a));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Rule_AfterTheAnimatorIsRemoved_ASignalThrowsNothing_AndLogsNothing()
        {
            StateMachineHost host = CreateHost();
            Animator door = CreateDoor("Door", host,
                Bool(_a, StateMoment.Enter, IsOpen, true),
                CrossFade(_a, StateMoment.Enter, Idle, 0.2f));
            yield return null;

            Object.Destroy(door);
            yield return null;

            Assert.DoesNotThrow(() => host.Switch(_a));
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- helpers

        static StateAnimatorBinding.Entry Trigger(StateId state, StateMoment moment, string parameter)
        {
            return new StateAnimatorBinding.Entry
            {
                State = state, Moment = moment, Action = StateAnimatorAction.SetTrigger, Name = parameter,
            };
        }

        static StateAnimatorBinding.Entry Bool(StateId state, StateMoment moment, string parameter, bool value)
        {
            return new StateAnimatorBinding.Entry
            {
                State = state, Moment = moment, Action = StateAnimatorAction.SetBool, Name = parameter, BoolValue = value,
            };
        }

        static StateAnimatorBinding.Entry Integer(StateId state, StateMoment moment, string parameter, int value)
        {
            return new StateAnimatorBinding.Entry
            {
                State = state, Moment = moment, Action = StateAnimatorAction.SetInteger, Name = parameter, IntValue = value,
            };
        }

        static StateAnimatorBinding.Entry CrossFade(StateId state, StateMoment moment, string animatorState, float seconds)
        {
            return new StateAnimatorBinding.Entry
            {
                State = state, Moment = moment, Action = StateAnimatorAction.CrossFade, Name = animatorState, Duration = seconds,
            };
        }

        /// <summary>One line per binding, with only the value its action uses.</summary>
        static string[] Describe(IReadOnlyList<StateAnimatorBinding.Entry> bindings)
        {
            var lines = new string[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
            {
                StateAnimatorBinding.Entry entry = bindings[i];
                string line = $"{(entry.State != null ? entry.State.name : "none")} {entry.Moment} {entry.Action} {entry.Name}";
                switch (entry.Action)
                {
                    case StateAnimatorAction.SetBool:
                        line += $" {entry.BoolValue}";
                        break;
                    case StateAnimatorAction.SetInteger:
                        line += $" {entry.IntValue}";
                        break;
                    case StateAnimatorAction.CrossFade:
                        line += $" {entry.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                        break;
                }

                lines[i] = line;
            }

            return lines;
        }

        /// <summary>The Base Layer is in <paramref name="animatorState"/> and is not fading to another state.</summary>
        static void AssertIsIn(Animator animator, string animatorState, string when)
        {
            Assert.That(animator.IsInTransition(BaseLayer), Is.False, $"{when}: the Base Layer is not in a transition");
            Assert.That(animator.GetCurrentAnimatorStateInfo(BaseLayer).IsName(animatorState), Is.True, $"{when}: the Base Layer is in '{animatorState}'");
        }

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

        /// <summary>
        /// An Animator that plays the test controller with a binding next to it, on its own object, set up before
        /// the object is first activated. It is active when this returns.
        /// </summary>
        Animator CreateDoor(string name, StateMachineHost host, params StateAnimatorBinding.Entry[] bindings)
        {
            GameObject gameObject = CreateInactiveObject(name);
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = _controller;
            AddBinding(gameObject, host, bindings);
            gameObject.SetActive(true);
            return animator;
        }

        /// <summary>Adds a binding to an object that is still inactive, so Host and Bindings are in place before it is enabled.</summary>
        static StateAnimatorBinding AddBinding(GameObject inactive, StateMachineHost host, params StateAnimatorBinding.Entry[] bindings)
        {
            Assert.That(inactive.activeInHierarchy, Is.False, "the binding must be set up before it is enabled");

            StateAnimatorBinding binding = inactive.AddComponent<StateAnimatorBinding>();
            binding.Host = host;
            binding.Bindings = bindings;
            return binding;
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

        StateAnimatorBindingTestSceneRefs FindSceneRefs()
        {
            foreach (GameObject root in _testScene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out StateAnimatorBindingTestSceneRefs refs))
                    return refs;
            }

            Assert.Fail($"No {nameof(StateAnimatorBindingTestSceneRefs)} on a root object of '{TestScenePath}'.");
            return null;
        }
    }
}
