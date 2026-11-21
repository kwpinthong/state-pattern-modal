using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>GEN-018 C1 and C8: the Time Scale choice of a binding in the Inspector, and the tooltips that explain it.</summary>
    public class StateAudioSnapshotTimeScaleInspectorTests
    {
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
        public void GEN018_C1_ABinding_HasATimeScaleChoice_IgnoreOrFollow_InTheInspector()
        {
            SerializedProperty entry = NewEntryInTheInspector(out _);
            SerializedProperty timeScale = entry.FindPropertyRelative("_timeScale");

            Assert.That(timeScale, Is.Not.Null, "a binding has a Time Scale field in the Inspector");
            Assert.That(timeScale.displayName, Is.EqualTo("Time Scale"));
            Assert.That(timeScale.enumDisplayNames, Is.EqualTo(new[] { "Ignore Time Scale", "Follow Time Scale" }), "the two choices");
        }

        [Test]
        public void GEN018_C1_ANewBinding_IgnoresTimeScale()
        {
            SerializedProperty entry = NewEntryInTheInspector(out StateAudioBinding binding);

            Assert.That(entry.FindPropertyRelative("_timeScale").enumValueIndex, Is.EqualTo((int)StateAudioTimeScale.IgnoreTimeScale),
                "an entry added with + in the Inspector");
            Assert.That(binding.Bindings[0].TimeScale, Is.EqualTo(StateAudioTimeScale.IgnoreTimeScale));
            Assert.That(new StateAudioBinding.Entry().TimeScale, Is.EqualTo(StateAudioTimeScale.IgnoreTimeScale), "an entry made from code");
        }

        [Test]
        public void GEN018_C1_ABindingSavedBeforeTheChoiceExisted_LoadsAsIgnore()
        {
            // A binding serialized by GEN-014..GEN-017 has no _timeScale field. Loaded into a new component, as when
            // a scene or prefab saved then is opened.
            StateAudioBinding saved = Create(new GameObject("Saved")).AddComponent<StateAudioBinding>();
            saved.Bindings = new[] { new StateAudioBinding.Entry { Moment = StateMoment.Paused, Action = StateAudioAction.TransitionToSnapshot, Seconds = 0.3f, TimeScale = StateAudioTimeScale.FollowTimeScale } };
            string json = EditorJsonUtility.ToJson(saved);
            Assert.That(json, Does.Contain("\"_timeScale\":1"), "Given: the field is in the saved data");
            string old = Regex.Replace(json, ",?\"_timeScale\":1", "");
            Assert.That(old, Does.Not.Contain("_timeScale"), "Given: the data as saved before the field existed");

            StateAudioBinding binding = Create(new GameObject("Music")).AddComponent<StateAudioBinding>();
            EditorJsonUtility.FromJsonOverwrite(old, binding);

            Assert.That(binding.Bindings, Has.Count.EqualTo(1));
            Assert.That(binding.Bindings[0].Action, Is.EqualTo(StateAudioAction.TransitionToSnapshot), "Given: the old binding is loaded");
            Assert.That(binding.Bindings[0].TimeScale, Is.EqualTo(StateAudioTimeScale.IgnoreTimeScale));
        }

        [Test]
        public void GEN018_C8_TheTooltipOfSeconds_ExplainsTheTwoTimeScales_NotTheMixersUpdateMode()
        {
            string tooltip = Tooltip("_seconds");

            Assert.That(tooltip, Does.Not.Contain("Update Mode"), "the old text: a snapshot transition follows the mixer's Update Mode");
            Assert.That(tooltip, Does.Contain("Time Scale"), "it points to the Time Scale choice");
            Assert.That(tooltip, Does.Contain("0 changes at once"));
        }

        [Test]
        public void GEN018_C8_TheTooltipOfTimeScale_ExplainsBothChoices()
        {
            string tooltip = Tooltip("_timeScale");

            Assert.That(tooltip, Does.Contain("Ignore Time Scale"));
            Assert.That(tooltip, Does.Contain("Follow Time Scale"));
            Assert.That(tooltip, Does.Contain("Transition To Snapshot"), "what it applies to");
            Assert.That(tooltip, Does.Contain("Update Mode"), "it says the mixer's own Update Mode does not matter");
        }

        static string Tooltip(string field)
        {
            FieldInfo info = typeof(StateAudioBinding.Entry).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, field);
            var tooltip = info.GetCustomAttribute<TooltipAttribute>();
            Assert.That(tooltip, Is.Not.Null, $"{field} has a tooltip");
            return tooltip.tooltip;
        }

        SerializedProperty NewEntryInTheInspector(out StateAudioBinding binding)
        {
            binding = ObjectFactory.AddComponent<StateAudioBinding>(Create(new GameObject("Music")));
            var inspector = new SerializedObject(binding);
            SerializedProperty bindings = inspector.FindProperty("_bindings");
            bindings.arraySize = 1;
            inspector.ApplyModifiedPropertiesWithoutUndo();
            return bindings.GetArrayElementAtIndex(0);
        }

        T Create<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
