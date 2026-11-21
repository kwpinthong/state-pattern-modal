using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// C4: what the README of the package has to contain. These tests check that the examples and sections are
    /// there; whether they read well is checked by reading it.
    /// </summary>
    public class ReadmeTests
    {
        const string ReadmePath = "Packages/com.alittlebb.statemachine/README.md";

        string _readme;
        string _code;

        [OneTimeSetUp]
        public void ReadTheReadme()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
            Assert.That(asset, Is.Not.Null, $"'{ReadmePath}' (with its .meta, so Unity imports it with the package)");

            _readme = asset.text.Replace("\r\n", "\n");
            _code = string.Join("\n", Regex.Matches(_readme, @"```csharp\n(.*?)```", RegexOptions.Singleline)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value));
        }

        [TestCase(".Switch(")]
        [TestCase(".Push(")]
        [TestCase(".Pop()")]
        public void C4_Readme_HasACodeExample_For(string call)
        {
            Assert.That(_code, Does.Contain(call), "C# code blocks of the README");
        }

        [Test]
        public void C4_Readme_HasACodeExample_ThatSubscribesFromCode()
        {
            Assert.That(_code, Does.Contain(".Subscribe("), "C# code blocks of the README");
            foreach (string callback in new[] { "onEnter", "onExit", "onPaused", "onResumed" })
                Assert.That(_code, Does.Contain(callback), "callbacks in the Subscribe example");

            Assert.That(_code, Does.Contain(".Dispose()"), "the example shows how to unsubscribe");
        }

        [Test]
        public void C4_Readme_ExplainsHowToSetUpAStateListener_InTheInspector()
        {
            string section = Section(@"StateListener.*Inspector|Inspector.*StateListener");

            foreach (string field in new[] { "Add Component", "Host", "States", "On Enter", "On Exit", "On Paused", "On Resumed" })
                Assert.That(section, Does.Contain(field), "the section about StateListener in the Inspector");
        }

        [Test]
        public void Readme_ExplainsTheActiveAndPausedRule()
        {
            string section = Section(@"active.*paused");

            foreach (string status in new[] { "Active", "Paused", "Inactive" })
                Assert.That(section, Does.Contain(status), "the section about active and paused");
        }

        [Test]
        public void Readme_ExplainsTheQueue_AndItsLimit()
        {
            string section = Section(@"queue");

            Assert.That(section, Does.Contain(nameof(StateMachine.MaxTransitionsPerRun)));
            Assert.That(section, Does.Contain(StateMachine.MaxTransitionsPerRun.ToString()), "the limit, as it is in the code");
        }

        [Test]
        public void Readme_ExplainsHowToImportTheSample_UnderTheNameThePackageManagerShows()
        {
            string section = Section(@"sample");

            Assert.That(section, Does.Contain("Package Manager"));
            Assert.That(section, Does.Contain("Import"));
            Assert.That(section, Does.Contain(ImportedSample.DisplayName), "display name of the sample in package.json");
        }

        // ---------------------------------------------------------------- GEN-011 C4: the Animator section

        [Test]
        public void GEN011_C4_Readme_AnimatorSection_ExplainsHowToSetUpABinding()
        {
            string section = Section(@"Animator");

            foreach (string step in new[] { "StateAnimatorBinding", "Add Component", "State Animator Binding", "Animator", "Host", "Bindings" })
                Assert.That(section, Does.Contain(step), "the Animator section: where the component goes and what to set on it");

            foreach (string field in new[] { "State", "Moment", "Action", "Name", "Bool Value", "Int Value", "Duration" })
                Assert.That(section, Does.Contain($"**{field}**"), "the Animator section: fields of one binding, as the Inspector names them");

            foreach (StateMoment moment in System.Enum.GetValues(typeof(StateMoment)))
                Assert.That(section, Does.Contain(moment.ToString()), "the Animator section: every moment a binding can react to");

            foreach (string action in new[] { "Set Trigger", "Set Bool", "Set Integer", "Cross Fade" })
                Assert.That(section, Does.Contain(action), "the Animator section: every action a binding can do");
        }

        [Test]
        public void GEN011_C4_Readme_AnimatorSection_SaysThatATransitionDoesNotWaitForTheAnimation()
        {
            string section = Section(@"Animator");

            Assert.That(section, Does.Match(@"(?i)(does|do) not wait for (the|an|its) animation"),
                "the Animator section: the note that a transition does not wait for the animation to finish");
        }

        [Test]
        public void GEN011_C4_Readme_AnimatorSection_ExplainsWhatHappensWhenTheGameObjectIsDisabledAndEnabled()
        {
            string section = Section(@"Animator");

            Assert.That(section, Does.Match(@"(?i)\benabled\b"), "the Animator section: Enter is applied again when the component is enabled");
            Assert.That(section, Does.Match(@"(?i)\binactive\b"), "the Animator section: what is missed while the GameObject is inactive");
            Assert.That(section, Does.Match(@"(?i)\bdropped\b"), "the Animator section: an Exit that comes while it is disabled is dropped");
            Assert.That(section, Does.Contain("CanvasGroup"), "the Animator section: keep the panel active and hide it with the animation");
        }

        [Test]
        public void GEN011_Readme_SampleSection_SaysThePanelsAreAnimated_ThroughStateAnimatorBinding()
        {
            string section = Section(@"sample");

            Assert.That(section, Does.Contain("StateAnimatorBinding"));
            Assert.That(section, Does.Contain("Animator"));
            Assert.That(section, Does.Not.Contain("show and hide"), "the old description: listeners that show and hide the panels with SetActive");
        }

        // ---------------------------------------------------------------- GEN-017 C1: the Input section

        [Test]
        public void GEN017_C1_Readme_InputSection_ExplainsHowToSetUpAnEntry()
        {
            string section = Section(@"StateInputBinding");

            foreach (string step in new[] { "Add Component > ALittleBB > State Machine > State Input Binding", "Input Action Asset" })
                Assert.That(section, Does.Contain(step), "the Input section: where the component is in the Add Component menu");

            foreach (string field in new[] { "Host", "Actions", "Entries", "States", "Action Map", "When Paused" })
                Assert.That(section, Does.Contain($"**{field}**"), "the Input section: fields as the Inspector names them");

            foreach (string choice in new[] { "Disable Map", "Keep Map Enabled" })
                Assert.That(section, Does.Contain(choice), "the Input section: the choices of When Paused");

            AssertMatch(section, @"(?i)one entry", "the Input section: states that share a map go in one entry");
            AssertMatch(section, @"(?i)not (looked up|searched)[^.\n]*parent", "the Input section: Host is not searched on the parents");
            AssertMatch(section, @"(?i)warning", "the Input section: what a missing Host, Actions or map name does");
        }

        [Test]
        public void GEN017_C1_Readme_InputSection_SaysItNeedsTheInputSystemPackage()
        {
            string section = Section(@"StateInputBinding");

            Assert.That(section, Does.Contain("`com.unity.inputsystem`"), "the Input section: the package id of the Input System");
            Assert.That(section, Does.Contain("ALittleBB.StateMachines.InputSystem"), "the Input section: the assembly of the component");
            AssertMatch(section, @"(?i)compiles only when", "the Input section: the assembly compiles only with the Input System");
            AssertMatch(section, @"(?i)component is (simply )?not there", "the Input section: without the Input System the component does not appear");
        }

        [Test]
        public void GEN017_C1_Readme_InputSection_ExplainsSwitchPushPopAndPaused()
        {
            string section = Section(@"StateInputBinding");

            AssertMatch(section, @"before `Switch`, `Push` or `Pop` returns", "the Input section: the map changes before the call returns");
            AssertMatch(section, @"(?i)`Push`[^\n]*disable", "the Input section: what a Push on top does to the map");
            AssertMatch(section, @"(?i)`Pop`[^\n]*enabled again", "the Input section: a Pop enables the map again");
            AssertMatch(section, @"(?i)paused", "the Input section: behaviour while the state is paused");
            AssertMatch(section, @"(?i)never touched", "the Input section: maps in no entry are left alone");
        }

        [Test]
        public void GEN017_C1_Readme_InputSection_SaysDisablingTheComponentDisablesItsMaps()
        {
            string section = Section(@"StateInputBinding");

            AssertMatch(section, @"(?i)disabled or destroyed[^.\n]*every (action )?map", "the Input section: disable/destroy disables every map of its list");
            AssertMatch(section, @"(?i)each time (the component|it) is enabled", "the Input section: it is synced with the stack in OnEnable");
        }

        // ---------------------------------------------------------------- GEN-017 C2: the Audio section

        [Test]
        public void GEN017_C2_Readme_AudioSection_ExplainsHowToSetUpABinding()
        {
            string section = Section(@"StateAudioBinding");

            Assert.That(section, Does.Contain("Add Component > ALittleBB > State Machine > State Audio Binding"),
                "the Audio section: where the component is in the Add Component menu");

            foreach (string field in new[] { "Host", "Audio Source", "Bindings", "State", "Moment", "Action", "Clip", "Snapshot", "Seconds" })
                Assert.That(section, Does.Contain($"**{field}**"), "the Audio section: fields as the Inspector names them");

            foreach (StateMoment moment in System.Enum.GetValues(typeof(StateMoment)))
                Assert.That(section, Does.Contain(moment.ToString()), "the Audio section: every moment a binding can react to");

            foreach (StateAudioAction action in System.Enum.GetValues(typeof(StateAudioAction)))
                Assert.That(section, Does.Contain(ObjectNames.NicifyVariableName(action.ToString())),
                    "the Audio section: every action, as the Inspector names it");

            AssertMatch(section, @"(?i)already playing[^.\n]*goes on", "the Audio section: Play Bgm of the clip that plays goes on");
            AssertMatch(section, @"(?i)warning", "the Audio section: what a missing Audio Source, Clip, Snapshot or Host does");
        }

        [Test]
        public void GEN017_C2_Readme_AudioSection_SaysTheCrossfadeUsesRealTime_AndDoesNotHoldTheTransition()
        {
            string section = Section(@"StateAudioBinding");

            AssertMatch(section, @"(?i)crossfade", "the Audio section: Play Bgm with Seconds > 0 crossfades");
            AssertMatch(section, @"(?i)unscaled", "the Audio section: fades run on unscaled (real) time");
            AssertMatch(section, @"`Time\.timeScale`", "the Audio section: what Time.timeScale does to a fade");
            AssertMatch(section, @"(?i)(does|do) not wait for (the|a|its) fade", "the Audio section: a transition does not wait for the fade");
            AssertMatch(section, @"(?i)mixer group", "the Audio section: the second Audio Source copies the settings, mixer group included");
        }

        [Test]
        public void GEN017_C2_Readme_AudioSection_SaysTheMusicGoesOnWhenTheComponentIsDisabled()
        {
            string section = Section(@"StateAudioBinding");

            AssertMatch(section, @"(?i)disabl[^.\n]*(does not stop the music|music (keeps|goes on) playing)", "the Audio section: disabling it leaves the music playing");
            AssertMatch(section, @"(?i)fade[^.\n]*ends at once", "the Audio section: a fade under way ends at its target");
            AssertMatch(section, @"(?i)each time (the component|it) is enabled", "the Audio section: it is synced with the stack in OnEnable");
            AssertMatch(section, @"(?i)current state[^.\n]*wins|wins[^.\n]*current state", "the Audio section: the music of the top state wins in the sync");
        }

        [Test]
        public void GEN017_C2_Readme_AudioSection_ExplainsHowToKeepTheBgmAcrossScenes()
        {
            string section = Section(@"StateAudioBinding");

            AssertMatch(section, @"(?i)scene", "the Audio section: music across scenes");
            Assert.That(section, Does.Contain("DontDestroyOnLoad"), "the Audio section: put the Audio Source on an object that is not destroyed");
            AssertMatch(section, @"(?i)package does not", "the Audio section: the package does not keep the music across scenes itself");
        }

        [Test]
        public void GEN017_Q005_Readme_AudioSection_SaysTheSnapshotFollowsTheMixersUpdateMode()
        {
            string section = Section(@"StateAudioBinding");

            Assert.That(section, Does.Contain("**Update Mode**"), "the Audio section: the Audio Mixer's Update Mode");
            Assert.That(section, Does.Contain("Unscaled Time"), "the Audio section: set the mixer to Unscaled Time when the game pauses time");
            AssertMatch(section, @"(?i)Normal[^.\n]*`Time\.timeScale` is 0", "the Audio section: a mixer in Normal mode stands still while Time.timeScale is 0");
        }

        [Test]
        public void GEN017_Readme_ContentsLinksTheInputAndAudioSections()
        {
            string contents = Section(@"^Contents$");

            Assert.That(contents, Does.Contain("StateInputBinding"));
            Assert.That(contents, Does.Contain("StateAudioBinding"));
        }

        static void AssertMatch(string section, string pattern, string what)
        {
            Assert.That(Regex.IsMatch(section, pattern), Is.True, $"{what} (/{pattern}/)");
        }

        /// <summary>The first section whose heading matches the pattern: its heading and everything under it.</summary>
        string Section(string headingPattern)
        {
            string[] lines = _readme.Split('\n');
            int start = -1;
            int level = 0;
            bool inCode = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("```"))
                    inCode = !inCode;

                Match heading = inCode ? Match.Empty : Regex.Match(lines[i], @"^(#+)\s+(.*)$");
                if (!heading.Success)
                    continue;

                if (start >= 0 && heading.Groups[1].Length <= level)
                    return string.Join("\n", lines, start, i - start);

                if (start < 0 && Regex.IsMatch(heading.Groups[2].Value, headingPattern, RegexOptions.IgnoreCase))
                {
                    start = i;
                    level = heading.Groups[1].Length;
                }
            }

            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"a heading of the README that matches /{headingPattern}/");
            return string.Join("\n", lines, start, lines.Length - start);
        }
    }
}
