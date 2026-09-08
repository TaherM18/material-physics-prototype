# Experiments

Each experiment answers **one** question about whether a mechanic is worth building.

An experiment is designed in writing before any code is written. The purpose of writing it down
first is to fix the success criteria *before* seeing the result, so that a disappointing outcome
cannot be quietly reinterpreted as a success.

## Rules

1. **One question per experiment.** If it needs two questions, it is two experiments.
2. **The observable is defined up front.** "Does it feel good?" is not an observable. "Can the
   player reach platform B, which is unreachable in the other state?" is.
3. **Abandon criteria are written before implementation.** An experiment that cannot fail has
   not tested anything.
4. **The result is recorded even when it is negative.** Killing a bad idea in one Sunday is the
   cheapest possible outcome.
5. **Compiling is not a result.** The question is always about behaviour, never about the build.

## Document structure

| Section | Purpose |
|---|---|
| Question | The single thing being tested. |
| Hypothesis | What is expected to happen, and why. |
| Test setup | The minimum scene and objects required. |
| Variables | Independent (what is changed), dependent (what is measured), controlled (what is held fixed). |
| Measurements / observations | How the result is captured. Filled in during the session. |
| Expected outcomes | Concrete predictions, ideally with rough numbers. |
| Proceed criteria | What result justifies the next experiment. |
| Abandon / redesign criteria | What result kills or reshapes the idea. |
| Technical questions | Things that must be verified in the Unity Editor. |
| Result | Filled in after running. Observed behaviour, then conclusion. |

## Naming

`NNN-short-kebab-case-title.md`, numbered in the order the experiments were designed.

## Status

| # | Title | Status |
|---|---|---|
| [001](001-rigid-vs-rubber-physical-behaviour.md) | Rigid vs rubber physical behaviour | Implemented, awaiting run |
