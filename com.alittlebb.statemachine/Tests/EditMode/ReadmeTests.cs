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
