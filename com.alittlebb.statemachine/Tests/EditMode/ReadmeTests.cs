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
