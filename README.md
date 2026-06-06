# Block Jam 3D (Mojo Developer Case)

A small Block Jam 3D clone made for the Mojo developer case. Built with Unity 6000.0.62f1, URP.

You tap a unit on the board, it walks down into the 7-slot bar. Get three of the same color in the bar and they match and disappear. Clear the whole board to win. Fill the bar up with no match left and you lose.

## Running it

Open the project in Unity 6000.0.62f1, open `Assets/Scenes/Gameplay.unity`, hit Play. It runs from the editor, starts at the first level.

The current level is saved to disk, so it remembers where you were next time you launch. If you want to reset that while testing: **Tools > BlockJam > Reset Save**.

Controls are just tap/click on a unit. Win screen has a Continue button (next level), fail screen has Retry (same level again).

## How selection works

A unit can only be sent if it has a clear walking path out to the front edge of the board. I went with a path-to-exit check (BFS) instead of the simpler "has at least one free side" wording, because that's how the actual Block Jam 3D behaves and the brief says to use it as the main reference. A unit boxed inside a Box blocker can't move until the box is broken by a nearby clear.

## Structure

No singletons. `GameController` is the root that holds references to everything and wires them up in `Setup()`. That keeps the dependencies visible instead of hidden behind static instances.

```
Core/       GameController, GameStateMachine, InputRouter, CameraFitter
Gameplay/   BoardController, SlotController, Unit, MatchResolver, BlockChecker,
            ColorPalette, UnitPool, ObstaclePool, VfxPool, PooledVfx, PooledObstacle, Cell
Data/       LevelData, GameConfig, CellData, UnitColor
Pooling/    ObjectPool<T>
Save/       SaveSystem, SaveData
Util/       Tweener, Easing
UI/         UIManager
Editor/     LevelEditorWindow, SaveMenu
```

Quick rundown of the main ones:

- `BoardController` owns the grid, handles taps, does the path-to-exit check, and decides if a unit is allowed to be sent.
- `SlotController` owns the 7-slot bar: grouping, matching, and the match animation.
- `MatchResolver` just finds match groups, nothing else.
- `GameConfig` is a ScriptableObject holding the level list and all the tunable numbers (slot count, match size, animation timings, pool sizes). I didn't want magic numbers scattered around, so they all live here.
- `Tweener` is a small coroutine tween helper. No DOTween or any other third-party tween/DI/pooling lib, since the brief doesn't allow those.

## Object pooling

Everything that gets spawned goes through one generic `ObjectPool<T>`: units, boxes, and VFX. They're all pre-warmed at startup (sizes in GameConfig).

`ObjectPool<T>` is the only thing in the project that calls Instantiate/Destroy, and only during pre-warm. Once you're playing, nothing is instantiated or destroyed: units, boxes and effects are taken from the pool and returned to it. Changing levels reuses the same objects too. The slot floor cells are made once and just moved around per level.

## The slot/match logic

This is where most of the time went. A few things I had to get right:

**Capacity is checked the instant you tap, not when the unit lands.** It counts what's already in the bar plus what's in flight plus the new one, groups by color, and cancels every complete set of three (those are going to pop anyway). If what's left still doesn't fit in 7 slots, the tap is refused.

The reason it works this way: say you have 2 red, 2 blue, 2 purple and one slot left. If you tap red, that completes a red triple, so the room frees up right away and you can tap blue straight after. But if you drop a color into that last slot that *doesn't* complete a match, the bar is full and the next tap is blocked. You can't fill the last slot with a dead color and get bailed out by some unrelated match later. That run is over.

**Each matched trio pops on its own, where it is, as soon as its third unit arrives.** It doesn't wait for another group to finish. If a red triple and a blue triple complete around the same time, both pop in place at the same time instead of one queuing behind the other. The bar only slides the leftover units together *after* the pops are done, so a group that's about to match doesn't get yanked sideways first. After it compacts it re-checks for any new match the shift created (cascade).

**A popping trio still counts as occupying its slots until the animation actually finishes,** and new units that land are placed after any still-popping ones, so a fresh unit never lands on top of a trio that's mid-pop. The lose check, on the other hand, only looks at settled units, otherwise a match that just freed space could trigger a false game over while it's still animating.

Input is never fully locked while this resolves. One resolver runs at a time and units that land mid-resolve get picked up by it, so you can tap fast without breaking the bar's state.

## Animations and feedback

Units have Idle, Run, Jump and Die. While walking to a slot they kick up a little dust puff tinted to their own color. On a match the trio jumps up, gathers together in the air and pops with a VFX burst.

Win and fail screens pop in with a bit of scale overshoot and a shake. The fail screen dims the board but keeps the slot bar visible, shows a "No Slot Available!" line with an angry face, shakes the camera and fires a VFX, then shows the Retry button.

## Level editor

**Tools > BlockJam > Level Editor.** You can make new levels or edit existing ones:

- Pick a LevelData asset or create a new one.
- Paint cells: set a color, toggle a cell empty (a hole), or toggle a Box blocker on a unit.
- Resize the grid.
- It validates as you go: warns if a color count isn't a multiple of three (can't be solved) and shows the total unit count, so you can build a level that has both a winnable and a losable path.
- If you assign a GameConfig, it can drop a new level straight into the config's level list, so it's playable immediately without dragging it in by hand.

There are 5 levels included under `Assets/Levels/`.

## Saving

`SaveSystem` writes the current level index to `Application.persistentDataPath` as JSON and reads it back on launch. Reset it with **Tools > BlockJam > Reset Save**.
