using UnityEngine.TestTools;
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
#endif

namespace ALittleBB.StateMachines.Tests
{
#if UNITY_EDITOR
    /// <summary>
    /// The package's sample, imported into Assets/ for a test run and removed after it.
    /// <para>A sample lives in the hidden <c>Samples~</c> folder, so Unity cannot load its scene from the package.
    /// The tests import it the way the Import button of the Package Manager window does
    /// (<see cref="Sample.Import"/>), which copies it to <c>Assets/Samples/&lt;package&gt;/&lt;version&gt;/&lt;sample&gt;</c>,
    /// and delete that copy when they are done.</para>
    /// <para>They never use or delete a copy they did not import: the copy they make is marked with a file,
    /// and <see cref="Import"/> refuses to run while another copy of the sample is in the project
    /// (two copies would share the same asset GUIDs).</para>
    /// </summary>
    public static class ImportedSample
    {
        public const string PackageName = "com.alittlebb.statemachine";

        /// <summary>Display name of the sample in package.json, shown in the Package Manager window.</summary>
        public const string DisplayName = "Menu, Gameplay and Pause";

        public const string SceneFileName = "MenuGameplayPause.unity";

        /// <summary>Unity ignores files that start with a dot, so the marker is not an asset.</summary>
        const string MarkerFileName = ".imported-by-tests";

        const string FailureKey = "ALittleBB.StateMachines.Tests.ImportedSample.Failure";

        /// <summary>
        /// Why <see cref="TryImport"/> failed, or null when it worked. Kept across the domain reload
        /// between the import (Edit mode) and the PlayMode tests.
        /// </summary>
        public static string Failure
        {
            get
            {
                string failure = SessionState.GetString(FailureKey, "");
                return failure.Length > 0 ? failure : null;
            }
        }

        /// <summary>Asset path of the folder the Package Manager imports the sample to.</summary>
        public static string FolderPath => ToAssetPath(Find().importPath);

        /// <summary>Asset path of the imported sample scene.</summary>
        public static string ScenePath => FolderPath + "/" + SceneFileName;

        /// <summary>True when the copy in <see cref="FolderPath"/> is one these tests imported.</summary>
        public static bool IsImportedByTests => File.Exists(MarkerPath(Find()));

        /// <summary>The sample as the Package Manager window lists it. Throws when package.json does not list it.</summary>
        public static Sample Find()
        {
            Sample[] samples = Sample.FindByPackage(PackageName, null).ToArray();
            foreach (Sample sample in samples)
            {
                if (sample.displayName == DisplayName)
                    return sample;
            }

            throw new InvalidOperationException(
                $"The Package Manager does not list a sample named '{DisplayName}' for {PackageName}. " +
                $"Samples it lists: [{string.Join(", ", samples.Select(sample => sample.displayName))}]. " +
                "Check the \"samples\" entry of package.json.");
        }

        /// <summary>
        /// Imports the sample like the Import button does. Throws with the reason when it cannot:
        /// the sample is not listed, or a copy that these tests did not import is already in the project.
        /// </summary>
        public static void Import()
        {
            Sample sample = Find();
            string folder = ToAssetPath(sample.importPath);

            if (Directory.Exists(sample.importPath))
            {
                if (!File.Exists(MarkerPath(sample)))
                {
                    throw new InvalidOperationException(
                        $"The sample is already imported at '{folder}' and that copy was not made by these tests, " +
                        "so they will not replace or delete it. Delete it (or move it out of Assets/) and run the tests again.");
                }

                // Left behind by a test run that was stopped before it could clean up.
                Delete(sample);
            }

            string otherCopy = FindCopyInProject(sample);
            if (otherCopy != null)
            {
                throw new InvalidOperationException(
                    $"A copy of the sample is already in the project ('{otherCopy}'). A second import would share its " +
                    "asset GUIDs, and the tests would then run against that copy. Delete it (or move it out of Assets/) " +
                    "and run the tests again.");
            }

            if (!sample.Import(Sample.ImportOptions.HideImportWindow))
                throw new InvalidOperationException($"The Package Manager could not import '{DisplayName}' to '{folder}'.");

            File.WriteAllText(MarkerPath(sample),
                "Imported by the tests of com.alittlebb.statemachine. They delete this folder when the run ends.");
        }

        /// <summary>
        /// <see cref="Import"/> for a prebuild setup, which must not throw: the reason is kept in
        /// <see cref="Failure"/> for the tests to report.
        /// </summary>
        public static void TryImport()
        {
            SessionState.EraseString(FailureKey);
            try
            {
                Import();
            }
            catch (Exception exception)
            {
                SessionState.SetString(FailureKey, exception.Message);
            }
        }

        /// <summary>Deletes the copy these tests imported, and the folders above it that are left empty.</summary>
        public static void Remove()
        {
            SessionState.EraseString(FailureKey);

            Sample[] samples = Sample.FindByPackage(PackageName, null)
                .Where(sample => sample.displayName == DisplayName)
                .ToArray();
            if (samples.Length == 0)
                return;

            Sample imported = samples[0];
            if (!Directory.Exists(imported.importPath) || !File.Exists(MarkerPath(imported)))
                return;

            Delete(imported);
        }

        static void Delete(Sample sample)
        {
            File.Delete(MarkerPath(sample));
            string folder = ToAssetPath(sample.importPath);
            AssetDatabase.DeleteAsset(folder);

            // Assets/Samples/<package>/<version>: delete what the import created and nothing else needs.
            string parent = ParentOf(folder);
            while (parent != "Assets" && AssetDatabase.IsValidFolder(parent) &&
                   !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                AssetDatabase.DeleteAsset(parent);
                parent = ParentOf(parent);
            }
        }

        /// <summary>Path of an asset in the project that has the GUID of one of the sample's files, or null.</summary>
        static string FindCopyInProject(Sample sample)
        {
            foreach (string meta in Directory.GetFiles(sample.resolvedPath, "*.meta", SearchOption.AllDirectories))
            {
                Match guid = Regex.Match(File.ReadAllText(meta), @"^guid: ([0-9a-f]{32})", RegexOptions.Multiline);
                if (!guid.Success)
                    continue;

                // GUIDToAssetPath also answers for an asset that was deleted, with the path it had: ask the path back.
                string path = AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value);
                if (!string.IsNullOrEmpty(path) &&
                    AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets) == guid.Groups[1].Value)
                {
                    return path;
                }
            }

            return null;
        }

        static string MarkerPath(Sample sample) => Path.Combine(sample.importPath, MarkerFileName);

        static string ToAssetPath(string absolutePath)
        {
            string path = absolutePath.Replace('\\', '/');
            string assets = Application.dataPath.Replace('\\', '/');
            return path.StartsWith(assets, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + path.Substring(assets.Length)
                : path;
        }

        static string ParentOf(string assetPath) => assetPath.Substring(0, assetPath.LastIndexOf('/'));
    }
#endif

    /// <summary>
    /// Put on a PlayMode test class with <c>[PrebuildSetup]</c> and <c>[PostBuildCleanup]</c>: imports the sample
    /// before the Editor enters Play mode (assets cannot be imported from inside a test) and removes it after the run.
    /// </summary>
    public class ImportedSampleSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            ImportedSample.TryImport();
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            ImportedSample.Remove();
#endif
        }
    }
}
