using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-012: <see cref="StateInputBinding"/> turns the action maps of an <see cref="InputActionAsset"/> on and off
    /// with the states of a <see cref="StateMachineHost"/>. Key presses are simulated with the Input System's
    /// <see cref="InputTestFixture"/>, which gives every test its own Input System with a test keyboard.
    /// C1 (the assemblies) is in <c>InputAssemblyTests</c> of the main EditMode assembly.
    /// The tests of GEN-013 (Push/Pop, When Paused, late sync, disable) are in <c>StateInputBindingTests.Gen013.cs</c>.
    /// </summary>
    public partial class StateInputBindingTests : InputTestFixture
    {
        // Every map has an action: InputActionMap.enabled is true only while one of its actions is enabled.
        const string Gameplay = "Gameplay"; // Jump: Space
        const string Common = "Common";     // Run: Left Shift
        const string Debug = "Debug";       // Toggle: F1

        readonly List<Object> _created = new List<Object>();
        Keyboard _keyboard;
        InputActionAsset _asset;
        InputAction _jump;
        InputAction _run;
        InputAction _toggle;
        StateMachineHost _host;
        StateId _a;
        StateId _b;
        StateId _c;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            _asset = Created(ScriptableObject.CreateInstance<InputActionAsset>());
            _asset.name = "TestActions";
            _jump = _asset.AddActionMap(Gameplay).AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _run = _asset.AddActionMap(Common).AddAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            _toggle = _asset.AddActionMap(Debug).AddAction("Toggle", InputActionType.Button, "<Keyboard>/f1");

            _host = Created(new GameObject("Host")).AddComponent<StateMachineHost>();
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _c = CreateStateId("C");
        }

        [TearDown]
        public override void TearDown()
        {
            _asset.Disable();
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            base.TearDown();
        }

        // ---------------------------------------------------------------- C2

#if UNITY_EDITOR
        [Test]
        public void C2_StateInputBinding_IsInTheAddComponentMenu()
        {
            var menu = typeof(StateInputBinding).GetCustomAttribute<AddComponentMenu>();
            Assert.That(menu, Is.Not.Null, "StateInputBinding has an [AddComponentMenu]");
            Assert.That(menu.componentMenu, Is.EqualTo("ALittleBB/State Machine/State Input Binding"));

            var binding = Created(new GameObject("Binding")).AddComponent<StateInputBinding>();
            MonoScript script = MonoScript.FromMonoBehaviour(binding);
            Assert.That(script, Is.Not.Null, "Add Component needs a script asset for the component");
            Assert.That(script.GetClass(), Is.EqualTo(typeof(StateInputBinding)), "the script file is named after the class");
        }

        [Test]
        public void C2_HostAssetAndEntries_SetThroughTheInspector_DriveTheMap()
        {
            var gameObject = Created(new GameObject("Binding"));
            gameObject.SetActive(false);
            var binding = gameObject.AddComponent<StateInputBinding>();

            // What the Inspector writes: the serialized fields, no code of the component.
            var serialized = new SerializedObject(binding);
            SerializedProperty host = serialized.FindProperty("_host");
            SerializedProperty actions = serialized.FindProperty("_actions");
            SerializedProperty entries = serialized.FindProperty("_entries");
            Assert.That(host, Is.Not.Null, "Host field");
            Assert.That(actions, Is.Not.Null, "Input Action Asset field");
            Assert.That(entries, Is.Not.Null, "Entries list");
            Assert.That(entries.isArray, Is.True, "Entries is a list");

            host.objectReferenceValue = _host;
            actions.objectReferenceValue = _asset;
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            SerializedProperty states = entry.FindPropertyRelative("_states");
            SerializedProperty actionMap = entry.FindPropertyRelative("_actionMap");
            SerializedProperty whenPaused = entry.FindPropertyRelative("_whenPaused");
            Assert.That(states, Is.Not.Null, "entry: States list");
            Assert.That(actionMap, Is.Not.Null, "entry: Action Map name");
            Assert.That(whenPaused, Is.Not.Null, "entry: When Paused option");
            Assert.That(whenPaused.propertyType, Is.EqualTo(SerializedPropertyType.Enum), "When Paused is a drop-down");
            Assert.That(whenPaused.enumNames, Is.EqualTo(new[] { "DisableMap", "KeepMapEnabled" }), "options of When Paused (the first is the default)");

            states.arraySize = 1;
            states.GetArrayElementAtIndex(0).objectReferenceValue = _a;
            actionMap.stringValue = Gameplay;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(binding.Host, Is.SameAs(_host));
            Assert.That(binding.Actions, Is.SameAs(_asset));
            Assert.That(binding.Entries, Has.Count.EqualTo(1));
            Assert.That(binding.Entries[0].States, Is.EqualTo(new[] { _a }));
            Assert.That(binding.Entries[0].ActionMap, Is.EqualTo(Gameplay));
            Assert.That(binding.Entries[0].WhenPaused, Is.EqualTo(StateInputBinding.WhenPaused.DisableMap), "default of When Paused");

            gameObject.SetActive(true);
            _host.Switch(_a);

            Assert.That(MapEnabled(Gameplay), Is.True, "the map set in the Inspector is enabled with its state");
        }
