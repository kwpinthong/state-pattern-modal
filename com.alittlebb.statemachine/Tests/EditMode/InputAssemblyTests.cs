using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-012 C1: the Input System is optional. The main assemblies of the package do not reference it, and the
    /// assemblies of <c>StateInputBinding</c> and of its tests compile only when the project has com.unity.inputsystem.
    /// These tests live in the main EditMode assembly, so they also run in a project without the Input System.
    /// </summary>
    public class InputAssemblyTests
    {
        const string PackageRoot = "Packages/com.alittlebb.statemachine";
        const string InputSystemPackage = "com.unity.inputsystem";
        const string InputSystemAssembly = "Unity.InputSystem";
        const string InputSystemAssemblyGuid = "75469ad4d38634e559750d17036d5f7c";

        const string InputAsmdef = PackageRoot + "/Runtime/InputSystem/ALittleBB.StateMachines.InputSystem.asmdef";
        const string InputTestsAsmdef = PackageRoot + "/Tests/InputSystem/ALittleBB.StateMachines.Tests.InputSystem.asmdef";

        [Serializable]
        class VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }

        [Serializable]
        class Asmdef
        {
            public string name;
            public string[] references = Array.Empty<string>();
            public string[] defineConstraints = Array.Empty<string>();
            public VersionDefine[] versionDefines = Array.Empty<VersionDefine>();
        }

        static Asmdef Read(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Assert.That(File.Exists(fullPath), Is.True, $"'{path}' exists");
            return JsonUtility.FromJson<Asmdef>(File.ReadAllText(fullPath));
        }

        static bool IsInputSystem(string reference) =>
            reference == InputSystemAssembly || reference == "GUID:" + InputSystemAssemblyGuid;

        [TestCase(PackageRoot + "/Runtime/ALittleBB.StateMachines.asmdef")]
        [TestCase(PackageRoot + "/Editor/ALittleBB.StateMachines.Editor.asmdef")]
        public void C1_MainAssembly_DoesNotReferenceTheInputSystem(string path)
        {
            Asmdef asmdef = Read(path);

            Assert.That(asmdef.references.Where(IsInputSystem), Is.Empty, $"references of '{asmdef.name}'");
            Assert.That(asmdef.defineConstraints, Is.Empty, $"'{asmdef.name}' always compiles");
        }

        [Test]
        public void C1_CompiledMainAssembly_DoesNotReferenceTheInputSystem()
        {
            Assembly runtime = typeof(StateMachine).Assembly;

            Assert.That(
                runtime.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.No.Member(InputSystemAssembly),
                $"assemblies referenced by '{runtime.GetName().Name}'");
        }

        [TestCase(InputAsmdef)]
        [TestCase(InputTestsAsmdef)]
        public void C1_InputAssembly_CompilesOnlyWhenTheProjectHasTheInputSystem(string path)
        {
            Asmdef asmdef = Read(path);

            Assert.That(asmdef.references.Any(IsInputSystem), Is.True, $"'{asmdef.name}' references {InputSystemAssembly}");

            VersionDefine[] fromInputSystem = asmdef.versionDefines
                .Where(versionDefine => versionDefine.name == InputSystemPackage && !string.IsNullOrEmpty(versionDefine.define))
                .ToArray();
            Assert.That(fromInputSystem, Has.Length.EqualTo(1),
                $"'{asmdef.name}' has one Version Define that is set only when {InputSystemPackage} is installed");

            Assert.That(asmdef.defineConstraints, Has.Member(fromInputSystem[0].define),
                $"'{asmdef.name}' compiles only when '{fromInputSystem[0].define}' is defined");
        }
    }
}
