using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ALittleBB.StateMachines.Tests
{
    public class PackageSmokeTests
    {
        const string RuntimeAssemblyName = "ALittleBB.StateMachines";
        const string EditorAssemblyName = "ALittleBB.StateMachines.Editor";

        [Test]
        public void PackageAssemblies_AreLoaded_AndRuntimeHasNoUnityEditorReference()
        {
            Assembly runtime = LoadAssemblyOrNull(RuntimeAssemblyName);
            Assembly editor = LoadAssemblyOrNull(EditorAssemblyName);

            Assert.That(runtime, Is.Not.Null, $"Assembly '{RuntimeAssemblyName}' is not loaded in the Editor.");
            Assert.That(editor, Is.Not.Null, $"Assembly '{EditorAssemblyName}' is not loaded in the Editor.");

            string[] editorReferences = runtime.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("UnityEditor", StringComparison.Ordinal))
                .ToArray();

            Assert.That(editorReferences, Is.Empty, $"'{RuntimeAssemblyName}' must not reference UnityEditor.");
        }

        static Assembly LoadAssemblyOrNull(string name)
        {
            try
            {
                return Assembly.Load(name);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }
    }
}
