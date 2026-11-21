using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>GEN-014 C1: StateAudioBinding can be added and set up from the Inspector, without any extra package.</summary>
    public class StateAudioBindingInspectorTests
    {
        const string PackageJsonPath = "Packages/com.alittlebb.statemachine/package.json";

        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void C1_Package_NeedsNoExtraPackage_OnlyBuiltInModules()
        {
            string json = File.ReadAllText(Path.GetFullPath(PackageJsonPath));
            Match dependencies = Regex.Match(json, "\"dependencies\"\\s*:\\s*\\{(?<body>[^}]*)\\}");
            Assert.That(dependencies.Success, Is.True, "package.json has a dependencies block");

            string[] names = Regex.Matches(dependencies.Groups["body"].Value, "\"(?<name>[^\"]+)\"\\s*:")
                .Cast<Match>()
                .Select(match => match.Groups["name"].Value)
                .ToArray();

            Assert.That(names, Is.All.StartsWith("com.unity.modules."), "only Unity's built-in modules");
            Assert.That(names, Does.Contain("com.unity.modules.audio"), "the Audio module that AudioSource and AudioMixerSnapshot come from is declared");
        }

        [Test]
        public void C1_AddComponentMenu_ListsStateAudioBinding_UnderALittleBBStateMachine()
        {
            var menu = (AddComponentMenu)System.Attribute.GetCustomAttribute(typeof(StateAudioBinding), typeof(AddComponentMenu));

            Assert.That(menu, Is.Not.Null, "StateAudioBinding has an Add Component menu entry");
            Assert.That(menu.componentMenu, Is.EqualTo("ALittleBB/State Machine/State Audio Binding"));
        }

        [Test]
        public void C1_AddComponent_ThenHostAudioSourceAndABinding_SetInTheInspector_AreWhatTheComponentUses()
        {
            GameObject gameObject = Create(new GameObject("Music"));
            StateMachineHost host = Create(new GameObject("Host")).AddComponent<StateMachineHost>();
            AudioSource source = gameObject.AddComponent<AudioSource>();
            StateId state = Create(ScriptableObject.CreateInstance<StateId>());
            AudioClip clip = Create(AudioClip.Create("Theme", 4410, 1, 44100, false));

            // Add Component: the script of the component is a MonoScript the Editor can add, and adding it works.
            StateAudioBinding binding = ObjectFactory.AddComponent<StateAudioBinding>(gameObject);
            MonoScript script = MonoScript.FromMonoBehaviour(binding);
            Assert.That(script, Is.Not.Null, "the component has its own script file");
            Assert.That(script.GetClass(), Is.EqualTo(typeof(StateAudioBinding)), "the script file is named after the component");

            // Inspector: set every field through the serialized properties the Inspector draws.
            var inspector = new SerializedObject(binding);
            SerializedProperty hostField = inspector.FindProperty("_host");
            SerializedProperty sourceField = inspector.FindProperty("_audioSource");
            SerializedProperty bindingsField = inspector.FindProperty("_bindings");
            Assert.That(hostField, Is.Not.Null, "Host is in the Inspector");
            Assert.That(sourceField, Is.Not.Null, "Audio Source is in the Inspector");
            Assert.That(bindingsField, Is.Not.Null, "Bindings is in the Inspector");

            hostField.objectReferenceValue = host;
            sourceField.objectReferenceValue = source;
            bindingsField.arraySize = 1;
            SerializedProperty entry = bindingsField.GetArrayElementAtIndex(0);
            SerializedProperty stateField = entry.FindPropertyRelative("_state");
            SerializedProperty momentField = entry.FindPropertyRelative("_moment");
            SerializedProperty actionField = entry.FindPropertyRelative("_action");
            SerializedProperty clipField = entry.FindPropertyRelative("_clip");
            SerializedProperty snapshotField = entry.FindPropertyRelative("_snapshot");
            SerializedProperty secondsField = entry.FindPropertyRelative("_seconds");
            Assert.That(new[] { stateField, momentField, actionField, clipField, snapshotField, secondsField }, Is.All.Not.Null,
                "a binding has State, Moment, Action, Clip, Snapshot and Seconds in the Inspector");

            Assert.That(momentField.enumNames, Is.EqualTo(new[] { "Enter", "Exit", "Paused", "Resumed" }), "the moments of StateAnimatorBinding");
            Assert.That(actionField.enumDisplayNames, Is.EqualTo(new[] { "Play Bgm", "Stop Bgm", "Transition To Snapshot" }), "the three actions");
            Assert.That(snapshotField.type, Does.Contain(nameof(AudioMixerSnapshot)), "the snapshot field takes an Audio Mixer Snapshot");

            stateField.objectReferenceValue = state;
            momentField.enumValueIndex = (int)StateMoment.Paused;
            actionField.enumValueIndex = (int)StateAudioAction.PlayBgm;
            clipField.objectReferenceValue = clip;
            secondsField.floatValue = 0f;
            inspector.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(binding.Host, Is.SameAs(host));
            Assert.That(binding.AudioSource, Is.SameAs(source));
            Assert.That(binding.Bindings, Has.Count.EqualTo(1));
            StateAudioBinding.Entry set = binding.Bindings[0];
            Assert.That(set.State, Is.SameAs(state));
            Assert.That(set.Moment, Is.EqualTo(StateMoment.Paused));
            Assert.That(set.Action, Is.EqualTo(StateAudioAction.PlayBgm));
            Assert.That(set.Clip, Is.SameAs(clip));
            Assert.That(set.Snapshot, Is.Null);
            Assert.That(set.Seconds, Is.EqualTo(0f));
        }

        T Create<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
