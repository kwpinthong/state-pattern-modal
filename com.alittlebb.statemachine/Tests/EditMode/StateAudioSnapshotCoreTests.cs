using System.IO;
using NUnit.Framework;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-016 C5: the snapshot action lives in StateAudioBinding and reaches the machine through the Listener API
    /// only; the core knows nothing of audio. The other half of C5, that no core file is in the diff of the task and
    /// the earlier tests all pass, is checked on the diff and the full test runs.
    /// </summary>
    public class StateAudioSnapshotCoreTests
    {
        const string Runtime = "Packages/com.alittlebb.statemachine/Runtime/";

        [TestCase("IState.cs")]
        [TestCase("StateChangeKind.cs")]
        [TestCase("StateChanged.cs")]
        [TestCase("StateId.cs")]
        [TestCase("StateListener.cs")]
        [TestCase("StateListenerStatus.cs")]
        [TestCase("StateMachine.cs")]
        [TestCase("StateMachineHost.cs")]
        [TestCase("StateMoment.cs")]
        [TestCase("StateSubscription.cs")]
        public void GEN016_C5_TheCoreFile_KnowsNothingOfAudio(string file)
        {
            string path = Path.GetFullPath(Runtime + file);
            Assert.That(File.Exists(path), Is.True, path);

            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("Audio"), $"{file}: no Audio Mixer, Audio Source or StateAudioBinding in the core");
        }
    }
}
