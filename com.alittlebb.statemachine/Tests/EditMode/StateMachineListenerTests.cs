using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    public class StateMachineListenerTests
    {
        const string SignalEntry = "signal";

        StateId _a;
        StateId _b;
        StateId _c;
        StateId _x;
        List<string> _calls;
        List<StateChanged> _signals;
        StateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            _signals = new List<StateChanged>();
            _machine = new StateMachine();
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _c = CreateStateId("C");
            _x = CreateStateId("X");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Object.DestroyImmediate(_c);
            Object.DestroyImmediate(_x);
        }

        [Test]
        public void C1_Switch_ToStateInSet_FromNoState_CallsOnEnterOnce()
        {
            Listen("L", _a);

            _machine.Switch(_a);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnEnter", "L.OnChanged" }));
        }

        [Test]
        public void C2_Switch_FromStateInSet_ToStateOutside_CallsOnExitOnce()
        {
            Listen("L", _a);
            GivenStackIs(_a);

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnExit", "L.OnChanged" }));
        }

        [Test]
        public void C3_Switch_BetweenTwoStatesOfTheSet_CallsOnlyOnChanged()
        {
            Listen("L", _a, _b);
            GivenStackIs(_a);

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnChanged" }));
            Assert.That(_signals, Has.Count.EqualTo(1));
            Assert.That(_signals[0].From, Is.SameAs(_a));
            Assert.That(_signals[0].To, Is.SameAs(_b));
            Assert.That(_signals[0].Kind, Is.EqualTo(StateChangeKind.Switch));
        }

        [Test]
        public void C4_PushThenPop_OverStateInSet_CallsOnPausedThenOnResumed_AndNeverOnExit()
        {
            Listen("L", _a);
            GivenStackIs(_a);

            _machine.Push(_x);
            _machine.Pop();

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnPaused", "L.OnChanged", "L.OnResumed", "L.OnChanged" }));
        }

        [Test]
        public void C5_Push_AnotherStateOfTheSet_CallsNoStatusCallback()
        {
            Listen("L", _a, _b);
            GivenStackIs(_a);

            _machine.Push(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnChanged" }));
        }

        [Test]
        public void C6_Subscribe_WhileStateInSetIsCurrent_CallsOnEnterImmediately()
        {
            GivenStackIs(_a);

            Listen("L", _a);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnEnter" }));
        }

        [TestCase("OnExit")]
        [TestCase("OnChanged")]
        public void C7_ListenerUnsubscribingItselfInsideACallback_OthersStillGetEveryCallback_AndItGetsNoneAfterwards(string unsubscribeIn)
        {
            StateSubscription first = null;
            bool unsubscribeNow = false;
            Action<string> record = callback =>
            {
                _calls.Add($"L1.{callback}");
                if (unsubscribeNow && callback == unsubscribeIn)
                    first.Dispose();
            };
            first = _machine.Subscribe(new[] { _a },
                onEnter: () => record("OnEnter"),
                onExit: () => record("OnExit"),
                onPaused: () => record("OnPaused"),
                onResumed: () => record("OnResumed"),
                onChanged: _ => record("OnChanged"));
            Listen("L2", _a);
            Listen("L3", _b);
            GivenStackIs(_a);
            unsubscribeNow = true;

            Assert.DoesNotThrow(() => _machine.Switch(_b));

            string[] expectedOfFirst = unsubscribeIn == "OnExit"
                ? new[] { "L1.OnExit" }
                : new[] { "L1.OnExit", "L1.OnChanged" };
            Assert.That(CallsOf("L1"), Is.EqualTo(expectedOfFirst));
            Assert.That(CallsOf("L2"), Is.EqualTo(new[] { "L2.OnExit", "L2.OnChanged" }));
            Assert.That(CallsOf("L3"), Is.EqualTo(new[] { "L3.OnEnter", "L3.OnChanged" }));

            _calls.Clear();
            _machine.Switch(_a);

            Assert.That(CallsOf("L1"), Is.Empty);
            Assert.That(CallsOf("L2"), Is.EqualTo(new[] { "L2.OnEnter", "L2.OnChanged" }));
            Assert.That(CallsOf("L3"), Is.EqualTo(new[] { "L3.OnExit", "L3.OnChanged" }));
        }

        [Test]
        public void C8_Switch_BetweenStatesOutsideTheSet_CallsNoStatusCallback()
        {
            Listen("L", _c);
            GivenStackIs(_a);

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnChanged" }));
        }

        [Test]
        public void Subscribe_WhileStateInSetIsPaused_CallsOnEnterThenOnPaused()
        {
            GivenStackIs(_a, _x);

            StateSubscription listener = Listen("L", _a);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnEnter", "L.OnPaused" }));
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Paused));
        }

        [Test]
        public void Switch_WhileListenerIsPaused_ToStateOutsideTheSet_CallsOnExitWithoutOnResumed()
        {
            Listen("L", _a);
            GivenStackIs(_a, _x);

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnExit", "L.OnChanged" }));
        }

        [Test]
        public void Switch_WhileListenerIsPaused_ToStateOfTheSet_CallsOnResumed()
        {
            Listen("L", _a);
            GivenStackIs(_a, _x);

            _machine.Switch(_a);

            Assert.That(_calls, Is.EqualTo(new[] { "L.OnResumed", "L.OnChanged" }));
        }

        [Test]
        public void Status_IsActiveWhenTopIsInSet_PausedWhenOnlyLowerInStack_InactiveOtherwise()
        {
            StateSubscription listener = Listen("L", _a);
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Inactive));

            _machine.Switch(_a);
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Active));

            _machine.Push(_x);
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Paused));

            _machine.Pop();
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Active));

            _machine.Switch(_b);
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Inactive));
        }

        [Test]
        public void Transition_CallsLeavingCallbacksOfEveryListener_ThenArrivingOnes_ThenTheSignal()
        {
            Listen("LB", _b);
            Listen("LA", _a);
            GivenStackIs(_a);
            _machine.Changed += _ => _calls.Add(SignalEntry);

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "LA.OnExit", "LB.OnEnter", SignalEntry, "LB.OnChanged", "LA.OnChanged" }));
        }

        [Test]
        public void Dispose_StopsEveryCallback_WithoutCallingOnExit_AndCanBeCalledTwice()
        {
            StateSubscription listener = Listen("L", _a);
            _machine.Switch(_a);
            Assert.That(_calls, Is.EqualTo(new[] { "L.OnEnter", "L.OnChanged" }));
            _calls.Clear();

            listener.Dispose();
            listener.Dispose();
            _machine.Switch(_b);

            Assert.That(_calls, Is.Empty);
        }

        [Test]
        public void Dispose_ByAnotherListenerEarlierInTheSameTransition_SkipsTheDisposedListener()
        {
            StateSubscription second = null;
            _machine.Subscribe(new[] { _a }, onExit: () => second.Dispose());
            second = Listen("L2", _a);
            GivenStackIs(_a);

            _machine.Switch(_b);

            Assert.That(_calls, Is.Empty);
        }

        [Test]
        public void Subscribe_InsideACallback_SyncsWithTheNewStack_AndDoesNotGetTheSignalInFlight()
        {
            _machine.Subscribe(new[] { _a }, onEnter: () => Listen("Late", _a));

            _machine.Switch(_a);
            Assert.That(_calls, Is.EqualTo(new[] { "Late.OnEnter" }));

            _calls.Clear();
            _machine.Switch(_b);
            Assert.That(_calls, Is.EqualTo(new[] { "Late.OnExit", "Late.OnChanged" }));
        }

        [Test]
        public void Subscribe_NullStates_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _machine.Subscribe(null));
        }

        /// <summary>Subscribes a listener that records every callback as "name.Callback" and keeps the signals it gets.</summary>
        StateSubscription Listen(string name, params StateId[] states)
        {
            return _machine.Subscribe(states,
                onEnter: () => _calls.Add($"{name}.OnEnter"),
                onExit: () => _calls.Add($"{name}.OnExit"),
                onPaused: () => _calls.Add($"{name}.OnPaused"),
                onResumed: () => _calls.Add($"{name}.OnResumed"),
                onChanged: signal =>
                {
                    _calls.Add($"{name}.OnChanged");
                    _signals.Add(signal);
                });
        }

        /// <summary>Builds the stack bottom → top (Switch to the first id, Push the rest) and forgets what that recorded.</summary>
        void GivenStackIs(StateId bottom, params StateId[] above)
        {
            _machine.Switch(bottom);
            foreach (StateId id in above)
                _machine.Push(id);

            _calls.Clear();
            _signals.Clear();
        }

        string[] CallsOf(string listenerName)
        {
            return _calls.Where(call => call.StartsWith(listenerName + ".")).ToArray();
        }

        static StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            return id;
        }
    }
}
