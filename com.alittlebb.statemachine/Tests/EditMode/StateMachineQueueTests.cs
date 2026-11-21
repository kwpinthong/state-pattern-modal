using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ALittleBB.StateMachines.Tests
{
    /// <summary>
    /// Switch / Push / Pop requested while a transition is running (inside IState Enter / Exit or a listener
    /// callback) wait in a queue and run in the order they were requested.
    /// </summary>
    public class StateMachineQueueTests
    {
        /// <summary>
        /// The looping states of these tests stop asking by themselves after this many requests,
        /// so a missing or broken loop guard fails an assertion instead of hanging the Editor.
        /// </summary>
        const int HardCap = StateMachine.MaxTransitionsPerRun * 4;

        StateId _a;
        StateId _b;
        StateId _c;
        StateId _x;
        StateId _y;
        List<string> _calls;
        Dictionary<StateId, HookedState> _states;
        StateMachine _machine;
        Application.LogCallback _logRecorder;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            _states = new Dictionary<StateId, HookedState>();
            _machine = new StateMachine();
            _a = CreateState("A");
            _b = CreateState("B");
            _c = CreateState("C");
            _x = CreateState("X");
            _y = CreateState("Y");
        }

        [TearDown]
        public void TearDown()
        {
            if (_logRecorder != null)
                Application.logMessageReceived -= _logRecorder;

            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
            Object.DestroyImmediate(_c);
            Object.DestroyImmediate(_x);
            Object.DestroyImmediate(_y);
        }

        [Test]
        public void C1_SwitchRequestedInsideEnter_RunsAfterTheCurrentTransitionNotifiedEveryListener()
        {
            Listen("LA", _a);
            Listen("LB", _b);
            RecordChangedEvent();
            GivenStackIs(_a);
            State(_b).OnEnter = Once(() => _machine.Switch(_c));

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "A.Exit", "B.Enter",
                "LA.OnExit", "LB.OnEnter", "Changed A>B Switch", "LA.OnChanged A>B Switch", "LB.OnChanged A>B Switch",
                "B.Exit", "C.Enter",
                "LB.OnExit", "Changed B>C Switch", "LA.OnChanged B>C Switch", "LB.OnChanged B>C Switch",
            }));
            Assert.That(_machine.Current, Is.SameAs(_c));
        }

        [TestCase("OnExit")]
        [TestCase("OnEnter")]
        [TestCase("Changed event")]
        [TestCase("OnChanged")]
        public void C2_PushRequestedByAListenerDuringTheSignal_RunsAfterEveryOtherListenerGotThatSignal(string requestIn)
        {
            bool armed = false;
            Action request = () =>
            {
                if (!armed)
                    return;

                armed = false;
                _machine.Push(_x);
            };
            Action<StateChanged> requestOnSignal = _ => request();

            // L1 is first in every step of the signal and records nothing itself.
            _machine.Subscribe(new[] { requestIn == "OnEnter" ? _b : _a },
                onEnter: requestIn == "OnEnter" ? request : null,
                onExit: requestIn == "OnExit" ? request : null,
                onChanged: requestIn == "OnChanged" ? requestOnSignal : null);
            if (requestIn == "Changed event")
                _machine.Changed += requestOnSignal;
            Listen("L2", _a);
            Listen("L3", _b);
            RecordChangedEvent();
            GivenStackIs(_a);
            armed = true;

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "A.Exit", "B.Enter",
                "L2.OnExit", "L3.OnEnter", "Changed A>B Switch", "L2.OnChanged A>B Switch", "L3.OnChanged A>B Switch",
                "X.Enter",
                "L3.OnPaused", "Changed B>X Push", "L2.OnChanged B>X Push", "L3.OnChanged B>X Push",
            }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b, _x }));
        }

        [Test]
        public void C3_TwoRequestsQueuedDuringATransition_RunInTheOrderTheyWereRequested()
        {
            RecordChangedEvent();
            GivenStackIs(_a);
            State(_b).OnEnter = Once(() =>
            {
                _machine.Push(_x);
                _machine.Pop();
            });

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "A.Exit", "B.Enter", "Changed A>B Switch",
                "X.Enter", "Changed B>X Push",
                "X.Exit", "Changed X>B Pop",
            }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b }));
        }

        [Test]
        public void C4_SwitchRequestedInsideExit_EntersTheStateOfTheCurrentTransitionFirst_ThenGoesOn()
        {
            RecordChangedEvent();
            GivenStackIs(_a);
            State(_a).OnExit = Once(() => _machine.Switch(_c));

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "A.Exit", "B.Enter", "Changed A>B Switch",
                "B.Exit", "C.Enter", "Changed B>C Switch",
            }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _c }));
        }

        [Test]
        public void C5_StatesThatKeepSwitchingToEachOther_StopAtTheLimit_WithAnError_AndDoNotHang()
        {
            int requests = 0;
            State(_a).OnEnter = () =>
            {
                if (requests++ < HardCap)
                    _machine.Switch(_b);
            };
            State(_b).OnEnter = () =>
            {
                if (requests++ < HardCap)
                    _machine.Switch(_a);
            };
            LogAssert.Expect(LogType.Error, LoopGuardError());

            _machine.Switch(_a);

            Assert.That(EnterCount(), Is.EqualTo(StateMachine.MaxTransitionsPerRun));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b }));
        }

        [Test]
        public void LoopGuard_AfterItTrips_TheDroppedRequestNeverRuns_AndTheNextRequestRunsNormally()
        {
            bool loop = true;
            int requests = 0;
            State(_a).OnEnter = () =>
            {
                if (loop && requests++ < HardCap)
                    _machine.Switch(_b);
            };
            State(_b).OnEnter = () =>
            {
                if (loop && requests++ < HardCap)
                    _machine.Switch(_a);
            };
            LogAssert.Expect(LogType.Error, LoopGuardError());
            _machine.Switch(_a);
            loop = false;
            _calls.Clear();

            _machine.Switch(_c);

            Assert.That(_calls, Is.EqualTo(new[] { "B.Exit", "C.Enter" }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _c }));
        }

        [Test]
        public void LoopGuard_ExactlyTheLimitInOneRun_RunsThemAllWithoutAnError()
        {
            int requests = 0;
            int allowed = StateMachine.MaxTransitionsPerRun - 1;
            State(_a).OnEnter = () =>
            {
                if (requests++ < allowed)
                    _machine.Switch(_b);
            };
            State(_b).OnEnter = () =>
            {
                if (requests++ < allowed)
                    _machine.Switch(_a);
            };

            _machine.Switch(_a);

            Assert.That(EnterCount(), Is.EqualTo(StateMachine.MaxTransitionsPerRun));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RequestMadeWhileAQueuedTransitionRuns_GoesToTheBackOfTheQueue()
        {
            RecordChangedEvent();
            GivenStackIs(_a);
            State(_b).OnEnter = Once(() =>
            {
                _machine.Push(_x);
                _machine.Push(_y);
            });
            State(_x).OnEnter = Once(() => _machine.Switch(_c));

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "A.Exit", "B.Enter", "Changed A>B Switch",
                "X.Enter", "Changed B>X Push",
                "Y.Enter", "Changed X>Y Push",
                "Y.Exit", "X.Exit", "B.Exit", "C.Enter", "Changed Y>C Switch",
            }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _c }));
        }

        [Test]
        public void RequestInsideATransition_ReturnsWithoutChangingTheStack()
        {
            GivenStackIs(_a);
            StateId currentRightAfterTheRequest = null;
            State(_b).OnEnter = Once(() =>
            {
                _machine.Switch(_c);
                currentRightAfterTheRequest = _machine.Current;
            });

            _machine.Switch(_b);

            Assert.That(currentRightAfterTheRequest, Is.SameAs(_b));
            Assert.That(_machine.Current, Is.SameAs(_c));
        }

        [Test]
        public void QueuedInvalidRequest_LogsLikeADirectCallWhenItsTurnComes_AndTheQueueGoesOn()
        {
            RecordChangedEvent();
            GivenStackIs(_a);
            State(_b).OnEnter = Once(() =>
            {
                _machine.Switch(null);
                _machine.Pop();
                _machine.Push(_x);
            });
            LogAssert.Expect(LogType.Error, new Regex("StateMachine.Switch: StateId is null"));
            LogAssert.Expect(LogType.Warning, new Regex("'B' is the only state"));
            RecordLogs();

            _machine.Switch(_b);

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "A.Exit", "B.Enter", "Changed A>B Switch",
                "log Error",
                "log Warning",
                "X.Enter", "Changed B>X Push",
            }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b, _x }));
        }

        [Test]
        public void CallbackThrows_TheExceptionReachesTheCaller_QueuedRequestsAreDropped_AndTheMachineStillWorks()
        {
            bool armed = false;
            _machine.Subscribe(new[] { _b }, onEnter: () =>
            {
                if (armed)
                    _machine.Push(_x);
            });
            _machine.Subscribe(new StateId[0], onChanged: _ =>
            {
                if (armed)
                    throw new InvalidOperationException("thrown by the test");
            });
            GivenStackIs(_a);
            armed = true;

            Assert.Throws<InvalidOperationException>(() => _machine.Switch(_b));
            armed = false;

            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b }));
            _calls.Clear();

            _machine.Switch(_c);

            Assert.That(_calls, Is.EqualTo(new[] { "B.Exit", "C.Enter" }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _c }));
        }

        [Test]
        public void RequestInsideTheSyncCallbackOfSubscribe_RunsAfterTheSync_BeforeSubscribeReturns()
        {
            GivenStackIs(_a, _x);

            StateSubscription listener = _machine.Subscribe(new[] { _a },
                onEnter: () =>
                {
                    _calls.Add("L.OnEnter");
                    _machine.Switch(_b);
                },
                onExit: () => _calls.Add("L.OnExit"),
                onPaused: () => _calls.Add("L.OnPaused"),
                onResumed: () => _calls.Add("L.OnResumed"),
                onChanged: signal => _calls.Add($"L.OnChanged {Describe(signal)}"));

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "L.OnEnter", "L.OnPaused",
                "X.Exit", "A.Exit", "B.Enter",
                "L.OnExit", "L.OnChanged X>B Switch",
            }));
            Assert.That(_machine.Stack, Is.EqualTo(new[] { _b }));
            Assert.That(listener.Status, Is.EqualTo(StateListenerStatus.Inactive));
        }

        HookedState State(StateId id) => _states[id];

        int EnterCount() => _calls.Count(call => call.EndsWith(".Enter"));

        static Regex LoopGuardError()
        {
            return new Regex($"stopped after {StateMachine.MaxTransitionsPerRun} transitions in one run");
        }

        /// <summary>Subscribes a listener that records every callback as "name.Callback", OnChanged with its signal.</summary>
        StateSubscription Listen(string name, params StateId[] states)
        {
            return _machine.Subscribe(states,
                onEnter: () => _calls.Add($"{name}.OnEnter"),
                onExit: () => _calls.Add($"{name}.OnExit"),
                onPaused: () => _calls.Add($"{name}.OnPaused"),
                onResumed: () => _calls.Add($"{name}.OnResumed"),
                onChanged: signal => _calls.Add($"{name}.OnChanged {Describe(signal)}"));
        }

        void RecordChangedEvent()
        {
            _machine.Changed += signal => _calls.Add($"Changed {Describe(signal)}");
        }

        /// <summary>Records every Unity log as "log Type" in the same list as the calls, until TearDown.</summary>
        void RecordLogs()
        {
            _logRecorder = (message, stackTrace, type) => _calls.Add($"log {type}");
            Application.logMessageReceived += _logRecorder;
        }

        /// <summary>Builds the stack bottom → top (Switch to the first id, Push the rest) and forgets what that recorded.</summary>
        void GivenStackIs(StateId bottom, params StateId[] above)
        {
            _machine.Switch(bottom);
            foreach (StateId id in above)
                _machine.Push(id);

            _calls.Clear();
        }

        /// <summary>Wraps a request so that a state entered or exited again does not ask a second time.</summary>
        static Action Once(Action action)
        {
            bool done = false;
            return () =>
            {
                if (done)
                    return;

                done = true;
                action();
            };
        }

        static string Describe(StateChanged signal)
        {
            return $"{NameOf(signal.From)}>{NameOf(signal.To)} {signal.Kind}";
        }

        static string NameOf(StateId id) => id != null ? id.name : "none";

        StateId CreateState(string name)
        {
            StateId id = ScriptableObject.CreateInstance<StateId>();
            id.name = name;
            var state = new HookedState(name, _calls);
            _states[id] = state;
            _machine.Register(id, state);
            return id;
        }

        /// <summary>Records "name.Enter" / "name.Exit", then runs the hook of the test, if any.</summary>
        sealed class HookedState : IState
        {
            readonly string _name;
            readonly List<string> _calls;

            public HookedState(string name, List<string> calls)
            {
                _name = name;
                _calls = calls;
            }

            public Action OnEnter { get; set; }

            public Action OnExit { get; set; }

            public void Enter()
            {
                _calls.Add($"{_name}.Enter");
                OnEnter?.Invoke();
            }

            public void Exit()
            {
                _calls.Add($"{_name}.Exit");
                OnExit?.Invoke();
            }
        }
    }
}
