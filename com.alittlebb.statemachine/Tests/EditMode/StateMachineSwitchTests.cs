using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    public class StateMachineSwitchTests
    {
        const string SignalEntry = "signal";

        StateId _a;
        StateId _b;
        List<string> _calls;
        List<StateChanged> _signals;
        StateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _a = CreateStateId("A");
            _b = CreateStateId("B");
            _calls = new List<string>();
            _signals = new List<StateChanged>();
            _machine = new StateMachine();
            _machine.Changed += signal =>
            {
                _calls.Add(SignalEntry);
                _signals.Add(signal);
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        [Test]
        public void C1_Switch_WithNoCurrentState_EntersOnce_SetsCurrent_AndSignalsFromNull()
        {
            _machine.Register(_a, new RecordingState("A", _calls));

            _machine.Switch(_a);

            Assert.That(_calls, Is.EqualTo(new[] { "A.Enter", SignalEntry }));
            Assert.That(_machine.Current, Is.SameAs(_a));
            AssertSingleSignal(from: null, to: _a);
        }

        [Test]
        public void C2_Switch_ToAnotherState_ExitsOld_ThenEntersNew_ThenSignals()
        {
            _machine.Register(_a, new RecordingState("A", _calls));
            _machine.Register(_b, new RecordingState("B", _calls));
            GivenCurrentIs(_a);

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "A.Exit", "B.Enter", SignalEntry }));
            AssertSingleSignal(from: _a, to: _b);
        }

        [Test]
        public void C3_Switch_ToCurrentState_DoesNotExitEnterOrSignal()
        {
            _machine.Register(_a, new RecordingState("A", _calls));
            GivenCurrentIs(_a);

            _machine.Switch(_a);

            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
            Assert.That(_machine.Current, Is.SameAs(_a));
        }

        [Test]
        public void C4_Switch_Null_LogsError_KeepsCurrent_AndDoesNotSignal()
        {
            _machine.Register(_a, new RecordingState("A", _calls));
            GivenCurrentIs(_a);
            LogAssert.Expect(LogType.Error, new Regex("StateId is null"));

            _machine.Switch(null);

            Assert.That(_machine.Current, Is.SameAs(_a));
            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
        }

        [Test]
        public void C5_Switch_ToStateIdWithoutRegisteredState_SwitchesAndSignals()
        {
            _machine.Register(_a, new RecordingState("A", _calls));
            GivenCurrentIs(_a);

            _machine.Switch(_b);

            Assert.That(_machine.Current, Is.SameAs(_b));
            Assert.That(_calls, Is.EqualTo(new[] { "A.Exit", SignalEntry }));
            AssertSingleSignal(from: _a, to: _b);
        }

        [Test]
        public void C6_Listener_ReadingCurrentInCallback_SeesNewState_AfterEnterFinished()
        {
            _machine.Register(_a, new RecordingState("A", _calls));
            _machine.Register(_b, new RecordingState("B", _calls));
            GivenCurrentIs(_a);
            StateId currentSeenByListener = null;
            bool enterFinishedBeforeSignal = false;
            _machine.Changed += _ =>
            {
                currentSeenByListener = _machine.Current;
                enterFinishedBeforeSignal = _calls.Contains("B.Enter");
            };

            _machine.Switch(_b);

            Assert.That(currentSeenByListener, Is.SameAs(_b));
            Assert.That(enterFinishedBeforeSignal, Is.True, "The signal must be sent after B.Enter has finished.");
        }

        [Test]
        public void C7_Switch_OnOneMachine_DoesNotSignalListenersOfAnotherMachine()
        {
            var otherMachine = new StateMachine();
            var otherSignals = new List<StateChanged>();
            otherMachine.Changed += otherSignals.Add;

            _machine.Switch(_a);

            Assert.That(_signals, Has.Count.EqualTo(1), "The machine that switched must signal its own listener.");
            Assert.That(otherSignals, Is.Empty);
            Assert.That(otherMachine.Current, Is.Null);
        }

        [Test]
        public void Register_NullIdOrNullState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _machine.Register(null, new RecordingState("A", _calls)));
            Assert.Throws<ArgumentNullException>(() => _machine.Register(_a, null));
        }

        void GivenCurrentIs(StateId id)
        {
            _machine.Switch(id);
            _calls.Clear();
            _signals.Clear();
        }

        void AssertSingleSignal(StateId from, StateId to)
        {
            Assert.That(_signals, Has.Count.EqualTo(1));
            Assert.That(_signals[0].From, Is.SameAs(from));
            Assert.That(_signals[0].To, Is.SameAs(to));
            Assert.That(_signals[0].Kind, Is.EqualTo(StateChangeKind.Switch));
        }

        static StateId CreateStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            return id;
        }

        sealed class RecordingState : IState
        {
            readonly string _name;
            readonly List<string> _calls;

            public RecordingState(string name, List<string> calls)
            {
                _name = name;
                _calls = calls;
            }

            public void Enter() => _calls.Add($"{_name}.Enter");

            public void Exit() => _calls.Add($"{_name}.Exit");
        }
    }
}
