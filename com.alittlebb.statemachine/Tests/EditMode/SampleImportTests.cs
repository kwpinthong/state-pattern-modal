using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// C1: the sample can be imported from the Package Manager and its scene opens without a missing reference.
    /// The fixture imports the sample once through the Package Manager API (what the Import button calls), opens
    /// the imported scene, and deletes the imported copy when it is done. See <see cref="ImportedSample"/>.
    /// </summary>
    public class SampleImportTests
    {
        /// <summary>GUIDs of Unity's built-in resources (default font, default UI material); they are not assets.</summary>
        const string BuiltInGuidPrefix = "0000000000000000";

        /// <summary>What a project needs for the sample, besides the sample itself. The README says the same.</summary>
        static readonly string[] AllowedPackages =
        {
            "Packages/com.alittlebb.statemachine/",
            "Packages/com.unity.ugui/",
            "Packages/com.unity.inputsystem/"
        };

        readonly List<string> _problemsLogged = new List<string>();
        Scene _scene;

        [OneTimeSetUp]
        public void ImportTheSample_AndOpenItsScene()
        {
            Application.logMessageReceived += RecordProblem;
            try
            {
                ImportedSample.Import();
                _scene = EditorSceneManager.OpenPreviewScene(ImportedSample.ScenePath);
            }
            catch
            {
                RemoveTheImportedSample();
                throw;
            }
        }

        [OneTimeTearDown]
        public void RemoveTheImportedSample()
        {
            Application.logMessageReceived -= RecordProblem;
            if (_scene.IsValid())
                EditorSceneManager.ClosePreviewScene(_scene);

            _scene = default;
            ImportedSample.Remove();
        }

        [Test]
        public void C1_PackageManager_ListsTheSample_WithItsScene()
        {
            Sample sample = ImportedSample.Find();

            Assert.That(sample.description, Is.Not.Empty, "description shown next to the Import button");
            Assert.That(File.Exists(Path.Combine(sample.resolvedPath, ImportedSample.SceneFileName)), Is.True,
                $"'{ImportedSample.SceneFileName}' in the sample folder of the package ('{sample.resolvedPath}')");
            Assert.That(sample.isImported, Is.True, "the Package Manager sees the copy this fixture imported");
        }

        /// <summary>
        /// Unity does not make .meta files in the hidden Samples~ folder, and a file imported without one gets a
        /// new GUID, which breaks every reference to it. A project that imported the sample before gives the file
        /// its old GUID back, so importing here cannot show that a .meta is missing: check the files.
        /// </summary>
        [Test]
        public void C1_EveryFileOfTheSample_InThePackage_HasItsMeta_SoReferencesSurviveTheImport()
        {
            string folder = ImportedSample.Find().resolvedPath;

            string[] withoutMeta = Directory.GetFileSystemEntries(folder, "*", SearchOption.AllDirectories)
                .Where(entry => !entry.EndsWith(".meta", StringComparison.Ordinal) && !File.Exists(entry + ".meta"))
                .Select(entry => entry.Substring(folder.Length).TrimStart('\\', '/'))
                .ToArray();

            Assert.That(withoutMeta, Is.Empty, $"files and folders of '{folder}' without a .meta");
        }

        [Test]
        public void C1_ImportedScene_Opens_WithTheHostTheCanvasAndTheEventSystem()
        {
            Assert.That(_scene.IsValid() && _scene.isLoaded, Is.True, $"'{ImportedSample.ScenePath}' is open");

            Assert.That(_scene.GetRootGameObjects().Select(root => root.name),
                Is.SupersetOf(new[] { "State Machine", "Canvas", "EventSystem" }));
        }

        [Test]
        public void C1_ImportedScene_HasNoMissingScript()
        {
            string[] broken = AllGameObjects()
                .Where(gameObject => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                .Select(PathOf)
                .ToArray();

            Assert.That(broken, Is.Empty, "GameObjects of the sample scene with a missing script");

            string[] stateIds = AssetDatabase.FindAssets("t:StateId", new[] { ImportedSample.FolderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(stateIds, Is.EqualTo(new[] { "Gameplay", "MainMenu", "Pause" }), "StateId assets of the imported sample");
        }

        [Test]
        public void C1_ImportedSample_HasNoMissingReference_ToAnotherAsset()
        {
            var missing = new List<string>();
            foreach (KeyValuePair<string, string> reference in AssetReferencesOfTheSample())
            {
                if (ExistingAssetPath(reference.Key) == null)
                    missing.Add($"{reference.Value} → guid {reference.Key}");
            }

            Assert.That(missing, Is.Empty, "references of the imported sample to an asset that is not in the project");
        }

        [Test]
        public void C1_ImportedScene_HasItsHostListenersAndButtonsWired()
        {
            StateMachineHost[] hosts = ComponentsInScene<StateMachineHost>();
            Assert.That(hosts.Length, Is.EqualTo(1), "hosts in the sample scene");
            Assert.That(hosts[0].InitialState, Is.Not.Null, "Initial State of the host");

            StateListener[] listeners = ComponentsInScene<StateListener>();
            Assert.That(listeners.Length, Is.EqualTo(3), "listeners in the sample scene (Main Menu, Gameplay HUD, Pause Popup)");

            var problems = new List<string>();
            foreach (StateListener listener in listeners)
            {
                string who = PathOf(listener.gameObject);
                if (listener.Host != hosts[0])
                    problems.Add($"{who}: Host is not the host of the scene");
                if (listener.States.Count == 0 || listener.States.Any(state => state == null))
                    problems.Add($"{who}: States is empty or has an empty slot");
                if (!listener.gameObject.activeInHierarchy)
                    problems.Add($"{who}: starts inactive, so it would not subscribe");

                problems.AddRange(BrokenCalls(listener.OnEnter, who + " On Enter", mustHaveCalls: true));
                problems.AddRange(BrokenCalls(listener.OnExit, who + " On Exit", mustHaveCalls: true));
                problems.AddRange(BrokenCalls(listener.OnPaused, who + " On Paused", mustHaveCalls: false));
                problems.AddRange(BrokenCalls(listener.OnResumed, who + " On Resumed", mustHaveCalls: false));
            }

            foreach (UnityEngine.UI.Button button in ComponentsInScene<UnityEngine.UI.Button>())
                problems.AddRange(BrokenCalls(button.onClick, PathOf(button.gameObject) + " On Click", mustHaveCalls: true));

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void C1_ImportedSample_NeedsOnly_ThisPackage_UGUI_AndTheInputSystem()
        {
            string sampleFolder = ImportedSample.FolderPath + "/";
            var others = new List<string>();
            foreach (KeyValuePair<string, string> reference in AssetReferencesOfTheSample())
            {
                string path = ExistingAssetPath(reference.Key);
                if (path == null || path.StartsWith(sampleFolder, StringComparison.Ordinal))
                    continue;

                if (!AllowedPackages.Any(package => path.StartsWith(package, StringComparison.Ordinal)))
                    others.Add($"{reference.Value} → {path}");
            }

            Assert.That(others, Is.Empty,
                "references of the sample to something a fresh project may not have (TextMeshPro, a render pipeline, an asset of this project)");
        }

        [Test]
        public void C1_ImportingTheSample_AndOpeningItsScene_LogsNoErrorOrWarning()
        {
            Assert.That(_problemsLogged, Is.Empty);
        }

        // ---------------------------------------------------------------- helpers

        void RecordProblem(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Log)
                _problemsLogged.Add($"{type}: {message}");
        }

        IEnumerable<GameObject> AllGameObjects()
        {
            return _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject);
        }

        T[] ComponentsInScene<T>() where T : Component
        {
            return _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        /// <summary>
        /// Every asset GUID written in the files of the imported sample (the scene and the StateId assets),
        /// with the file it is written in. A reference from one file to another is always by GUID, so this is
        /// what breaks when a .meta file is lost on import or a package is not installed.
        /// </summary>
        static Dictionary<string, string> AssetReferencesOfTheSample()
        {
            var references = new Dictionary<string, string>();
            string folder = ImportedSample.FolderPath;
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".unity", StringComparison.Ordinal) && !file.EndsWith(".asset", StringComparison.Ordinal))
                    continue;

                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"guid: ([0-9a-f]{32})"))
                {
                    string guid = match.Groups[1].Value;
                    if (!guid.StartsWith(BuiltInGuidPrefix, StringComparison.Ordinal))
                        references[guid] = Path.GetFileName(file);
                }
            }

            Assert.That(references, Is.Not.Empty, $"asset references found in '{folder}'");
            return references;
        }

        static string ExistingAssetPath(string guid)
        {
            // GUIDToAssetPath also answers for an asset that was deleted, with the path it had: ask the path back.
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool exists = !string.IsNullOrEmpty(path) &&
                          AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) == guid;
            return exists ? path : null;
        }

        /// <summary>Entries of a UnityEvent, as set in the Inspector, that have no target or name a method the target does not have.</summary>
        static IEnumerable<string> BrokenCalls(UnityEventBase unityEvent, string what, bool mustHaveCalls)
        {
            int count = unityEvent.GetPersistentEventCount();
            if (count == 0 && mustHaveCalls)
                yield return $"{what}: no entry";

            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object target = unityEvent.GetPersistentTarget(i);
                string method = unityEvent.GetPersistentMethodName(i);
                if (target == null)
                    yield return $"{what} [{i}]: no target";
                else if (string.IsNullOrEmpty(method) || target.GetType().GetMethods().All(candidate => candidate.Name != method))
                    yield return $"{what} [{i}]: {target.GetType().Name} has no method '{method}'";
            }
        }

        static string PathOf(GameObject gameObject)
        {
            string path = gameObject.name;
            for (Transform parent = gameObject.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;

            return path;
        }
    }
}