#endif

        // ---------------------------------------------------------------- C3

        [Test]
        public void C3_EntryAToGameplay_WhenSwitchToA_GameplayIsEnabled_AsSoonAsSwitchReturns()
        {
            Bind(Entry(Gameplay, _a));
            Assert.That(MapEnabled(Gameplay), Is.False, "Given: Gameplay starts disabled");

            _host.Switch(_a);

            Assert.That(MapEnabled(Gameplay), Is.True, "Gameplay is enabled right after Switch(A), in the same frame");
            Counter jumps = CountPerformed(_jump);
            Tap(_keyboard.spaceKey);
            Assert.That(jumps.Value, Is.EqualTo(1), "Space performs Jump");
        }

        // ---------------------------------------------------------------- C4

        [UnityTest]
        public IEnumerator C4_CurrentAWithGameplayEnabled_WhenSwitchToB_GameplayIsDisabled_AndItsKeysDoNothing()
        {
            Bind(Entry(Gameplay, _a));
            _host.Switch(_a);
            yield return null;
            Assert.That(MapEnabled(Gameplay), Is.True, "Given: Current is A and Gameplay is enabled");

            _host.Switch(_b);

            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay is disabled right after Switch(B)");
            Counter jumps = CountPerformed(_jump);
            Tap(_keyboard.spaceKey);
            Assert.That(jumps.Value, Is.EqualTo(0), "Space does not perform Jump right after Switch(B)");

            yield return null;
            Tap(_keyboard.spaceKey);
            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay stays disabled on the next frame");
            Assert.That(jumps.Value, Is.EqualTo(0), "Space does not perform Jump on the next frame");
        }

        // ---------------------------------------------------------------- C5

        [UnityTest]
        public IEnumerator C5_EntryAAndBToCommon_KeyHeld_WhenSwitchAToB_CommonStaysEnabled_AndTheHeldKeyIsNotCancelled()
        {
            Bind(Entry(Common, _a, _b));
            _host.Switch(_a);
            yield return null;
            int canceled = 0;
            _run.canceled += _ => canceled++;
            Hold(_keyboard.leftShiftKey);
            Assert.That(_run.IsPressed(), Is.True, "Given: Left Shift is held and Run is pressed");

            _host.Switch(_b);
            yield return null;
            yield return null;

            Assert.That(MapEnabled(Common), Is.True, "Common is still enabled in B");
            Assert.That(canceled, Is.EqualTo(0), "Run was not cancelled by Switch(A → B)");
            Assert.That(_run.IsPressed(), Is.True, "Run is still pressed");
            Assert.That(_run.phase, Is.EqualTo(InputActionPhase.Performed), "Run is still in its Performed phase");

            LetGo(_keyboard.leftShiftKey);
            Assert.That(canceled, Is.EqualTo(1), "releasing the key ends Run, as it would without the switch");
        }

        // ---------------------------------------------------------------- C6

        [UnityTest]
        public IEnumerator C6_DebugInNoEntry_EnabledByOtherCode_WhenSwitchingBackAndForth_DebugStaysEnabled()
        {
            Bind(Entry(Gameplay, _a), Entry(Common, _b));
            _asset.FindActionMap(Debug).Enable(); // other code

            foreach (StateId state in new[] { _a, _b, _c, _a, _b })
            {
                _host.Switch(state);
                Assert.That(MapEnabled(Debug), Is.True, $"Debug is still enabled right after Switch({state.name})");
                yield return null;
                Assert.That(MapEnabled(Debug), Is.True, $"Debug is still enabled a frame after Switch({state.name})");
            }

            Counter toggles = CountPerformed(_toggle);
            Tap(_keyboard.f1Key);
            Assert.That(toggles.Value, Is.EqualTo(1), "F1 still performs Toggle");
        }

        [UnityTest]
        public IEnumerator C6_DebugInNoEntry_DisabledByOtherCode_WhenSwitchingBackAndForth_DebugStaysDisabled()
        {
            Bind(Entry(Gameplay, _a), Entry(Common, _b));

            foreach (StateId state in new[] { _a, _b, _c, _a })
            {
                _host.Switch(state);
                yield return null;
                Assert.That(MapEnabled(Debug), Is.False, $"Debug is still disabled after Switch({state.name})");
            }
        }

        // ---------------------------------------------------------------- C7

        [Test]
        public void C7_StateInputBinding_IsOutsideTheCore_AndUsesOnlyItsPublicApi()
        {
            Assembly core = typeof(StateMachine).Assembly;
            Assembly input = typeof(StateInputBinding).Assembly;

            Assert.That(input, Is.Not.SameAs(core), "StateInputBinding is not in the core assembly");
            Assert.That(input.GetName().Name, Is.EqualTo("ALittleBB.StateMachines.InputSystem"));
            Assert.That(
                core.GetCustomAttributes<InternalsVisibleToAttribute>().Select(attribute => attribute.AssemblyName),
                Has.No.Member(input.GetName().Name),
                "the core does not open its internals to the Input assembly: it is connected through the Listener API");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Adds a StateInputBinding the way a scene sets one up: the GameObject is activated once Host, the asset and
        /// the entries are set.
        /// </summary>
        StateInputBinding Bind(params StateInputBinding.Entry[] entries)
        {
            var gameObject = Created(new GameObject("Binding"));
            gameObject.SetActive(false);
            var binding = gameObject.AddComponent<StateInputBinding>();
            binding.Host = _host;
            binding.Actions = _asset;
            binding.Entries = entries;
            gameObject.SetActive(true);
            return binding;
        }

        static StateInputBinding.Entry Entry(string actionMap, params StateId[] states) =>
            new StateInputBinding.Entry { States = states, ActionMap = actionMap };

        bool MapEnabled(string map) => _asset.FindActionMap(map, throwIfNotFound: true).enabled;

        // In a [UnityTest] the fixture only queues the key events until the next frame; these process them at once,
        // so that the assertion right after a key sees its effect.
        void Hold(KeyControl key)
        {
            Press(key);
            InputSystem.Update();
        }

        void LetGo(KeyControl key)
        {
            Release(key);
            InputSystem.Update();
        }

        void Tap(KeyControl key)
        {
            Hold(key);
            LetGo(key);
        }

        /// <summary>Counts the times <paramref name="action"/> is performed from now on.</summary>
        static Counter CountPerformed(InputAction action)
        {
            var counter = new Counter();
            action.performed += _ => counter.Value++;
            return counter;
        }

        sealed class Counter
        {
            public int Value;
        }

        StateId CreateStateId(string stateName)
        {
            var id = Created(ScriptableObject.CreateInstance<StateId>());
            id.name = stateName;
            return id;
        }

        T Created<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
