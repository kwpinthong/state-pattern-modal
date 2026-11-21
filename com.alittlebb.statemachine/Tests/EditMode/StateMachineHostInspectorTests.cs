using System.Linq;
using ALittleBB.StateMachines.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// C5: what the Inspector of a <see cref="StateMachineHost"/> shows for the stack. The tests build the real
    /// Inspector content (the one Unity puts in the Inspector window) and read its rows.
    /// </summary>
    public class StateMachineHostInspectorTests
    {
        StateId _a;
        StateId _b;
        StateId _c;
        GameObject _hostObject;
        StateMachineHost _host;
        UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _c = CreateStateId("C");
            _hostObject = new GameObject("Host");
            _host = _hostObject.AddComponent<StateMachineHost>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
                Object.DestroyImmediate(_editor);

            Object.DestroyImmediate(_hostObject);
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Object.DestroyImmediate(_c);
        }

        [Test]
        public void C5_Inspector_OfAHostWithStackAB_ShowsTheStack_TopToBottom()
        {
            _host.Switch(_a);
            _host.Push(_b);

            VisualElement inspector = OpenInspector();

            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "B", "A" }));
            Assert.That(StatusesShownIn(inspector), Is.EqualTo(new[] { "Current", "Paused" }));
            Assert.That(StackSectionOf(inspector).style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void C5_Inspector_ThatIsAlreadyOpen_FollowsTheStack_WhenItChanges()
        {
            _host.Switch(_a);
            VisualElement inspector = OpenInspector();
            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "A" }));

            _host.Push(_b);
            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "B", "A" }));

            _host.Push(_c);
            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "C", "B", "A" }));
            Assert.That(StatusesShownIn(inspector), Is.EqualTo(new[] { "Current", "Paused", "Paused" }));

            _host.Pop();
            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "B", "A" }));

            _host.Switch(_c);
            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "C" }));
        }

        [Test]
        public void C5_Inspector_OutsidePlayMode_WithAnEmptyStack_HidesTheStackSection()
        {
            Assume.That(Application.isPlaying, Is.False);

            VisualElement inspector = OpenInspector();

            Assert.That(StatesShownIn(inspector), Is.Empty);
            Assert.That(StackSectionOf(inspector).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void C5_Inspector_ThatWasClosed_NoLongerListensToTheMachine()
        {
            _host.Switch(_a);
            VisualElement inspector = OpenInspector();

            Object.DestroyImmediate(_editor);
            _host.Push(_b);

            Assert.That(StatesShownIn(inspector), Is.EqualTo(new[] { "A" }));
        }

        StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            return id;
        }

        /// <summary>Creates the custom Editor Unity uses for the host and builds its Inspector content.</summary>
        VisualElement OpenInspector()
        {
            _editor = UnityEditor.Editor.CreateEditor(_host);
            Assert.That(_editor, Is.InstanceOf<StateMachineHostEditor>(), "Unity picks the package's Editor for a StateMachineHost");

            VisualElement inspector = _editor.CreateInspectorGUI();
            Assert.That(inspector, Is.Not.Null);
            return inspector;
        }

        static VisualElement StackSectionOf(VisualElement inspector)
        {
            VisualElement section = inspector.Q(StateMachineHostEditor.StackSectionName);
            Assert.That(section, Is.Not.Null, "the Inspector has a stack section");
            return section;
        }

        /// <summary>State names in the stack section, in the order they are laid out (first = top row).</summary>
        static string[] StatesShownIn(VisualElement inspector) =>
            StackSectionOf(inspector).Query<Label>(className: StateMachineHostEditor.StackStateClass)
                .ToList().Select(label => label.text).ToArray();

        static string[] StatusesShownIn(VisualElement inspector) =>
            StackSectionOf(inspector).Query<Label>(className: StateMachineHostEditor.StackStatusClass)
                .ToList().Select(label => label.text).ToArray();
    }
}
