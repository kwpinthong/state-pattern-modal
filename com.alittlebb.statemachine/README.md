# ALittleBB State Machine

A small state machine for Unity. A state is identified by a `StateId` asset, the machine keeps a stack of them,
and anything that cares about a state (a UI panel, music, input) listens for it, from code or from the Inspector.

- `Switch` replaces the current state. `Push` puts a state on top of the current one without exiting it, and `Pop` goes back to it.
- A listener is told when its state is entered, exited, paused (covered by a pushed state) and resumed.
- `StateMachineHost`, `StateListener`, `StateAnimatorBinding`, `StateAudioBinding` and `StateInputBinding` are components, so a scene can be wired without writing a script.
- A transition requested in the middle of another one is queued and runs after it.

Package id `com.alittlebb.statemachine`, namespace `ALittleBB.StateMachines`, Unity 6000.0 or newer. The runtime has no dependency on another package; `StateAnimatorBinding` uses the Animator of Unity's built-in Animation module and `StateAudioBinding` the built-in Audio module, which are enabled by default. `StateInputBinding` appears only in a project that has the Input System package.

## Contents

- [Install](#install)
- [The pieces](#the-pieces)
- [Switch, Push and Pop](#switch-push-and-pop)
- [Listen from code](#listen-from-code)
- [Set up a StateListener in the Inspector](#set-up-a-statelistener-in-the-inspector)
- [Active, paused and inactive](#active-paused-and-inactive)
- [Drive an Animator with StateAnimatorBinding](#drive-an-animator-with-stateanimatorbinding)
- [Turn action maps on and off with StateInputBinding](#turn-action-maps-on-and-off-with-stateinputbinding)
- [Play music with StateAudioBinding](#play-music-with-stateaudiobinding)
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
| `StateAnimatorBinding` | A component that drives the Animator on its GameObject from a list of bindings: at this moment of this `StateId`, set a trigger, a bool or an integer, or cross fade to a state. |
| `StateAudioBinding` | A component that plays background music on one Audio Source, with crossfades, and moves an Audio Mixer to snapshots, from a list of bindings: at this moment of this `StateId`, play a clip, stop the music or transition to a snapshot. |
| `StateInputBinding` | A component that enables and disables the action maps of an Input Action Asset with the states. Only with the Input System package. |
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
- To open and close a panel with an animation instead of **SetActive**, see [Drive an Animator with StateAnimatorBinding](#drive-an-animator-with-stateanimatorbinding).

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

## Drive an Animator with StateAnimatorBinding

`StateAnimatorBinding` drives the Animator on its GameObject from the states of a host, from the Inspector and
without code. It holds a list of bindings, and each one says: at this moment of this state, do this to the Animator.

1. On the GameObject that has the **Animator**, **Add Component > ALittleBB > State Machine > State Animator Binding**. The Animator is always the one on the same GameObject; there is no field for it.
2. **Host**: drag the host in. Leave it empty to use the `StateMachineHost` on the same GameObject or on its nearest parent.
3. **Bindings**: press **+** once for each thing the Animator should do, and fill the entry in:

| Field | What to set |
|---|---|
| **State** | The `StateId` the binding reacts to. An entry without a state does nothing. |
| **Moment** | When to act: **Enter**, **Exit**, **Paused** or **Resumed** (first table below). |
| **Action** | What to do: **Set Trigger**, **Set Bool**, **Set Integer** or **Cross Fade** (second table below). |
| **Name** | The name of the Animator parameter, or of the Animator state for Cross Fade. |
| **Bool Value** | The value for Set Bool. |
| **Int Value** | The value for Set Integer. |
| **Duration** | The length of the Cross Fade, in seconds. 0 switches at once. |

The moments are those of a listener of that one state (see [Active, paused and inactive](#active-paused-and-inactive)):

| Moment | When |
|---|---|
| Enter | The state becomes the current state, and it was not on the stack before. |
| Exit | The state leaves the stack, whether it was the current state or covered by another. |
| Paused | Another state is pushed on top of the state. |
| Resumed | The state on top is popped and the state is the current state again. |

| Action | What it calls | What the Animator Controller needs |
|---|---|---|
| Set Trigger | `animator.SetTrigger(Name)` | A Trigger parameter called **Name**. |
| Set Bool | `animator.SetBool(Name, Bool Value)` | A Bool parameter called **Name**. |
| Set Integer | `animator.SetInteger(Name, Int Value)` | An Int parameter called **Name**. |
| Cross Fade | `animator.CrossFadeInFixedTime(Name, Duration)` | A state called **Name**. It is looked up on every layer and the first layer that has it is used. |

For example, a HUD that opens with `Gameplay` and dims while another state is pushed on top of it (the sample's HUD):

| State | Moment | Action | Name | Bool Value |
|---|---|---|---|---|
| Gameplay | Enter | Set Bool | `IsOpen` | on |
| Gameplay | Exit | Set Bool | `IsOpen` | off |
| Gameplay | Paused | Set Bool | `IsDimmed` | on |
| Gameplay | Resumed | Set Bool | `IsDimmed` | off |
| Gameplay | Exit | Set Bool | `IsDimmed` | off |

The last entry is there because leaving while paused is Exit only: Resumed is not called first, so without it
`IsDimmed` would still be on the next time `Gameplay` is entered.

Within one transition every Exit and Paused action is applied before any Enter and Resumed action, whatever the
order of the list. Entries that share a state and a moment are applied in list order.

### A transition does not wait for the animation

A binding only starts an animation: it sets a parameter or begins a cross fade and returns. `Switch`, `Push` and
`Pop` do not wait for the animation to finish. They are done as soon as the stack has changed and the listeners
have been told, so the new state is already the current state while the old panel is still fading out. What
follows from that:

- Do not let the animation decide what the player can do. A panel that is fading out belongs to a state that was already left, so stop its input at once and let only the look take time. The sample does this with a second Animator layer that switches **Interactable** and **Blocks Raycasts** of the panel's `CanvasGroup` with a transition of 0 seconds, while the first layer fades its alpha.
- State changes can come faster than the animations. For "open" and "closed", prefer a Bool parameter to a pair of Triggers: a bool says where the Animator has to end up, so it gets there whatever was playing. Triggers queue up instead: after Play → Main Menu → Play in one frame, a trigger-driven panel ends open for a state that was left. Transitions that can be interrupted (**Interruption Source** = *Next State*) let a change take over a fade that is still running.
- To make something wait for an animation, do it yourself: for example, an Animation Event at the end of the clip calls a method of yours, and that method calls `host.Switch`.

### Enabled, disabled and inactive

`StateAnimatorBinding` follows the host while it is enabled on an active GameObject, from `OnEnable` to `OnDisable`.
A `StateListener` keeps listening while its GameObject is inactive; this component does not, because the Animator
does not either: Unity's Animator forgets its parameters and goes back to its default states while its GameObject
is inactive.

- **Enter is applied again each time the component is enabled.** It is synced with the stack like a new listener: every Enter action of a state that is on the stack is applied (triggers and cross fades included), followed by the Paused actions when that state is covered by another.
- **What happens while it is disabled is dropped.** An Exit or a Paused that comes while the component is disabled or its GameObject is inactive is not applied, then or later.
- **So keep an animated panel active**, and let the animation hide it: give the panel a `CanvasGroup` and make the closed pose alpha 0 with **Interactable** and **Blocks Raycasts** off, so that a closed panel cannot take a click. A panel that is deactivated with `SetActive(false)` when its state exits never plays its close animation, and its Animator starts from the default state the next time.
- An Animator with **Keep Animator State On Disable** keeps what it had, so there the Enter actions are applied on top of it.
- A binding whose parameter or state is not in the Animator Controller, or whose parameter is of another type, logs one warning the first time it is needed and does nothing; the other bindings still work.
- Without an Animator on the GameObject, or without a host, the component logs one error and drives nothing.
- **Host** and the states of **Bindings** are read each time the component is enabled. To change them from code, do it while the component is disabled.

## Turn action maps on and off with StateInputBinding

`StateInputBinding` enables and disables the action maps of one **Input Action Asset** of the Input System with the
states of a host. It lives in its own assembly, `ALittleBB.StateMachines.InputSystem`, which compiles only when the
project has the Input System package (`com.unity.inputsystem`); without it the rest of the package works and the
component is simply not there: it is missing from the **Add Component** menu.

1. On any GameObject, **Add Component > ALittleBB > State Machine > State Input Binding**.
2. **Host**: drag the host in. It must be set: unlike the other components, it is not looked up on the same GameObject or its parents.
3. **Actions**: the Input Action Asset whose maps it drives.
4. **Entries**: press **+** once per action map, and fill the entry in:

| Field | What to set |
|---|---|
| **States** | The states during which the map is enabled. With more than one, the map stays enabled while the machine moves between them. |
| **Action Map** | The name of the action map in the asset, as the Input Actions editor shows it. |
| **When Paused** | What happens while the state is paused under a pushed state: *Disable Map* (the default) or *Keep Map Enabled*. |

For example, with the maps `UI` and `Player` of one asset:

| States | Action Map | When Paused |
|---|---|---|
| MainMenu, Pause | `UI` | Disable Map |
| Gameplay | `Player` | Disable Map |

### When a map is enabled

The status of an entry follows the rule of [Active, paused and inactive](#active-paused-and-inactive) for its set
of states, and the map is changed before `Switch`, `Push` or `Pop` returns, so code that runs right after the call
already sees the new maps:

| The states of the entry | Map |
|---|---|
| One of them becomes the current state (Enter) | Enabled. |
| The current state moves to another state of the same entry | Not touched, so a key held across the change is not cancelled. |
| A `Push` covers it (Paused) | *Disable Map*: disabled. *Keep Map Enabled*: stays enabled. |
| The `Pop` of the state on top makes it current again (Resumed) | Enabled again. |
| None of them is on the stack any more (Exit), paused or not | Disabled. |

With the example above, `Push(Pause)` from Gameplay disables `Player` and enables `UI`; after the `Pop` back to Gameplay, `Player` is enabled again and `UI` is disabled.

- List every state that shares a map in one entry. Two entries with the same map each enable and disable it on their own, so moving from a state of one to a state of the other disables the map and enables it again, which cancels a key that is held.
- Action maps that are in no entry are never touched: their enabled state is whatever other code set.
- Transitions requested during a transition are queued (see [Nested transitions are queued](#nested-transitions-are-queued)); once the queue is empty, the maps match the last state.

### Enabled, disabled and destroyed

- Each time the component is enabled (first activation, after `Instantiate`, or enabled again) it is synced with the stack: the maps of the entries that are active now are enabled, and every other map of its entries is disabled, whatever other code set before.
- When the component is disabled or destroyed, every map of its entries is disabled. What happens while it is disabled is not applied, then or later.
- **Host**, **Actions** and **Entries** are read each time the component is enabled. To change them from code, do it while it is disabled.
- Wrong settings log a warning, once for each problem, and throw nothing: without **Host** or **Actions** no map is driven; an entry whose **Action Map** is not in the asset does nothing, and the other entries still work.

## Play music with StateAudioBinding

`StateAudioBinding` plays the background music (BGM) of the states of a host on one Audio Source, and can move an
Audio Mixer to a snapshot, from the Inspector and without code. Like `StateAnimatorBinding`, it holds a list of
bindings, and each one says: at this moment of this state, do this to the music.

1. **Add Component > ALittleBB > State Machine > State Audio Binding** on any GameObject.
2. **Host**: drag the host in. Leave it empty to use the `StateMachineHost` on the same GameObject or on its nearest parent.
3. **Audio Source**: the Audio Source that plays the music. It must be set for **Play Bgm** and **Stop Bgm**. Its **Volume** is the volume of the music, and its **Output** is the mixer group the music goes to.
4. **Bindings**: press **+** once for each thing to do, and fill the entry in:

| Field | What to set |
|---|---|
| **State** | The `StateId` the binding reacts to. An entry without a state does nothing. |
| **Moment** | When to act: **Enter**, **Exit**, **Paused** or **Resumed**, as for [StateAnimatorBinding](#drive-an-animator-with-stateanimatorbinding). |
| **Action** | **Play Bgm**, **Stop Bgm** or **Transition To Snapshot** (table below). |
| **Clip** | The clip for Play Bgm. |
| **Snapshot** | The Audio Mixer snapshot for Transition To Snapshot. |
| **Seconds** | The crossfade of Play Bgm, the fade out of Stop Bgm, or the length of Transition To Snapshot. 0 changes at once. |
| **Time Scale** | For Transition To Snapshot: **Ignore Time Scale** (the default) or **Follow Time Scale** ([below](#snapshot-transitions-ignore-or-follow-timetimescale)). |

| Action | What it does |
|---|---|
| Play Bgm | Plays **Clip** looped on the Audio Source. When that clip is already playing, it goes on from where it is instead of starting again, so two states with the same music switch without a break. With **Seconds** > 0 it crossfades from the music that plays. |
| Stop Bgm | Stops the music, fading it out first when **Seconds** > 0. |
| Transition To Snapshot | Moves the Audio Mixer of **Snapshot** to that snapshot over **Seconds**, on the clock chosen by **Time Scale**. It does not need the Audio Source. |

For example, music for the menu and for gameplay, muffled while the pause popup is pushed on top:

| State | Moment | Action | Clip / Snapshot | Seconds |
|---|---|---|---|---|
| MainMenu | Enter | Play Bgm | `MenuTheme` | 1 |
| Gameplay | Enter | Play Bgm | `GameplayTheme` | 1 |
| Gameplay | Paused | Transition To Snapshot | `Muffled` | 0.3 |
| Gameplay | Resumed | Transition To Snapshot | `Normal` | 0.3 |
| Gameplay | Exit | Transition To Snapshot | `Normal` | 0 |

As in `StateAnimatorBinding`, the last entry is there because leaving while paused is Exit only. Within one
transition every Exit and Paused action is applied before any Enter and Resumed action, whatever the order of the
list; entries that share a state and a moment are applied in list order.

### Crossfades run on real time and do not hold a transition

- A crossfade fades the music that plays out while the new clip fades in up to the volume of the Audio Source, then only the new clip plays. The second clip plays on a second Audio Source that is added, hidden in the Inspector, on the GameObject of the Audio Source that is set. It copies that Audio Source's settings at the start of each fade: mixer group, spatial blend, priority, pitch, 3D settings and so on.
- Fades run on unscaled time: they use real seconds and go on while `Time.timeScale` is 0, so music fades as expected on a pause screen that stops time.
- `Switch`, `Push` and `Pop` do not wait for the fade. The transition is done and its signal is sent while the fade has just started.
- A Play Bgm during a fade starts a new fade from where the volumes are, towards the newest clip. A Play Bgm or Stop Bgm with **Seconds** 0 ends the fade at once.

### Snapshot transitions ignore or follow Time.timeScale

Each Transition To Snapshot binding chooses its clock with **Time Scale**, whatever the **Update Mode** set on the
Audio Mixer asset:

- **Ignore Time Scale**, the default: the transition runs on real time, like the BGM fades. It takes **Seconds** of real time and goes on while `Time.timeScale` is 0, so a Paused binding muffles the music on a pause screen that stops time.
- **Follow Time Scale**: the transition runs on game time. `Time.timeScale` slows it (at 0.5 it takes twice **Seconds** of real time), and it stands still while `Time.timeScale` is 0, then goes on when time runs again.
- **Seconds** 0 changes the mixer on the next frame with either choice, even while `Time.timeScale` is 0.

The transition is the mixer's own, so it starts from the values the mixer has: a transition that starts during
another one goes on from where the mixer is, without a jump, and ends on its own snapshot. To run on the chosen clock,
the component sets the mixer's **Update Mode** to **Unscaled Time** (Ignore) or **Normal** (Follow) while the
transition runs, and puts back the Update Mode the mixer had when it ends. A snapshot transition never holds a state
change either. When the component is disabled or destroyed, its snapshot transition under way ends at once on its
snapshot, as a BGM fade does.

### Enabled, disabled and other scenes

- Each time the component is enabled it is synced with the stack as if the states on it had been entered one by one, bottom first: the Enter actions of each state, then its Paused actions when it is covered, then the next state up. So the music of the current state wins, whatever the order of the list, and plays as soon as the component starts, even for the Initial State the host entered before. A clip that is already playing goes on.
- What happens while it is disabled is not applied, then or later.
- Disabling or destroying the component does not stop the music. A fade under way ends at once where it was going: the new clip at full volume, or silence for Stop Bgm. A snapshot transition under way ends at once on its snapshot.
- **Host** and the states of **Bindings** are read each time the component is enabled. To change them from code, do it while it is disabled.
- A binding without its Audio Source, Clip or Snapshot logs one warning the first time it is needed and does nothing; the other bindings still work. Without a host, the component logs one warning and plays nothing.

The package does not keep the music across scenes. Loading another scene (not additively) destroys the Audio Source
with the rest of the old scene, and the music stops. To keep the BGM playing, put the Audio Source on an object that is not destroyed, for
example one you pass to `DontDestroyOnLoad`, yourself. Either keep the host and the `StateAudioBinding` on that object
too, or, since a scene cannot reference that object in the Inspector, save the binding of the new scene disabled
and give it the Audio Source from code before enabling it (`binding.AudioSource = music; binding.enabled = true;`). A Play Bgm of the clip that is already playing then goes on
without a break.

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

One scene that goes MainMenu → Gameplay → Pause popup and back. It has no script: three `StateAnimatorBinding`
components open and close the panels with an Animator, a `StateListener` changes the HUD's status text, and the
buttons call the `StateMachineHost` from their **On Click**.

Import it:

1. Open the Package Manager window (**Window > Package Management > Package Manager**; **Window > Package Manager** in older versions).
2. Select **ALittleBB State Machine** under **In Project** and open the **Samples** tab.
3. Press **Import** next to **Menu, Gameplay and Pause**.
4. Open `Assets/Samples/ALittleBB State Machine/<version>/Menu, Gameplay and Pause/MenuGameplayPause.unity` and press Play.

What the sample needs:

- **Unity UI** (`com.unity.ugui`). The texts are legacy `Text` components, so TextMeshPro and its essential resources are not needed.
- **Input System** (`com.unity.inputsystem`) with **Project Settings > Player > Active Input Handling** set to *Input System Package (New)* or *Both*: the scene's EventSystem uses the **Input System UI Input Module**. In a project that uses the old Input Manager only, replace that component with a **Standalone Input Module**.
- Unity's built-in Animation and UI modules (Animator, CanvasGroup), which are enabled by default.
- Nothing from a render pipeline: the camera only clears the screen.

What is in the scene:

| Object | Components | What it does |
|---|---|---|
| `State Machine` | `StateMachineHost` | Initial State = `MainMenu`. Select it in Play mode to see the stack. |
| `Canvas/Main Menu` | `CanvasGroup`, `Animator` (`Panel` controller), `StateAnimatorBinding` | `MainMenu` Enter sets `IsOpen` on, Exit sets it off. **Play** calls `Switch(Gameplay)`. |
| `Canvas/Gameplay HUD` | `CanvasGroup`, `Animator` (`GameplayHud` controller), `StateAnimatorBinding`, `StateListener` for `Gameplay` | `Gameplay` Enter / Exit set `IsOpen`, Paused / Resumed set `IsDimmed`, and Exit also sets `IsDimmed` off. The listener's On Enter, On Paused and On Resumed change the status text. **Pause** calls `Push(Pause)`, **Main Menu** calls `Switch(MainMenu)`. |
| `Canvas/Pause Popup` | `CanvasGroup`, `Animator` (`Panel` controller), `StateAnimatorBinding` | `Pause` Enter sets `IsOpen` on, Exit sets it off. **Resume** calls `Pop()`, **Main Menu** calls `Switch(MainMenu)`. |

The panels are never deactivated. Each one is a `CanvasGroup` that its Animator fades and switches off, see
[Enabled, disabled and inactive](#enabled-disabled-and-inactive). The two Animator Controllers are in the sample's
`Animations` folder, and each has two layers driven by the same Bool parameters:

| Layer | States | What it animates | Transitions |
|---|---|---|---|
| **Base Layer** | `Closed` (default), `Open`, and `Dimmed` in `GameplayHud` | Alpha of the `CanvasGroup`: 0, 1 and 0.5. | 0.2 seconds, and they can be interrupted. |
| **Input** | `Off` (default), `On` | **Interactable** and **Blocks Raycasts** of the `CanvasGroup`. `On` only while `IsOpen` is on and, in `GameplayHud`, `IsDimmed` is off. | 0 seconds. |

Why two layers: Unity keeps a bool property on until a blend between "on" and "off" has ended, so with one layer
a panel would still take clicks while it fades out, and a second click on **Resume** would call `Pop()` again.
With the **Input** layer a panel takes clicks exactly while its state says so, and only its look takes 0.2 seconds.

Things to try:

- Press **Pause**: the HUD dims and stays on screen under the popup, and its status text changes, because Gameplay was paused, not exited.
- Press **Main Menu** in the popup: one `Switch` exits both Pause and Gameplay, and both panels fade out. The HUD's binding list has a second Exit entry that sets `IsDimmed` off, because leaving while paused is Exit only.
- Click **Play**, **Pause** and **Main Menu** as fast as you can: a state change does not wait for a fade, and the panels still end where the stack is, because Bool parameters say where each Animator has to end up.
- Select a panel in Play mode and open the Animator window to watch its two layers.
- Before Play only the Main Menu is visible in the scene: the `CanvasGroup` of the HUD and of the popup is saved with alpha 0. Raise it in the Inspector to look at them; the Animator sets it again in Play mode.
- The Animators run on scaled time. In a game that sets `Time.timeScale` to 0 while paused, set their **Update Mode** to **Unscaled Time**, or the popup never fades in.

The three `StateId` assets are in the sample's `States` folder.

## Tests

The package has EditMode and PlayMode tests in `Tests/`; the tests of `StateInputBinding` (`Tests/InputSystem`) compile only with the Input System and use its test fixture. To run them in a project that uses the package, add it to
`testables` in `Packages/manifest.json`:

```json
"testables": ["com.alittlebb.statemachine"]
```

The sample tests import the sample through the Package Manager, play it by clicking its buttons, and delete the
imported copy when the run ends. They refuse to run while another copy of the sample is in `Assets/`.
