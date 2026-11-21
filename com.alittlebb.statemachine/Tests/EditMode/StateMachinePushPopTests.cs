using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    public class StateMachinePushPopTests
    {
        const string SignalEntry = "signal";

        StateId _a;
        StateId _b;
        StateId _c;
        StateId _d;
        List<string> _calls;
        List<StateChanged> _signals;
        StateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            _signals = new List<StateChanged>();
            _machine = new StateMachine();
            _a = CreateRegisteredStateId("A");
            _b = CreateRegisteredStateId("B");
            _c = CreateRegisteredStateId("C");
            _d = CreateRegisteredStateId("D");
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
            Object.DestroyImmediate(_c);
            Object.DestroyImmediate(_d);
        }

        [Test]
        public void C1_Push_OverCurrentState_DoesNotExitIt_EntersNew_StacksBoth_AndSignalsPush()
        {
            GivenStackIs(_a);

            _machine.Push(_b);

            Assert.That(_calls, Is.EqualTo(new[] { "B.Enter", SignalEntry }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a, _b }));
            Assert.That(_machine.Current, Is.SameAs(_b));
            AssertSingleSignal(from: _a, to: _b, StateChangeKind.Push);
        }

        [Test]
        public void C2_Pop_ExitsTop_DoesNotEnterLowerAgain_MakesLowerCurrent_AndSignalsPop()
        {
            GivenStackIs(_a, _b);

            _machine.Pop();

            Assert.That(_calls, Is.EqualTo(new[] { "B.Exit", SignalEntry }));
            Assert.That(_machine.Current, Is.SameAs(_a));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a }));
            AssertSingleSignal(from: _b, to: _a, StateChangeKind.Pop);
        }

        [Test]
        public void C3_Pop_WithSingleState_LogsWarning_KeepsStack_AndDoesNotSignal()
        {
            GivenStackIs(_a);
            LogAssert.Expect(LogType.Warning, new Regex("only state"));

            _machine.Pop();

            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a }));
            Assert.That(_machine.Current, Is.SameAs(_a));
            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
        }

        [Test]
        public void C4_Pop_WithNoState_LogsWarning_AndDoesNotSignal()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no state"));

            _machine.Pop();

            Assert.That(_machine.Stack, Is.Empty);
            Assert.That(_machine.Current, Is.Null);
            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
        }

        [Test]
        public void C5_Switch_WithStack_ExitsTopToBottom_ThenEntersNew_LeavesOnlyNew_AndSignalsOnce()
        {
            GivenStackIs(_a, _b, _c);

            _machine.Switch(_d);

            Assert.That(_calls, Is.EqualTo(new[] { "C.Exit", "B.Exit", "A.Exit", "D.Enter", SignalEntry }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _d }));
            Assert.That(_machine.Current, Is.SameAs(_d));
            AssertSingleSignal(from: _c, to: _d, StateChangeKind.Switch);
        }

        [Test]
        public void C6_Push_StateAlreadyInStack_LogsError_KeepsStack_AndDoesNotSignal()
        {
            GivenStackIs(_a, _b);
            LogAssert.Expect(LogType.Error, new Regex("already on the stack"));

            _machine.Push(_a);

            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a, _b }));
            Assert.That(_machine.Current, Is.SameAs(_b));
            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
        }

        [Test]
        public void C7_Stack_AfterSeveralPushesAndPops_ListsStatesBottomToTop()
        {
            _machine.Switch(_a);
            _machine.Push(_b);
            _machine.Push(_c);
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a, _b, _c }));

            _machine.Pop();
            _machine.Push(_d);
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a, _b, _d }));

            _machine.Pop();
            _machine.Pop();
            _machine.Push(_c);
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a, _c }));
        }

        [Test]
        public void C7_Stack_CannotBeChangedByTheReader()
        {
            GivenStackIs(_a, _b);

            var asCollection = _machine.Stack as ICollection<StateId>;

            Assert.That(asCollection == null || asCollection.IsReadOnly, Is.True,
                "Stack must not hand out a collection the caller can modify.");
        }

        [Test]
        public void Push_WithNoState_EntersIt_AndSignalsPushFromNull()
        {
            _machine.Push(_a);

            Assert.That(_calls, Is.EqualTo(new[] { "A.Enter", SignalEntry }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a }));
            AssertSingleSignal(from: null, to: _a, StateChangeKind.Push);
        }

        [Test]
        public void Push_Null_LogsError_KeepsStack_AndDoesNotSignal()
        {
            GivenStackIs(_a);
            LogAssert.Expect(LogType.Error, new Regex("StateId is null"));

            _machine.Push(null);

            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a }));
            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
        }

        [Test]
        public void Switch_ToTopOfStack_DoesNothing()
        {
            GivenStackIs(_a, _b);

            _machine.Switch(_b);

            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a, _b }));
            Assert.That(_calls, Is.Empty);
            Assert.That(_signals, Is.Empty);
        }

        [Test]
        public void Switch_ToPausedStateLowerInStack_ExitsWholeStack_ThenEntersItAgain()
        {
            GivenStackIs(_a, _b);

            _machine.Switch(_a);

            Assert.That(_calls, Is.EqualTo(new[] { "B.Exit", "A.Exit", "A.Enter", SignalEntry }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _a }));
            AssertSingleSignal(from: _b, to: _a, StateChangeKind.Switch);
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

        void AssertSingleSignal(StateId from, StateId to, StateChangeKind kind)
        {
            Assert.That(_signals, Has.Count.EqualTo(1));
            Assert.That(_signals[0].From, Is.SameAs(from));
            Assert.That(_signals[0].To, Is.SameAs(to));
            Assert.That(_signals[0].Kind, Is.EqualTo(kind));
        }

        StateId CreateRegisteredStateId(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            _machine.Register(id, new RecordingState(name, _calls));
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
