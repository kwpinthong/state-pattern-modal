#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
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
    /// </summary>
    [PrebuildSetup(typeof(ImportedSampleSetup))]
    [PostBuildCleanup(typeof(ImportedSampleSetup))]
    public class SampleSceneTests
    {
        const string MainMenuPanel = "Main Menu Panel";
        const string GameplayHudPanel = "Gameplay HUD Panel";
        const string PausePanel = "Pause Panel";

        const string OnEnter = "OnEnter";
        const string OnExit = "OnExit";
        const string OnPaused = "OnPaused";
        const string OnResumed = "OnResumed";

        Scene _scene;
        StateMachineHost _host;
        GameObject _mainMenuPanel;
        GameObject _gameplayHudPanel;
        GameObject _pausePanel;
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

            yield return EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _scene = SceneManager.GetActiveScene();
            Assert.That(_scene.path, Is.EqualTo(scenePath), "the sample scene is the active scene");

            // One frame: every Start has run (the host enters its Initial State there) and the Canvas is laid out.
            yield return null;

            _host = Find("State Machine").GetComponent<StateMachineHost>();
            _mainMenuPanel = Find("Canvas/Main Menu/" + MainMenuPanel);
            _gameplayHudPanel = Find("Canvas/Gameplay HUD/" + GameplayHudPanel);
            _pausePanel = Find("Canvas/Pause Popup/" + PausePanel);
            _gameplayHudListener = Find("Canvas/Gameplay HUD").GetComponent<StateListener>();
            _gameplayStatus = Find("Canvas/Gameplay HUD/" + GameplayHudPanel + "/Status Text").GetComponent<Text>();
            _play = FindButton("Canvas/Main Menu/" + MainMenuPanel + "/Play Button");
            _pause = FindButton("Canvas/Gameplay HUD/" + GameplayHudPanel + "/Pause Button");
            _hudMainMenu = FindButton("Canvas/Gameplay HUD/" + GameplayHudPanel + "/Main Menu Button");
            _resume = FindButton("Canvas/Pause Popup/" + PausePanel + "/Window/Resume Button");
            _pauseMainMenu = FindButton("Canvas/Pause Popup/" + PausePanel + "/Window/Main Menu Button");
        }

        [UnityTearDown]
        public IEnumerator UnloadSampleScene()
        {
            // Do not leave the sample's host, Canvas and EventSystem behind for the next test.
            if (_scene.IsValid() && _scene.isLoaded)
            {
                Scene empty = SceneManager.CreateScene($"SampleSceneTests empty {Time.frameCount}");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(_scene);
            }

            _scene = default;
        }

        // ---------------------------------------------------------------- C2

        [UnityTest]
        public IEnumerator C2_MainMenu_Gameplay_Pause_Resume_MainMenu_ShowsThePanelsOfTheState_AtEveryStep()
        {
            AssertStack("after loading the scene", "MainMenu");
            AssertShownPanels("after loading the scene", MainMenuPanel);

            yield return Click(_play);
            AssertStack("after Play", "Gameplay");
            AssertShownPanels("after Play", GameplayHudPanel);

            yield return Click(_pause);
            AssertStack("after Pause", "Gameplay", "Pause");
            AssertShownPanels("after Pause", GameplayHudPanel, PausePanel);

            yield return Click(_resume);
            AssertStack("after Resume", "Gameplay");
            AssertShownPanels("after Resume", GameplayHudPanel);

            yield return Click(_hudMainMenu);
            AssertStack("after Main Menu", "MainMenu");
            AssertShownPanels("after Main Menu", MainMenuPanel);
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

        // ---------------------------------------------------------------- C3

        [UnityTest]
        public IEnumerator C3_Pause_PushedOverGameplay_GivesTheHud_OnPaused_NotOnExit_AndTheHudStaysShown()
        {
            yield return Click(_play);
            List<string> signals = RecordSignalsOf(_gameplayHudListener);
            Assert.That(_gameplayStatus.text, Does.Not.Contain("paused").IgnoreCase, "Given: the HUD shows gameplay as running");

            yield return Click(_pause);

            Assert.That(signals, Is.EqualTo(new[] { OnPaused }), "signals of the HUD's listener after Pause");
            Assert.That(_gameplayHudPanel.activeInHierarchy, Is.True, "the HUD is still shown under the Pause popup");
            Assert.That(_gameplayStatus.text, Does.Contain("paused").IgnoreCase, "On Paused changed the HUD's status text");

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

            GameObject clicked = ClickTargetAt(_pause);

            Assert.That(clicked, Is.Not.SameAs(_pause.gameObject), "what a click on the HUD's Pause button lands on while paused");
            Assert.That(_gameplayHudPanel.activeInHierarchy, Is.True, "the HUD is shown, only covered");
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
            AssertShownPanels("after Main Menu from Pause", MainMenuPanel);
            Assert.That(signals, Is.EqualTo(new[] { OnExit }), "signals of the HUD's listener: paused → inactive");
        }

        [UnityTest]
        public IEnumerator PlayingAgain_AfterLeavingFromPause_ShowsTheHudAsRunning()
        {
            yield return Click(_play);
            yield return Click(_pause);
            yield return Click(_pauseMainMenu);

            yield return Click(_play);

            AssertStack("after Play again", "Gameplay");
            AssertShownPanels("after Play again", GameplayHudPanel);
            Assert.That(_gameplayStatus.text, Does.Not.Contain("paused").IgnoreCase, "status text of the HUD");
        }

        // ---------------------------------------------------------------- helpers

        void AssertStack(string when, params string[] bottomToTop)
        {
            Assert.That(_host.Machine.Stack.Select(state => state.name), Is.EqualTo(bottomToTop), $"{when}: stack, bottom → top");
        }

        void AssertShownPanels(string when, params string[] expected)
        {
            IEnumerable<string> shown = new[] { _mainMenuPanel, _gameplayHudPanel, _pausePanel }
                .Where(panel => panel.activeInHierarchy)
                .Select(panel => panel.name);

            Assert.That(shown, Is.EqualTo(expected), $"{when}: panels shown");
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
        /// Then one frame, so anything the new state breaks per frame fails the test.
        /// </summary>
        static IEnumerator Click(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, $"'{button.name}' is shown and interactable");
            GameObject target = ClickTargetAt(button);
            Assert.That(target, Is.SameAs(button.gameObject), $"what a click on '{button.name}' lands on");

            ExecuteEvents.Execute(target, PointerAt(button), ExecuteEvents.pointerClickHandler);
            yield return null;
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
