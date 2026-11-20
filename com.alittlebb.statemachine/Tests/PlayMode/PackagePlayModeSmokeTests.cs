using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ALittleBB.StateMachines.Tests
{
    public class PackagePlayModeSmokeTests
    {
        const string RuntimeAssemblyName = "ALittleBB.StateMachines";

        [UnityTest]
        public IEnumerator RuntimeAssembly_IsLoaded_InPlayMode()
        {
            yield return null;

            Assert.That(Application.isPlaying, Is.True, "PlayMode smoke test must run in Play Mode.");
            Assert.That(LoadAssemblyOrNull(RuntimeAssemblyName), Is.Not.Null,
                $"Assembly '{RuntimeAssemblyName}' is not loaded in Play Mode.");
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
