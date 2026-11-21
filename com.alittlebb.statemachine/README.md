# ALittleBB State Machine

A small state machine for Unity. A state is identified by a `StateId` asset, the machine keeps a stack of them,
and anything that cares about a state (a UI panel, music, input) listens for it, from code or from the Inspector.

- `Switch` replaces the current state. `Push` puts a state on top of the current one without exiting it, and `Pop` goes back to it.
- A listener is told when its state is entered, exited, paused (covered by a pushed state) and resumed.
- `StateMachineHost` and `StateListener` are components, so a scene can be wired without writing a script.
- A transition requested in the middle of another one is queued and runs after it.

Package id `com.alittlebb.statemachine`, namespace `ALittleBB.StateMachines`, Unity 6000.0 or newer. The runtime has no dependency on another package.

## Contents

- [Install](#install)
- [The pieces](#the-pieces)
- [Switch, Push and Pop](#switch-push-and-pop)
- [Listen from code](#listen-from-code)
- [Set up a StateListener in the Inspector](#set-up-a-statelistener-in-the-inspector)
- [Active, paused and inactive](#active-paused-and-inactive)
- [Nested transitions are queued](#nested-transitions-are-queued)
- [Sample: Menu, Gameplay and Pause](#sample-menu-gameplay-and-pause)
- [Tests](#tests)

## Install

In the Package Manager window, choose **+ > Install package from disk...** and pick this folder's `package.json`,
or add the package to `Packages/manifest.json` yourself:

```json
"com.alittlebb.statemachine": "file:../com.alittlebb.statemachine"
```

From a git repository that holds the package in a subfolder, use **+ > Install package from git URL...** with
`<repository url>?path=/com.alittlebb.statemachine`.

## The pieces

| Type | What it is |
|---|---|
| `StateId` | A ScriptableObject asset that names one state. Create it with **Assets > Create > ALittleBB > State Machine > State Id**. Two ids are the same state only when they are the same asset. |
| `StateMachine` | A plain C# class. It holds the stack and sends the signals. `Current` is the top of the stack, `Stack` is a read-only view of it, bottom → top. |
| `IState` | Optional logic of a state: `Enter()` and `Exit()`. Bind it to an id with `machine.Register(id, state)`. A `StateId` works without one. |
| `StateMachineHost` | A component that owns one `StateMachine` (`host.Machine`), enters its **Initial State** in `Start`, and has `Switch`, `Push` and `Pop` methods a UnityEvent can call. In Play mode its Inspector shows the stack. |
| `StateListener` | A component that raises UnityEvents (**On Enter**, **On Exit**, **On Paused**, **On Resumed**) for a set of `StateId`. |
| `StateChanged` | The signal of one transition: `From`, `To` and `Kind` (`StateChangeKind.Switch`, `Push` or `Pop`). |
| `StateSubscription` | What `Subscribe` returns. `Status` is `StateListenerStatus.Inactive`, `Active` or `Paused`; `Dispose()` unsubscribes. |

## Switch, Push and Pop

With a `StateMachineHost` in the scene:

```csharp
using ALittleBB.StateMachines;
using UnityEngine;

public class GameFlow : MonoBehaviour
{
    [SerializeField] StateMachineHost host;
    [SerializeField] StateId mainMenu;
    [SerializeField] StateId gameplay;
    [SerializeField] StateId pause;

    // MainMenu → Gameplay. Stack: [Gameplay]
    public void StartGame() => host.Switch(gameplay);

    // Gameplay stays on the stack, paused. Stack: [Gameplay, Pause]
    public void OpenPause() => host.Push(pause);

    // Back to Gameplay, which is resumed, not entered again. Stack: [Gameplay]
    public void ClosePause() => host.Pop();

    // From anywhere: exits Pause, then Gameplay, and enters MainMenu. Stack: [MainMenu]
    public void QuitToMenu() => host.Switch(mainMenu);
}
```

`host.Switch`, `host.Push` and `host.Pop` call the same methods on `host.Machine`. Without a host, make the machine yourself:

```csharp
var machine = new StateMachine();
machine.Register(gameplay, new GameplayState()); // optional: logic that runs on Enter and Exit

machine.Switch(mainMenu);
machine.Switch(gameplay);
machine.Push(pause);

StateId current = machine.Current;               // Pause
IReadOnlyList<StateId> stack = machine.Stack;    // [Gameplay, Pause], bottom → top

machine.Pop();                                   // Current is Gameplay again
```

```csharp
public class GameplayState : IState
{
    public void Enter() => Debug.Log("Gameplay entered");
    public void Exit() => Debug.Log("Gameplay exited");
}
```

What each call does:

| Call | Stack | `IState` calls | Signal |
|---|---|---|---|
| `Switch(id)` | Every state is removed, `id` becomes the only one. | `Exit` of every state on the stack, top → bottom, then `Enter` of `id`. | One, `Kind = Switch`. |
| `Push(id)` | `id` is added on top. | `Enter` of `id`. The state underneath does **not** get `Exit`. | One, `Kind = Push`. |
| `Pop()` | The top is removed. | `Exit` of the top. The state underneath does **not** get `Enter` again. | One, `Kind = Pop`. |

Calls that change nothing send no signal:

- `Switch` to the state that is already current does nothing at all.
- `Push` of a state that is already on the stack logs an error; a state can be on the stack only once.
- `Pop` of the last state, or with no state, logs a warning.
- `Switch` or `Push` with a null id logs an error.

With a host, register the `IState` of the Initial State in `Awake` or `OnEnable`: the host enters it in its `Start`.
The host does not enter the Initial State when something already switched before that `Start`. Nothing is sent when
a host is destroyed: no `Exit`, no signal.

## Listen from code

`Subscribe` takes the set of states the listener belongs to and the callbacks it wants. All callbacks are optional.

```csharp
using ALittleBB.StateMachines;
using UnityEngine;

public class GameplayMusic : MonoBehaviour
{
    [SerializeField] StateMachineHost host;
    [SerializeField] StateId gameplay;
    [SerializeField] AudioSource music;

    StateSubscription _subscription;

    void Awake()
    {
        _subscription = host.Machine.Subscribe(
            new[] { gameplay },
            onEnter: () => music.Play(),
            onExit: () => music.Stop(),
            onPaused: () => music.Pause(),      // another state was pushed on top of Gameplay
            onResumed: () => music.UnPause(),   // that state was popped
            onChanged: change => Debug.Log($"{change.Kind}: {NameOf(change.From)} → {NameOf(change.To)}"));
    }

    void OnDestroy() => _subscription.Dispose();

    static string NameOf(StateId id) => id != null ? id.name : "none";
}
```

- `host.Machine` exists before any `Awake`, so subscribing in `Awake`, `OnEnable` or `Start` all work, whatever the script order.
- `onChanged` is called for every transition of the machine, whatever the listener's status. `change.From` is null for the first transition.
- `Dispose()` unsubscribes. No callback is called after it, not even `onExit`. It is safe to call inside a callback.
- `_subscription.Status` tells where the listener stands: `Inactive`, `Active` or `Paused`.

To hear every transition without a set of states, use the event:

```csharp
host.Machine.Changed += change => Debug.Log($"{change.Kind} to {change.To.name}");
```

Order within one transition: every listener that is paused or exited is told first, then every listener that is
entered or resumed, then `Changed` is raised, then `onChanged` is called for every listener. Each step runs in
subscribe order.

## Set up a StateListener in the Inspector

`StateListener` is `Subscribe` as a component: the same rules, with UnityEvents instead of callbacks.

1. Create the `StateId` assets: **Assets > Create > ALittleBB > State Machine > State Id** (`MainMenu`, `Gameplay`, `Pause`).
2. On an empty GameObject, **Add Component > ALittleBB > State Machine > State Machine Host** and set **Initial State** (`MainMenu`).
3. On a GameObject that stays active, for example the parent of the panel, **Add Component > ALittleBB > State Machine > State Listener**.
4. **Host**: drag the host in. Leave it empty to use the `StateMachineHost` on the same GameObject or on its nearest parent (this is what a prefab needs).
5. **States**: add the `StateId` the listener belongs to (`Gameplay`). With more than one, the listener stays active while the machine moves between them.
6. **On Enter**: press **+**, drag the panel in, choose **GameObject > SetActive** and tick the box. **On Exit**: the same with the box unticked.
7. **On Paused** and **On Resumed** are optional: use them for what should happen while another state is pushed on top, such as dimming the panel or pausing a sound.
8. To change state from a UI Button, add an entry to its **On Click**, drag the host in and choose **StateMachineHost > Switch (StateId)**, **Push (StateId)** or **Pop ()**, then pick the `StateId`.

Things to know:

- The listener subscribes in `Awake` and unsubscribes in `OnDestroy`. Its events are still raised while its GameObject is inactive or the component is disabled, so a panel may hide itself in **On Exit** and show itself again in **On Enter**.
- Unity does not run `Awake` on a GameObject that **starts** inactive. A listener there gets nothing until the GameObject is activated for the first time. For a panel that starts hidden, put the listener on an object that starts active and point the events at the panel, as in step 3.
- A panel that is not part of the first state never gets **On Exit** at startup, so set its active checkbox in the scene to what it should be before the first state is entered.
- **Host** and **States** are read once, in `Awake`. To set them from code, do it before the GameObject is first activated.
- With no **Host** and no host on a parent, the listener logs one error and never raises an event.
- From code, add handlers the usual way: `listener.OnEnter.AddListener(...)`, and the same for `OnExit`, `OnPaused` and `OnResumed`.

## Active, paused and inactive

A listener (a `Subscribe` call or a `StateListener`) has a set of states and, from the stack, one status:

| Status | When |
|---|---|
| **Active** | The top of the stack is one of its states. |
| **Paused** | One of its states is on the stack, but the top is not one of them. |
| **Inactive** | None of its states is on the stack. |

After each transition the status is worked out again, and a change of status is what calls the listener:

| Status change | Callback | UnityEvent |
|---|---|---|
| Inactive → Active | `onEnter` | On Enter |
| Active → Paused | `onPaused` | On Paused |
| Paused → Active | `onResumed` | On Resumed |
| Active → Inactive, Paused → Inactive | `onExit` | On Exit |
| No change | none | none |

Example, for a listener of `{Gameplay}`:

| Call | Stack, bottom → top | Status | Called |
|---|---|---|---|
| `Switch(MainMenu)` | MainMenu | Inactive | |
| `Switch(Gameplay)` | Gameplay | Active | On Enter |
| `Push(Pause)` | Gameplay, Pause | Paused | On Paused |
| `Pop()` | Gameplay | Active | On Resumed |
| `Push(Pause)` | Gameplay, Pause | Paused | On Paused |
| `Switch(MainMenu)` | MainMenu | Inactive | On Exit |

What follows from the rule:

- `Push` pauses the listeners of the state underneath; it does not exit them. `Pop` resumes them; it does not enter them again.
- Leaving while paused (the last row) calls On Exit only. On Resumed is not called first.
- Moving between two states of the same set calls nothing: a listener of `{Gameplay, Cutscene}` stays active through `Switch(Cutscene)`.
- A listener that subscribes late is synced right away: when one of its states is already on the stack it gets On Enter, followed by On Paused when that state is not on top.

## Nested transitions are queued

`Switch`, `Push` and `Pop` called from outside a transition finish before they return. Called while a transition
is running, that is inside `IState.Enter`, `IState.Exit`, a `Changed` handler, a `Subscribe` callback or a
`StateListener` event, they only queue the request and return. The running transition first tells every listener;
then the queued requests run one by one, in the order they were made (first in, first out).

```csharp
public class LoadingState : IState
{
    readonly StateMachine _machine;
    readonly StateId _gameplay;

    public LoadingState(StateMachine machine, StateId gameplay)
    {
        _machine = machine;
        _gameplay = gameplay;
    }

    public void Enter()
    {
        // Queued: the machine finishes entering Loading and tells its listeners, then switches to Gameplay.
        _machine.Switch(_gameplay);
    }

    public void Exit() { }
}
```

- Listeners always see the transitions one at a time and in order: here `Changed` is raised for Loading, then for Gameplay.
- A queued request is checked against the stack when its turn comes, exactly like a direct call, so it can turn out to do nothing or to log the same warning.
- One outside call runs at most `StateMachine.MaxTransitionsPerRun` (64) requests, itself included and counting the ones that do nothing. When more are still queued, an error is logged and they are dropped, so two states that keep switching to each other cannot hang the game.
- When an `Enter`, `Exit` or callback throws, the requests still queued are dropped and the exception reaches the outside caller.

## Sample: Menu, Gameplay and Pause

One scene that goes MainMenu → Gameplay → Pause popup and back. It has no script: three `StateListener` components
show and hide the panels, and the buttons call the `StateMachineHost` from their **On Click**.

Import it:

1. Open the Package Manager window (**Window > Package Management > Package Manager**; **Window > Package Manager** in older versions).
2. Select **ALittleBB State Machine** under **In Project** and open the **Samples** tab.
3. Press **Import** next to **Menu, Gameplay and Pause**.
4. Open `Assets/Samples/ALittleBB State Machine/<version>/Menu, Gameplay and Pause/MenuGameplayPause.unity` and press Play.

What the sample needs:

- **Unity UI** (`com.unity.ugui`). The texts are legacy `Text` components, so TextMeshPro and its essential resources are not needed.
- **Input System** (`com.unity.inputsystem`) with **Project Settings > Player > Active Input Handling** set to *Input System Package (New)* or *Both*: the scene's EventSystem uses the **Input System UI Input Module**. In a project that uses the old Input Manager only, replace that component with a **Standalone Input Module**.
- Nothing from a render pipeline: the camera only clears the screen.

What is in the scene:

| Object | Components | What it does |
|---|---|---|
| `State Machine` | `StateMachineHost` | Initial State = `MainMenu`. Select it in Play mode to see the stack. |
| `Canvas/Main Menu` | `StateListener` for `MainMenu` | On Enter / On Exit show and hide `Main Menu Panel`. **Play** calls `Switch(Gameplay)`. |
| `Canvas/Gameplay HUD` | `StateListener` for `Gameplay` | On Enter / On Exit show and hide `Gameplay HUD Panel`. On Paused and On Resumed change its status text. **Pause** calls `Push(Pause)`, **Main Menu** calls `Switch(MainMenu)`. |
| `Canvas/Pause Popup` | `StateListener` for `Pause` | On Enter / On Exit show and hide `Pause Panel`. **Resume** calls `Pop()`, **Main Menu** calls `Switch(MainMenu)`. |

Things to try:

- Press **Pause**: the HUD stays on screen under the popup and its status text changes, because the HUD's listener got On Paused, not On Exit.
- Press **Main Menu** in the popup: one `Switch` exits both Pause and Gameplay, and both panels are hidden by their On Exit.
- The listeners sit on objects that stay active (`Main Menu`, `Gameplay HUD`, `Pause Popup`), and the panels they show and hide are their children. The HUD and Pause panels start hidden in the scene.

The three `StateId` assets are in the sample's `States` folder.

## Tests

The package has EditMode and PlayMode tests in `Tests/`. To run them in a project that uses the package, add it to
`testables` in `Packages/manifest.json`:

```json
"testables": ["com.alittlebb.statemachine"]
```

The sample tests import the sample through the Package Manager, play it by clicking its buttons, and delete the
imported copy when the run ends. They refuse to run while another copy of the sample is in `Assets/`.
