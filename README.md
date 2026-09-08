# material-physics-prototype

An experimental Unity project for learning game development systematically through small,
disposable prototypes.

**This is a laboratory, not a game.** Nothing here is intended to ship. Code is expected to be
thrown away once an experiment has answered its question.

## Project

| | |
|---|---|
| Unity | 6000.6.0f1 (Unity 6.6) |
| Render pipeline | Universal RP 17.6.0 |
| Dimension | 3D |
| Input | Input System package only (legacy `Input.*` API is disabled) |
| Colour space | Linear |

## Current direction

The first line of enquiry is **rigid ↔ rubber transformation**: a physics-driven body that can
switch between a rigid physical state and a rubber-like one.

The interesting question is not the visual change. It is whether changing *physical behaviour*
produces gameplay worth building on — impact absorption, bouncing, momentum, collision response,
and the interactions that emerge between them.

This is deliberately being approached in stages rather than as a "material system":

| Experiment | Question |
|---|---|
| [001](docs/experiments/001-rigid-vs-rubber-physical-behaviour.md) | Does changing a Rigidbody's physical properties from rigid-like to rubber-like produce meaningfully different and interesting physical behaviour? |
| 002 | Can the player deliberately control and exploit that behaviour? |
| 003 | Does *timing* the transformation create meaningful skill and interesting decisions? |

Each experiment must answer its own question before the next one starts. A generalised
material system is explicitly **not** a goal until the underlying mechanic has proven itself.

## Repository layout

```
Assets/            Unity assets. Scenes live in Assets/Scenes/.
Packages/          Package manifest and lock file.
ProjectSettings/   Unity project configuration (physics, time, input, rendering).
docs/experiments/  Experiment design documents and results.
```

## Working practice

- Experiments are designed in writing *before* implementation. See
  [`docs/experiments/`](docs/experiments/) for the format.
- Each experiment records its observed result and conclusion, including failures. A negative
  result that kills a bad idea early is a successful experiment.
- Small, logically-scoped commits. Housekeeping, design documents, and gameplay changes are
  committed separately so each can be reviewed on its own.
- Generated Unity output (`Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, IDE project
  files) is git-ignored and must never be committed.

## Git LFS

This repository uses Git LFS, configured via `.gitattributes` for binary asset types
(textures, models, audio, video). Clone with `git-lfs` installed, or binary assets will arrive
as pointer files rather than real content.
