using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// GEN-013: <see cref="StateInputBinding"/> with Push and Pop (When Paused), late sync, nested transitions,
    /// disable and destroy, and settings that are wrong.
    /// </summary>
    public partial class StateInputBindingTests
    {
        const string SeedPanel = "SeedPanel"; // Pick: S

        // ---------------------------------------------------------------- C1

        [Test]
        public void GEN013_C1_CurrentAWithGameplay_WhenPushSeed_GameplayIsDisabled_AndSeedPanelIsEnabled()
        {
            StateId seed = CreateStateId("Seed");
            AddMap(SeedPanel, "<Keyboard>/s");
            Bind(Entry(Gameplay, _a), Entry(SeedPanel, seed));
            _host.Switch(_a);
            Assert.That(MapEnabled(Gameplay), Is.True, "Given: Current is A and Gameplay is enabled");
            Assert.That(MapEnabled(SeedPanel), Is.False, "Given: SeedPanel is disabled");

            _host.Push(seed);

            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay of the paused A is disabled right after Push(Seed)");
            Assert.That(MapEnabled(SeedPanel), Is.True, "SeedPanel is enabled right after Push(Seed)");
            Counter jumps = CountPerformed(_jump);
            Tap(_keyboard.spaceKey);
            Assert.That(jumps.Value, Is.EqualTo(0), "Space does not perform Jump while the seed panel is open");
        }

        // ---------------------------------------------------------------- C2

        [Test]
        public void GEN013_C2_StackASeed_WhenPop_SeedPanelIsDisabled_AndGameplayIsEnabledAgain()
        {
            StateId seed = CreateStateId("Seed");
            AddMap(SeedPanel, "<Keyboard>/s");
            Bind(Entry(Gameplay, _a), Entry(SeedPanel, seed));
            _host.Switch(_a);
            _host.Push(seed);
            Assert.That(_host.Machine.Stack, Is.EqualTo(new[] { _a, seed }), "Given: Stack is [A, Seed]");

            _host.Pop();

            Assert.That(MapEnabled(SeedPanel), Is.False, "SeedPanel is disabled right after Pop()");
            Assert.That(MapEnabled(Gameplay), Is.True, "Gameplay is enabled again right after Pop()");
            Counter jumps = CountPerformed(_jump);
            Tap(_keyboard.spaceKey);
            Assert.That(jumps.Value, Is.EqualTo(1), "Space performs Jump again");
        }

        // ---------------------------------------------------------------- C3

        [Test]
        public void GEN013_C3_EntryAKeepsItsMapWhenPaused_WhenPushXOverA_TheMapOfAStaysEnabled()
        {
            Bind(Entry(Gameplay, StateInputBinding.WhenPaused.KeepMapEnabled, _a), Entry(Common, _b));
            _host.Switch(_a);
            int canceled = 0;
            _jump.canceled += _ => canceled++;
            Hold(_keyboard.spaceKey);
            Assert.That(_jump.IsPressed(), Is.True, "Given: A is current and Space is held");

            _host.Push(_b);

            Assert.That(MapEnabled(Gameplay), Is.True, "Gameplay of A stays enabled while A is paused");
            Assert.That(MapEnabled(Common), Is.True, "the map of the pushed state is enabled too");
            Assert.That(canceled, Is.EqualTo(0), "the held Space is not cancelled by Push");

            _host.Pop();

            Assert.That(MapEnabled(Gameplay), Is.True, "Gameplay is still enabled after Pop()");
            Assert.That(canceled, Is.EqualTo(0), "the held Space is not cancelled by Pop");
        }

        [Test]
        public void GEN013_C3_EntryAKeepsItsMapWhenPaused_WhenAIsLeftWhilePaused_TheMapIsDisabled()
        {
            Bind(Entry(Gameplay, StateInputBinding.WhenPaused.KeepMapEnabled, _a));
            _host.Switch(_a);
            _host.Push(_b);
            Assert.That(MapEnabled(Gameplay), Is.True, "Given: A is paused under B and Gameplay is still enabled");

            _host.Switch(_c);

            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay is disabled once A leaves the stack");
        }

        [Test]
        public void GEN013_C3_DefaultDisableMap_WhenAIsLeftWhilePaused_TheMapStaysDisabled()
        {
            Bind(Entry(Gameplay, _a));
            _host.Switch(_a);
            _host.Push(_b);

            _host.Switch(_c);

            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay is disabled after A leaves the stack while paused");
        }

        // ---------------------------------------------------------------- C4

        [Test]
        public void GEN013_C4_CurrentAAlready_WhenTheBindingStartsUp_TheMapOfAIsEnabled_AndOtherListedMapsAreDisabled()
        {
            _host.Switch(_a);
            _asset.FindActionMap(Common).Enable(); // other code, before the binding exists
            _asset.FindActionMap(Debug).Enable();  // other code, map in no entry

            Bind(Entry(Gameplay, _a), Entry(Common, _b)); // first activation, as after Instantiate

            Assert.That(MapEnabled(Gameplay), Is.True, "the map of the active A is enabled at once");
            Assert.That(MapEnabled(Common), Is.False, "Common is in the list and B is not active: it is disabled");
            Assert.That(MapEnabled(Debug), Is.True, "Debug is in no entry and is not touched");
        }

        [Test]
        public void GEN013_C4_CurrentAAlready_WhenTheComponentIsEnabled_TheMapOfAIsEnabled_AndOtherListedMapsAreDisabled()
        {
            StateInputBinding binding = Bind(Entry(Gameplay, _a), Entry(Common, _b));
            binding.enabled = false;
            _host.Switch(_a);
            _asset.FindActionMap(Common).Enable(); // other code, while the binding is disabled
            Assert.That(MapEnabled(Gameplay), Is.False, "Given: the disabled binding did not follow Switch(A)");

            binding.enabled = true;

            Assert.That(MapEnabled(Gameplay), Is.True, "the map of the active A is enabled when the component is enabled");
            Assert.That(MapEnabled(Common), Is.False, "Common is in the list and B is not active: it is disabled");
        }

        [TestCase(StateInputBinding.WhenPaused.DisableMap, false)]
        [TestCase(StateInputBinding.WhenPaused.KeepMapEnabled, true)]
        public void GEN013_C4_APausedUnderB_WhenTheBindingStartsUp_TheMapOfAFollowsWhenPaused(
            StateInputBinding.WhenPaused whenPaused, bool gameplayEnabled)
        {
            _host.Switch(_a);
            _host.Push(_b);

            Bind(Entry(Gameplay, whenPaused, _a), Entry(Common, _b));

            Assert.That(MapEnabled(Gameplay), Is.EqualTo(gameplayEnabled), $"Gameplay of the paused A with {whenPaused}");
            Assert.That(MapEnabled(Common), Is.True, "the map of the current B is enabled");
        }

        // ---------------------------------------------------------------- C5

        [Test]
        public void GEN013_C5_AEnterSwitchesToB_WhenSwitchToA_OnlyTheMapOfBIsEnabledOnceTheQueueIsEmpty()
        {
            _host.Machine.Register(_a, new OnEnter(() => _host.Switch(_b)));
            Bind(Entry(Gameplay, _a), Entry(Common, _b), Entry(Debug, _c));

            _host.Switch(_a);

            Assert.That(_host.Machine.Current, Is.SameAs(_b), "Given: the queued Switch(B) ran before Switch(A) returned");
            Assert.That(MapEnabled(Common), Is.True, "Common of B is enabled");
            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay of A is disabled");
            Assert.That(MapEnabled(Debug), Is.False, "Debug of C is disabled");
        }

        [Test]
        public void GEN013_C5_AEnterPushesB_WhenSwitchToA_OnlyTheMapOfBIsEnabledOnceTheQueueIsEmpty()
        {
            _host.Machine.Register(_a, new OnEnter(() => _host.Push(_b)));
            Bind(Entry(Gameplay, _a), Entry(Common, _b));

            _host.Switch(_a);

            Assert.That(_host.Machine.Stack, Is.EqualTo(new[] { _a, _b }), "Given: the queued Push(B) ran before Switch(A) returned");
            Assert.That(MapEnabled(Common), Is.True, "Common of B is enabled");
            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay of the paused A is disabled");
        }

        // ---------------------------------------------------------------- C6

        [Test]
        public void GEN013_C6_MapEnabledFromAnEntry_WhenTheComponentIsDisabled_EveryListedMapIsDisabled_AndEnablingSyncsAgain()
        {
            StateInputBinding binding = Bind(Entry(Gameplay, _a), Entry(Common, _b));
            _host.Switch(_a);
            _asset.FindActionMap(Common).Enable(); // other code
            _asset.FindActionMap(Debug).Enable();  // other code, map in no entry
            Assert.That(MapEnabled(Gameplay), Is.True, "Given: Gameplay is enabled from the entry of A");

            binding.enabled = false;

            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay is disabled with the component");
            Assert.That(MapEnabled(Common), Is.False, "Common, in the list, is disabled with the component");
            Assert.That(MapEnabled(Debug), Is.True, "Debug is in no entry and is not touched");

            _host.Switch(_b);
            Assert.That(MapEnabled(Common), Is.False, "the disabled component does not follow Switch(B)");

            binding.enabled = true;

            Assert.That(MapEnabled(Common), Is.True, "enabled again: Common follows the current B");
            Assert.That(MapEnabled(Gameplay), Is.False, "enabled again: Gameplay stays disabled");
        }

        [UnityTest]
        public IEnumerator GEN013_C6_MapEnabledFromAnEntry_WhenTheComponentIsDestroyed_EveryListedMapIsDisabled()
        {
            StateInputBinding binding = Bind(Entry(Gameplay, _a), Entry(Common, _b));
            _host.Switch(_a);
            _asset.FindActionMap(Common).Enable(); // other code
            Assert.That(MapEnabled(Gameplay), Is.True, "Given: Gameplay is enabled from the entry of A");

            Object.Destroy(binding.gameObject);
            yield return null;

            Assert.That(MapEnabled(Gameplay), Is.False, "Gameplay is disabled when the component is destroyed");
            Assert.That(MapEnabled(Common), Is.False, "Common, in the list, is disabled when the component is destroyed");

            Assert.DoesNotThrow(() => _host.Switch(_b), "the host still switches after the binding is gone");
            Assert.That(MapEnabled(Common), Is.False, "the destroyed component does not follow Switch(B)");
        }

        // ---------------------------------------------------------------- C7

        [TestCase("Nope")]
        [TestCase("")]
        public void GEN013_C7_MapNotInTheAsset_WarnsOnce_WithTheGameObjectAndTheMap_AndOtherEntriesStillWork(string missing)
        {
            List<string> warnings = CollectWarnings();
            StateInputBinding binding = null;

            Assert.DoesNotThrow(() => binding = Bind(Entry(Gameplay, _a), Entry(missing, _b), Entry(Common, _b)));
            Assert.That(warnings, Has.Count.EqualTo(1), "one warning as soon as the binding starts up");
            Assert.That(warnings[0], Does.Contain("'Binding'").And.Contain($"'{missing}'").And.Contain("'TestActions'"),
                "the warning names the GameObject, the map and the asset");

            Assert.DoesNotThrow(() =>
            {
                _host.Switch(_b);
                _host.Switch(_a);
                _host.Push(_b);
                _host.Pop();
                _host.Switch(_b);
                binding.enabled = false;
                binding.enabled = true;
            });

            Assert.That(warnings, Has.Count.EqualTo(1), "still one warning after transitions and re-enabling");
            Assert.That(MapEnabled(Common), Is.True, "the other entry of B still enables Common");
            Assert.That(MapEnabled(Gameplay), Is.False, "the entry of A still disables Gameplay");
        }

        [Test]
        public void GEN013_C7_NoAsset_WarnsOnce_WithTheGameObjectAndTheMaps_AndNothingThrows()
        {
            List<string> warnings = CollectWarnings();
            StateInputBinding binding = null;

            Assert.DoesNotThrow(() => binding = Bind(null, _host, Entry(Gameplay, _a), Entry(Common, _b)));
            Assert.That(warnings, Has.Count.EqualTo(1), "one warning as soon as the binding starts up");
            Assert.That(warnings[0], Does.Contain("'Binding'").And.Contain("Actions").And.Contain("'Gameplay'").And.Contain("'Common'"),
                "the warning names the GameObject, the missing field and the maps");

            Assert.DoesNotThrow(() =>
            {
                _host.Switch(_a);
                _host.Push(_b);
                binding.enabled = false;
                binding.enabled = true;
                Object.DestroyImmediate(binding);
            });

            Assert.That(warnings, Has.Count.EqualTo(1), "still one warning after transitions and re-enabling");
        }

        [Test]
        public void GEN013_C7_NoHost_WarnsOnce_WithTheGameObjectAndTheMaps_AndNothingThrows()
        {
            List<string> warnings = CollectWarnings();
            StateInputBinding binding = null;
            _asset.FindActionMap(Gameplay).Enable(); // other code

            Assert.DoesNotThrow(() => binding = Bind(_asset, null, Entry(Gameplay, _a)));
            Assert.That(warnings, Has.Count.EqualTo(1), "one warning as soon as the binding starts up");
            Assert.That(warnings[0], Does.Contain("'Binding'").And.Contain("Host").And.Contain("'Gameplay'"),
                "the warning names the GameObject, the missing field and the map");
            Assert.That(MapEnabled(Gameplay), Is.True, "without a host the binding does not touch the maps");

            Assert.DoesNotThrow(() =>
            {
                _host.Switch(_a);
                binding.enabled = false;
                binding.enabled = true;
                Object.DestroyImmediate(binding);
            });

            Assert.That(warnings, Has.Count.EqualTo(1), "still one warning after re-enabling");
        }

        // ---------------------------------------------------------------- C8

        [Test]
        public void GEN013_C8_StateInputBinding_ReachesTheCoreOnlyThroughItsPublicApi()
        {
            Assembly core = typeof(StateMachine).Assembly;
            Assembly input = typeof(StateInputBinding).Assembly;

            Assert.That(input, Is.Not.SameAs(core), "StateInputBinding is not in the core assembly");
            Assert.That(
                core.GetCustomAttributes<InternalsVisibleToAttribute>().Select(attribute => attribute.AssemblyName),
                Has.No.Member(input.GetName().Name),
                "the core does not open its internals to the Input assembly");
            Assert.That(
                input.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.Member(core.GetName().Name),
                "the Input assembly is a client of the core");
        }

        // ---------------------------------------------------------------- helpers

        StateInputBinding Bind(InputActionAsset actions, StateMachineHost host, params StateInputBinding.Entry[] entries)
        {
            var gameObject = Created(new GameObject("Binding"));
            gameObject.SetActive(false);
            var binding = gameObject.AddComponent<StateInputBinding>();
            binding.Host = host;
            binding.Actions = actions;
            binding.Entries = entries;
            gameObject.SetActive(true);
            return binding;
        }

        static StateInputBinding.Entry Entry(string actionMap, StateInputBinding.WhenPaused whenPaused, params StateId[] states) =>
            new StateInputBinding.Entry { States = states, ActionMap = actionMap, WhenPaused = whenPaused };

        void AddMap(string map, string binding) =>
            _asset.AddActionMap(map).AddAction("Press", InputActionType.Button, binding);

        /// <summary>Warnings logged from now on until the end of the test.</summary>
        List<string> CollectWarnings()
        {
            var warnings = new List<string>();
            Application.LogCallback collect = (message, _, type) =>
            {
                if (type == LogType.Warning)
                    warnings.Add(message);
            };
            Application.logMessageReceived += collect;
            _afterTest.Add(() => Application.logMessageReceived -= collect);
            return warnings;
        }

        readonly List<Action> _afterTest = new List<Action>();

        [TearDown]
        public void RemoveLogCollectors()
        {
            foreach (Action undo in _afterTest)
                undo();

            _afterTest.Clear();
        }

        sealed class OnEnter : IState
        {
            readonly Action _enter;
            bool _done;

            public OnEnter(Action enter) => _enter = enter;

            // Once only: a transition requested here must not repeat if the state is entered again.
            public void Enter()
            {
                if (_done)
                    return;

                _done = true;
                _enter();
            }

            public void Exit() { }
        }
    }
}
