#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Smoke test of the sample scene (Samples~/MenuGameplayPause), played the way a user plays it:
    /// by clicking its buttons. The sample is imported through the Package Manager before the run and deleted
    /// after it, see <see cref="ImportedSample"/>. Editor only: a sample is not part of a player build.
    /// <para>The panels of the sample are opened and closed by an Animator (through StateAnimatorBinding), so
    /// "shown" is read from the Animator state of the panel and from what its CanvasGroup lets through, after
    /// waiting for the animation. The tests named GEN011_ are the cases of the Animator version of the sample;
    /// the others are the cases of the first version (C2, C3) and still hold.</para>
    /// </summary>
    [PrebuildSetup(typeof(ImportedSampleSetup))]
    [PostBuildCleanup(typeof(ImportedSampleSetup))]
    public class SampleSceneTests
    {
        const string OnEnter = "OnEnter";
        const string OnExit = "OnExit";
        const string OnPaused = "OnPaused";
        const string OnResumed = "OnResumed";

        // Animator states of a panel.
        const string Closed = "Closed";
        const string Open = "Open";
        const string Dimmed = "Dimmed";
        static readonly string[] AnimatorStates = { Closed, Open, Dimmed };

        /// <summary>The tests run at a fixed time step, so an animation takes the same number of frames on any machine.</summary>
        const int FramesPerSecond = 60;

        /// <summary>How long a test waits for the Animators (5 seconds of game time) before it fails.</summary>
        const int TimeoutFrames = 5 * FramesPerSecond;

        /// <summary>Once the panels are as expected they have to stay so: a panel that reopens later fails too.</summary>
        const int HoldFrames = FramesPerSecond / 2;

        sealed class Panel
        {
            public readonly string Name;
            public readonly GameObject Content;
            public readonly Animator Animator;
            public readonly CanvasGroup Group;

            /// <summary>Alpha of the CanvasGroup at every frame of the last <see cref="ExpectPanels"/>.</summary>
            public readonly List<float> Alpha = new List<float>();

            public Panel(GameObject root, GameObject content)
            {
                Name = root.name;
                Content = content;
                Animator = root.GetComponent<Animator>();
                Group = root.GetComponent<CanvasGroup>();
            }
        }

        Scene _scene;
        int _captureFramerate;
        StateMachineHost _host;
        Panel _mainMenu;
        Panel _hud;
        Panel _pausePopup;
        StateListener _gameplayHudListener;
        Text _gameplayStatus;
        Button _play;
        Button _pause;
        Button _hudMainMenu;
        Button _resume;
        Button _pauseMainMenu;

        [UnitySetUp]
        public IEnumerator LoadSampleScene()
        {
            Assert.That(ImportedSample.Failure, Is.Null, "importing the sample before the run");
            string scenePath = ImportedSample.ScenePath;
            Assert.That(ImportedSample.IsImportedByTests && File.Exists(scenePath), Is.True,
                $"'{scenePath}' was imported by this test run");

            _captureFramerate = Time.captureFramerate;
            Time.captureFramerate = FramesPerSecond;

            yield return EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _scene = SceneManager.GetActiveScene();
            Assert.That(_scene.path, Is.EqualTo(scenePath), "the sample scene is the active scene");

            // One frame: every Start has run (the host enters its Initial State there) and the Canvas is laid out.
            yield return null;

            _host = Find("State Machine").GetComponent<StateMachineHost>();
            _mainMenu = new Panel(Find("Canvas/Main Menu"), Find("Canvas/Main Menu/Main Menu Panel"));
            _hud = new Panel(Find("Canvas/Gameplay HUD"), Find("Canvas/Gameplay HUD/Gameplay HUD Panel"));
            _pausePopup = new Panel(Find("Canvas/Pause Popup"), Find("Canvas/Pause Popup/Pause Panel"));
            _gameplayHudListener = Find("Canvas/Gameplay HUD").GetComponent<StateListener>();
            _gameplayStatus = Find("Canvas/Gameplay HUD/Gameplay HUD Panel/Status Text").GetComponent<Text>();
            _play = FindButton("Canvas/Main Menu/Main Menu Panel/Play Button");
            _pause = FindButton("Canvas/Gameplay HUD/Gameplay HUD Panel/Pause Button");
            _hudMainMenu = FindButton("Canvas/Gameplay HUD/Gameplay HUD Panel/Main Menu Button");
            _resume = FindButton("Canvas/Pause Popup/Pause Panel/Window/Resume Button");
            _pauseMainMenu = FindButton("Canvas/Pause Popup/Pause Panel/Window/Main Menu Button");
        }

        [UnityTearDown]
        public IEnumerator UnloadSampleScene()
        {
            Time.captureFramerate = _captureFramerate;

            // Do not leave the sample's host, Canvas and EventSystem behind for the next test.
            if (_scene.IsValid() && _scene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene($"SampleSceneTests empty {Time.frameCount}");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(_scene);
            }

            _scene = default;
        }

        // ---------------------------------------------------------------- GEN-011 C1

        [UnityTest]
        public IEnumerator GEN011_C1_MainMenuToGameplay_PlaysTheCloseAnimationOfTheMainMenu_AndTheOpenAnimationOfTheHud()
        {
            yield return ExpectPanels("Given: MainMenu is the current state", mainMenu: Open, hud: Closed, pausePopup: Closed);

            yield return Click(_play);

            AssertStack("one frame after Play", "Gameplay");
            Assert.That(_mainMenu.Group.alpha, Is.GreaterThan(0f),
                "alpha of the Main Menu one frame after Play: the state changed at once, the panel is still closing");

            yield return ExpectPanels("after Play", mainMenu: Closed, hud: Open, pausePopup: Closed);
            AssertFaded(_mainMenu, "close animation of the Main Menu", fadesOut: true);
            AssertFaded(_hud, "open animation of the Gameplay HUD", fadesOut: false);
        }

        // ---------------------------------------------------------------- GEN-011 C2

        [UnityTest]
        public IEnumerator GEN011_C2_PushPause_PlaysTheDimAnimationOfTheHud_AndPopBringsItBack()
        {
            yield return Click(_play);
            yield return ExpectPanels("Given: Gameplay is the current state", mainMenu: Closed, hud: Open, pausePopup: Closed);

            yield return Click(_pause);

            AssertStack("after Pause", "Gameplay", "Pause");
            yield return ExpectPanels("after Pause (Push)", mainMenu: Closed, hud: Dimmed, pausePopup: Open);
            AssertFaded(_hud, "dim animation of the Gameplay HUD", fadesOut: true);
            AssertFaded(_pausePopup, "open animation of the Pause popup", fadesOut: false);

            yield return Click(_resume);

            AssertStack("after Resume", "Gameplay");
            yield return ExpectPanels("after Resume (Pop)", mainMenu: Closed, hud: Open, pausePopup: Closed);
            AssertFaded(_hud, "animation of the Gameplay HUD back from dimmed", fadesOut: false);
            AssertFaded(_pausePopup, "close animation of the Pause popup", fadesOut: true);
        }

        // ---------------------------------------------------------------- GEN-011 C3

        /// <summary>Three state changes in a row, as the buttons of the sample make them, and the stack they end with.</summary>
        public sealed class RapidChanges
        {
            public readonly string Name;
            public readonly string Given;
            public readonly string[] Changes;
            public readonly string[] Stack;

            public RapidChanges(string name, string given, string[] changes, string[] stack)
            {
                Name = name;
                Given = given;
                Changes = changes;
                Stack = stack;
            }

            public override string ToString() => Name;
        }

        static readonly RapidChanges[] ThreeChangesInARow =
        {
            new RapidChanges("FromMainMenu_Gameplay_MainMenu_Gameplay", "MainMenu",
                new[] { "Switch Gameplay", "Switch MainMenu", "Switch Gameplay" }, new[] { "Gameplay" }),
            new RapidChanges("FromMainMenu_Gameplay_PushPause_Pop", "MainMenu",
                new[] { "Switch Gameplay", "Push Pause", "Pop" }, new[] { "Gameplay" }),
            new RapidChanges("FromMainMenu_Gameplay_PushPause_MainMenu", "MainMenu",
                new[] { "Switch Gameplay", "Push Pause", "Switch MainMenu" }, new[] { "MainMenu" }),
            new RapidChanges("FromGameplay_PushPause_Pop_PushPause", "Gameplay",
                new[] { "Push Pause", "Pop", "Push Pause" }, new[] { "Gameplay", "Pause" }),
            new RapidChanges("FromGameplay_PushPause_MainMenu_Gameplay", "Gameplay",
                new[] { "Push Pause", "Switch MainMenu", "Switch Gameplay" }, new[] { "Gameplay" }),
            new RapidChanges("FromGameplay_MainMenu_Gameplay_MainMenu", "Gameplay",
                new[] { "Switch MainMenu", "Switch Gameplay", "Switch MainMenu" }, new[] { "MainMenu" })
        };

        /// <summary>
        /// 0 = the three changes in the same frame; 1 and 6 = one and six frames apart, which is inside the
        /// animation of the change before (the animations of the sample take 0.2 s = 12 frames here).
        /// </summary>
        [UnityTest]
        public IEnumerator GEN011_C3_ThreeChangesInARow_WhileTheAnimationsStillPlay_LeaveOnlyThePanelsOfTheCurrentState(
            [ValueSource(nameof(ThreeChangesInARow))] RapidChanges changes,
            [Values(0, 1, 6)] int framesBetweenChanges)
        {
            if (changes.Given == "Gameplay")
                yield return Click(_play);

            yield return ExpectPanels($"Given: {changes.Given} is the current state",
                PoseOf("MainMenu", new[] { changes.Given }), PoseOf("Gameplay", new[] { changes.Given }), PoseOf("Pause", new[] { changes.Given }));

            for (int i = 0; i < changes.Changes.Length; i++)
            {
                if (i > 0)
                {
                    for (int frame = 0; frame < framesBetweenChanges; frame++)
                        yield return null;
                }

                Change(changes.Changes[i]);
            }

            AssertStack("after the three changes", changes.Stack);
            yield return ExpectPanels($"after [{string.Join(", ", changes.Changes)}], {framesBetweenChanges} frame(s) apart",
                PoseOf("MainMenu", changes.Stack), PoseOf("Gameplay", changes.Stack), PoseOf("Pause", changes.Stack));
        }

        [UnityTest]
        public IEnumerator GEN011_C3_ClickingPlay_Pause_MainMenu_EachAsSoonAsItsButtonTakesTheClick_LeavesOnlyTheMainMenuOpen()
        {
            yield return Click(_play);
            Assert.That(_hud.Group.alpha, Is.LessThan(1f), "alpha of the HUD when Pause is clicked: it is still fading in");

            yield return Click(_pause);
            Assert.That(_pausePopup.Group.alpha, Is.LessThan(1f), "alpha of the Pause popup when its Main Menu button is clicked: it is still fading in");

            yield return Click(_pauseMainMenu);

            AssertStack("after Play, Pause, Main Menu", "MainMenu");
            yield return ExpectPanels("after Play, Pause, Main Menu", mainMenu: Open, hud: Closed, pausePopup: Closed);
        }

        // ---------------------------------------------------------------- GEN-011: input follows the state at once, the fade follows

        /// <summary>
        /// A second click on Resume while the popup fades out would Pop again, and a click on the HUD's Pause while
        /// the HUD fades out would push Pause over the Main Menu: a panel takes input for exactly as long as its
        /// state says, whatever its animation is doing.
        /// </summary>
        [UnityTest]
        public IEnumerator GEN011_APanelThatIsClosing_TakesNoClick_FromTheFrameItsStateIsLeft_AndOneThatIsOpening_TakesThemAtOnce()
        {
            yield return Click(_play);

            Assert.That(_mainMenu.Group.alpha, Is.GreaterThan(0f), "Given: one frame after Play the Main Menu is still fading out");
            Assert.That(TakesTheClick(_play), Is.False, "Play takes a click while the Main Menu fades out");
            Assert.That(TakesTheClick(_pause), Is.True, "Pause takes a click while the HUD fades in");

            yield return Click(_pause);

            Assert.That(TakesTheClick(_pause), Is.False, "Pause (HUD) takes a click while the HUD is dimmed under the popup");
            Assert.That(TakesTheClick(_resume), Is.True, "Resume takes a click while the popup fades in");

            yield return ExpectPanels("Given: paused, the popup is fully open", mainMenu: Closed, hud: Dimmed, pausePopup: Open);
            yield return Click(_resume);

            Assert.That(_pausePopup.Group.alpha, Is.GreaterThan(0f), "Given: one frame after Resume the popup is still fading out");
            Assert.That(TakesTheClick(_resume), Is.False, "Resume takes a second click while the popup fades out");
            Assert.That(TakesTheClick(_pause), Is.True, "Pause takes a click as soon as Gameplay is resumed");

            yield return Click(_hudMainMenu);

            Assert.That(_hud.Group.alpha, Is.GreaterThan(0f), "Given: one frame after Main Menu the HUD is still fading out");
            Assert.That(TakesTheClick(_pause), Is.False, "Pause takes a click while the HUD fades out");
            Assert.That(TakesTheClick(_play), Is.True, "Play takes a click while the Main Menu fades in");
        }

        // ---------------------------------------------------------------- GEN-011: closed panels stay active and take no input

        [UnityTest]
        public IEnumerator GEN011_ClosedPanels_StayActive_AndTheirButtonsTakeNoClickAndNoSubmit()
        {
            yield return Click(_play);
            yield return ExpectPanels("Given: Gameplay is the current state", mainMenu: Closed, hud: Open, pausePopup: Closed);

            Assert.That(_play.gameObject.activeInHierarchy && _pauseMainMenu.gameObject.activeInHierarchy, Is.True,
                "the buttons of the closed panels are still active GameObjects (the Animator hides them, not SetActive)");
            Assert.That(ClickTargetAt(_play), Is.Not.SameAs(_play.gameObject), "what a click at the place of the hidden Play button lands on");
            Assert.That(ClickTargetAt(_pauseMainMenu), Is.Not.SameAs(_pauseMainMenu.gameObject),
                "what a click at the place of the hidden Main Menu button of the popup lands on");

            // What a keyboard or a gamepad sends to the selected button, and a click that would get past the raycast.
            var noPointer = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(_pauseMainMenu.gameObject, noPointer, ExecuteEvents.submitHandler);
            ExecuteEvents.Execute(_pauseMainMenu.gameObject, PointerAt(_pauseMainMenu), ExecuteEvents.pointerClickHandler);
            yield return null;

            AssertStack("after a submit and a click sent to the hidden Main Menu button of the popup", "Gameplay");
        }

        // ---------------------------------------------------------------- C2 (first version of the sample)

        [UnityTest]
        public IEnumerator C2_MainMenu_Gameplay_Pause_Resume_MainMenu_ShowsThePanelsOfTheState_AtEveryStep()
        {
            AssertStack("after loading the scene", "MainMenu");
            yield return ExpectPanels("after loading the scene", mainMenu: Open, hud: Closed, pausePopup: Closed);

            yield return Click(_play);
            AssertStack("after Play", "Gameplay");
            yield return ExpectPanels("after Play", mainMenu: Closed, hud: Open, pausePopup: Closed);

            yield return Click(_pause);
            AssertStack("after Pause", "Gameplay", "Pause");
            yield return ExpectPanels("after Pause", mainMenu: Closed, hud: Dimmed, pausePopup: Open);

            yield return Click(_resume);
            AssertStack("after Resume", "Gameplay");
            yield return ExpectPanels("after Resume", mainMenu: Closed, hud: Open, pausePopup: Closed);

            yield return Click(_hudMainMenu);
            AssertStack("after Main Menu", "MainMenu");
            yield return ExpectPanels("after Main Menu", mainMenu: Open, hud: Closed, pausePopup: Closed);
        }

        [UnityTest]
        public IEnumerator C2_EventSystem_OfTheScene_HasAnInputModule_ThatWorksWithTheProjectsInputHandling()
        {
            // A module that does not match the active input handling throws every frame: let a few frames run.
            for (int frame = 0; frame < 3; frame++)
                yield return null;

            Assert.That(EventSystem.current, Is.Not.Null, "the scene has an EventSystem");
            Assert.That(EventSystem.current.currentInputModule, Is.Not.Null,
                "the EventSystem picked an input module, so real pointer input reaches the buttons");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------------------------------------------------------- C3 (first version of the sample)

        [UnityTest]
        public IEnumerator C3_Pause_PushedOverGameplay_GivesTheHud_OnPaused_NotOnExit_AndTheHudStaysShown()
        {
            yield return Click(_play);
            List<string> signals = RecordSignalsOf(_gameplayHudListener);
            Assert.That(_gameplayStatus.text, Does.Not.Contain("paused").IgnoreCase, "Given: the HUD shows gameplay as running");

            yield return Click(_pause);

            Assert.That(signals, Is.EqualTo(new[] { OnPaused }), "signals of the HUD's listener after Pause");
            Assert.That(_gameplayStatus.text, Does.Contain("paused").IgnoreCase, "On Paused changed the HUD's status text");
            yield return ExpectPanels("after Pause: the HUD is still shown, dimmed, under the Pause popup",
                mainMenu: Closed, hud: Dimmed, pausePopup: Open);

            yield return Click(_resume);

            Assert.That(signals, Is.EqualTo(new[] { OnPaused, OnResumed }),
                "signals of the HUD's listener after Resume: resumed, not entered again");
            Assert.That(_gameplayStatus.text, Does.Not.Contain("paused").IgnoreCase, "On Resumed changed the status text back");
        }

        [UnityTest]
        public IEnumerator C3_PausePopup_CoversTheHud_SoItsButtonsCannotBeClicked_WhilePaused()
        {
            yield return Click(_play);
            yield return Click(_pause);
            yield return ExpectPanels("Given: paused", mainMenu: Closed, hud: Dimmed, pausePopup: Open);

            GameObject clicked = ClickTargetAt(_pause);

            Assert.That(clicked, Is.Not.SameAs(_pause.gameObject), "what a click on the HUD's Pause button lands on while paused");
        }

        // ---------------------------------------------------------------- beyond the cases: the other way out of Pause

        [UnityTest]
        public IEnumerator Pause_MainMenuButton_SwitchesWithAStack_HidesHudAndPopup_AndTheHudGetsOnExitOnly()
        {
            yield return Click(_play);
            yield return Click(_pause);
            List<string> signals = RecordSignalsOf(_gameplayHudListener);

            yield return Click(_pauseMainMenu);

            AssertStack("after Main Menu from Pause", "MainMenu");
            Assert.That(signals, Is.EqualTo(new[] { OnExit }), "signals of the HUD's listener: paused → inactive");
            yield return ExpectPanels("after Main Menu from Pause", mainMenu: Open, hud: Closed, pausePopup: Closed);
        }

        [UnityTest]
        public IEnumerator PlayingAgain_AfterLeavingFromPause_ShowsTheHudAsRunning()
        {
            yield return Click(_play);
            yield return Click(_pause);
            yield return Click(_pauseMainMenu);

            yield return Click(_play);

            AssertStack("after Play again", "Gameplay");
            Assert.That(_gameplayStatus.text, Does.Not.Contain("paused").IgnoreCase, "status text of the HUD");
            yield return ExpectPanels("after Play again: the HUD is open, not dimmed", mainMenu: Closed, hud: Open, pausePopup: Closed);
        }

        // ---------------------------------------------------------------- helpers

        void AssertStack(string when, params string[] bottomToTop)
        {
            Assert.That(_host.Machine.Stack.Select(state => state.name), Is.EqualTo(bottomToTop), $"{when}: stack, bottom → top");
        }

        /// <summary>Makes one state change the way the buttons of the sample do: "Switch X", "Push X" or "Pop".</summary>
        void Change(string change)
        {
            string[] words = change.Split(' ');
            switch (words[0])
            {
                case "Switch":
                    _host.Switch(State(words[1]));
                    break;
                case "Push":
                    _host.Push(State(words[1]));
                    break;
                case "Pop":
                    _host.Pop();
                    break;
                default:
                    throw new ArgumentException($"unknown change '{change}'");
            }
        }

        static StateId State(string name)
        {
            string path = $"{ImportedSample.FolderPath}/States/{name}.asset";
            var state = AssetDatabase.LoadAssetAtPath<StateId>(path);
            Assert.That(state, Is.Not.Null, $"StateId asset '{path}'");
            return state;
        }

        /// <summary>The Animator state the panel of <paramref name="state"/> has to be in for this stack (bottom → top).</summary>
        static string PoseOf(string state, string[] stack)
        {
            if (!stack.Contains(state))
                return Closed;

            // Only the HUD has something for "covered by a pushed state".
            return state == "Gameplay" && stack[stack.Length - 1] != state ? Dimmed : Open;
        }

        /// <summary>
        /// Waits until the Animator of every panel is in the given state (and no longer in a transition), for at
        /// most <see cref="TimeoutFrames"/>, then checks that the panels stay so for <see cref="HoldFrames"/>.
        /// What is compared is what a player gets: the Animator state, whether the panel is visible, and
        /// whether it takes clicks.
        /// </summary>
        IEnumerator ExpectPanels(string when, string mainMenu, string hud, string pausePopup)
        {
            Panel[] panels = { _mainMenu, _hud, _pausePopup };
            string expected = string.Join(" | ", Expected(_mainMenu, mainMenu), Expected(_hud, hud), Expected(_pausePopup, pausePopup));

            foreach (Panel panel in panels)
            {
                Assert.That(panel.Animator != null && panel.Group != null, Is.True,
                    $"{when}: '{panel.Name}' has an Animator and a CanvasGroup (the panels are opened and closed by an Animator)");
                panel.Alpha.Clear();
            }

            string actual = Sample(panels);
            for (int frame = 0; actual != expected && frame < TimeoutFrames; frame++)
            {
                yield return null;
                actual = Sample(panels);
            }

            Assert.That(actual, Is.EqualTo(expected), $"{when}: panels, after waiting {TimeoutFrames} frames for their Animators");

            for (int frame = 0; frame < HoldFrames; frame++)
            {
                yield return null;
                Assert.That(Sample(panels), Is.EqualTo(expected), $"{when}: panels, {frame + 1} frame(s) after they were as expected");
            }
        }

        static string Sample(Panel[] panels)
        {
            foreach (Panel panel in panels)
                panel.Alpha.Add(panel.Group.alpha);

            return string.Join(" | ", panels.Select(Describe));
        }

        static string Expected(Panel panel, string animatorState)
        {
            switch (animatorState)
            {
                case Closed:
                    return $"{panel.Name}: Closed (hidden, takes no input)";
                case Open:
                    return $"{panel.Name}: Open (fully visible, takes clicks)";
                case Dimmed:
                    return $"{panel.Name}: Dimmed (dimmed, takes no input)";
                default:
                    throw new ArgumentException($"unknown Animator state '{animatorState}'");
            }
        }

        static string Describe(Panel panel)
        {
            if (!panel.Content.activeInHierarchy || !panel.Animator.isActiveAndEnabled)
                return $"{panel.Name}: inactive GameObject or disabled Animator";

            float alpha = panel.Group.alpha;
            string look = alpha <= 0.001f ? "hidden" : alpha >= 0.999f ? "fully visible" : "dimmed";

            bool interactable = panel.Group.interactable;
            bool blocksRaycasts = panel.Group.blocksRaycasts;
            string input = interactable && blocksRaycasts ? "takes clicks"
                : !interactable && !blocksRaycasts ? "takes no input"
                : blocksRaycasts ? "blocks clicks but is not interactable"
                : "interactable but lets clicks through";

            return $"{panel.Name}: {AnimatorStateOf(panel.Animator)} ({look}, {input})";
        }

        static string AnimatorStateOf(Animator animator)
        {
            string current = NameOf(animator.GetCurrentAnimatorStateInfo(0));
            return animator.IsInTransition(0) ? $"{current} → {NameOf(animator.GetNextAnimatorStateInfo(0))}" : current;
        }

        static string NameOf(AnimatorStateInfo state)
        {
            return AnimatorStates.FirstOrDefault(state.IsName) ?? "another state";
        }

        /// <summary>
        /// The alpha recorded by the last <see cref="ExpectPanels"/> went one way only and through values in
        /// between: the panel was animated, not switched.
        /// </summary>
        static void AssertFaded(Panel panel, string what, bool fadesOut)
        {
            float[] alpha = panel.Alpha.ToArray();
            string recorded = string.Join(", ", alpha.Select(value => value.ToString("0.00")));

            for (int i = 1; i < alpha.Length; i++)
            {
                bool rightWay = fadesOut ? alpha[i] <= alpha[i - 1] + 0.0001f : alpha[i] >= alpha[i - 1] - 0.0001f;
                Assert.That(rightWay, Is.True, $"{what}: alpha goes {(fadesOut ? "down" : "up")} only. Alpha per frame: {recorded}");
            }

            int steps = alpha.Select(value => Mathf.RoundToInt(value * 1000f)).Distinct().Count();
            Assert.That(steps, Is.GreaterThanOrEqualTo(3),
                $"{what}: alpha passes through values between its start and its end. Alpha per frame: {recorded}");
        }

        static List<string> RecordSignalsOf(StateListener listener)
        {
            var signals = new List<string>();
            listener.OnEnter.AddListener(() => signals.Add(OnEnter));
            listener.OnExit.AddListener(() => signals.Add(OnExit));
            listener.OnPaused.AddListener(() => signals.Add(OnPaused));
            listener.OnResumed.AddListener(() => signals.Add(OnResumed));
            return signals;
        }

        /// <summary>
        /// A left click at the middle of the button, delivered the way the EventSystem delivers it: the scene's
        /// raycasters decide what is under the pointer, so a button that is hidden or covered cannot be clicked.
        /// A panel that is opening takes clicks a little after its state was entered, so this first waits, as a
        /// player would, until the button takes the click (at most <see cref="TimeoutFrames"/>).
        /// Then one frame, so anything the new state breaks per frame fails the test.
        /// </summary>
        static IEnumerator Click(Button button)
        {
            for (int frame = 0; frame < TimeoutFrames && !TakesTheClick(button); frame++)
                yield return null;

            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, $"'{button.name}' is shown and interactable");
            GameObject target = ClickTargetAt(button);
            Assert.That(target, Is.SameAs(button.gameObject), $"what a click on '{button.name}' lands on");

            ExecuteEvents.Execute(target, PointerAt(button), ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        static bool TakesTheClick(Button button)
        {
            return button.isActiveAndEnabled && button.IsInteractable() && ClickTargetAt(button) == button.gameObject;
        }

        /// <summary>The object that would receive a click at the middle of the button, or null.</summary>
        static GameObject ClickTargetAt(Button button)
        {
            Assert.That(EventSystem.current, Is.Not.Null, "the scene has an EventSystem");
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(PointerAt(button), hits);
            return hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
        }

        static PointerEventData PointerAt(Button button)
        {
            var rect = (RectTransform)button.transform;
            Vector3 middle = rect.TransformPoint(rect.rect.center);
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                // The sample's Canvas is Screen Space - Overlay: world position = screen position.
                position = RectTransformUtility.WorldToScreenPoint(null, middle)
            };
        }

        Button FindButton(string path) => Find(path).GetComponent<Button>();

        /// <summary>Finds an object of the sample scene by its path in the Hierarchy, hidden or not.</summary>
        GameObject Find(string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            GameObject root = _scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == rootName);
            Assert.That(root, Is.Not.Null, $"root object '{rootName}' of the sample scene");
            if (slash < 0)
                return root;

            Transform child = root.transform.Find(path.Substring(slash + 1));
            Assert.That(child, Is.Not.Null, $"object '{path}' of the sample scene");
            return child.gameObject;
        }
    }
}
#endif
