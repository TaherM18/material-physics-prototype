# Experiment 001 — Rigid vs rubber physical behaviour

**Status:** Implemented, awaiting a run in the Unity Editor
**Scope:** Physics only. No player control, no transformation timing, no gameplay design.

This experiment deliberately tests *the physics*, not *the mechanic*. Nothing here involves
player input. If rubber-like physical behaviour is not interesting when a body is simply
dropped, thrown, and slid, then it will not become interesting merely because a player pressed
a button to cause it — so that question is asked first, and cheaply.

---

## 1. Question

Does changing a Rigidbody's physical properties from rigid-like to rubber-like, using **only
Unity's native physics** (Physics Materials + Rigidbody settings), produce meaningfully
different and interesting physical behaviour?

"Meaningfully different" is deliberately stronger than "different". A rubber body that simply
travels a bit further, or wobbles slightly more, is *different* but not *meaningful*. The
target is a **qualitative change in trajectory**: outcomes a player could not have produced in
the other state at all.

## 2. Hypothesis

**H1 — Restitution produces a qualitative change.** A body with high bounciness will convert
downward momentum into repeated vertical travel, and will convert a wall impact into a
redirected trajectory rather than a stop. A rigid body dissipates that momentum on first
contact. This is a change in *trajectory shape*, not in *magnitude*, and is therefore
meaningful.

**H2 — Friction produces a second, independent axis of change.** A low-friction rigid state
slides and preserves horizontal momentum; a high-friction rubber state grips, converts sliding
into rolling, and loses horizontal momentum on contact. This matters because it means the two
states are not simply "more bouncy / less bouncy" — they trade different things away, which is
the precondition for interesting decisions later.

**H3 — Unity's native restitution will be visibly incomplete on its own.** Specifically,
`Physics.bounceThreshold` (currently `2`) will cause bouncing to terminate *abruptly* rather
than decaying naturally, and PhysX restitution is applied only to the normal component of
relative velocity, so glancing impacts will feel less "rubbery" than head-on ones. The
prediction is that native physics gets us most of the way but leaves an artefact we can
identify precisely.

H3 is the one I most expect to be right, and it is the most useful to be right about, because
it tells us exactly what custom logic (if any) would need to do in a later experiment.

## 3. Test setup

The smallest scene that can answer the question. Everything is passive — released and observed.

**Scene:** `Assets/Scenes/Experiment001.unity` (new; `SampleScene` stays untouched).

**Objects:**

| Object | Components | Purpose |
|---|---|---|
| `Ground` | Box collider, scaled flat, static (no Rigidbody) | Landing surface |
| `Wall` | Box collider, vertical, static | Redirection surface |
| `Ramp` | Box collider, rotated ~25 degrees, static | Friction / slide surface |
| `Subject` | Sphere collider + Rigidbody + MeshRenderer | The body under test |
| `HeightMarkers` | Empty parent with markers at 0.25 / 0.5 / 1 / 2 / 4 m | Reading apex heights by eye |

**Three micro-tests, run once per state:**

- **A — Drop test.** `Subject` released from rest at 5 m above `Ground`. Isolates restitution
  with zero horizontal component. This is the cleanest possible measurement.
- **B — Wall test.** `Subject` given a fixed initial horizontal velocity toward `Wall` (set
  once in the inspector or on `Start`, not by input). Isolates redirection.
- **C — Slope test.** `Subject` released from rest on `Ramp`. Isolates friction, and
  distinguishes sliding from rolling.

**Materials:** two Physics Material assets in `Assets/Experiments/001_RigidVsRubber/Physics/`:

| | Rigid | Rubber |
|---|---|---|
| Dynamic friction | 0.2 | 0.9 |
| Static friction | 0.25 | 1.0 |
| Bounciness | 0.05 | 0.85 |
| Friction combine | Average | Average |
| Bounce combine | Average | Average |

A third asset, `Neutral` (friction 0.4 / 0.4, bounciness 0, both combines `Average`), is
assigned to `Ground`, `Wall` and `Ramp`. **This is not optional.** See section 4.

Switching state in this experiment means **swapping `Collider.sharedMaterial` in the inspector
between runs**. No runtime switching code is written yet — that is Experiment 002.

### Amendment at implementation time (2026-09-08)

Recorded rather than silently edited, because the design above was written before the sandbox
existed. Nothing in sections 6, 7 or 8 was changed.

1. **State switching is now a runtime toggle (Tab), not an inspector edit.** Swapping the
   material by hand between runs is slower and easier to get wrong than a keypress, and the
   active state is printed on the HUD and in every summary so a run cannot be misattributed.
   This does *not* pull Experiment 002 forward: the question here is still whether the two
   states behave differently, not whether a player can exploit the switch.
2. **Manual WASD control was added.** Measurements still come only from the three scripted
   scenarios, which reset position, rotation and both velocities so runs are comparable.
   Manual control exists to poke at the sandbox between runs and find behaviour worth measuring
   properly. Keep hands off the keyboard during a recorded run.
3. **The scene is generated by an editor script** (`Tools > Experiment 001 > Build Sandbox
   Scene`) rather than hand-authored, so a confusing result can be answered by rebuilding to a
   known state instead of hunting for what drifted.
4. **`collisionDetectionMode` is left at `Discrete`** (the PhysX default) and the ground is
   built 2 units thick instead. Pre-emptively switching to a continuous mode would hide
   tunnelling rather than reveal it; every summary records which mode was active.
5. **A runtime `Physics.bounceThreshold` override exists but is off by default.** It is for the
   deliberate sweep in section 5, never to make a run "work". It writes only to the runtime
   global, never to the project asset, and restores the original value on disable.

## 4. Variables

**Independent (what changes):** the Physics Material assigned to `Subject` — Rigid or Rubber.
Exactly one thing changes between paired runs.

**Dependent (what is measured):** see section 5.

**Controlled (held fixed, and *why* each one matters):**

| Held fixed | Value | Why it would otherwise confound the result |
|---|---|---|
| Environment material | `Neutral` on all static geometry | PhysX resolves a contact from **both** colliders' materials. Leaving the ground material empty means it silently uses PhysX defaults, so a "rubber vs rigid" comparison would actually be measuring an uncontrolled pair of materials. |
| Combine modes | `Average` everywhere | When two materials disagree on combine mode, PhysX does **not** average the modes — it applies the one with the higher priority (`Average` < `Minimum` < `Multiply` < `Maximum`). A single `Maximum` anywhere in the scene overrides everything else and makes results non-reproducible. Holding all modes at `Average` removes this entire class of confusion from run 1. |
| Mass | 1 kg | Restitution against *static* geometry is mass-independent, so mass should change nothing in tests A and C. Holding it fixed lets a surprise here reveal a misconfiguration. |
| Drop height | 5 m | Determines impact speed (v = sqrt(2gh), about 9.9 m/s) and therefore how many bounces occur before the bounce threshold cuts in. |
| Collider shape | Sphere | Avoids edge/corner contact artefacts. A box tumbling on its corners would confound restitution with angular response. |
| Fixed Timestep | 0.02 (50 Hz) | Already the project value. Changing it changes solver behaviour and invalidates comparison across runs. |
| Interpolation | `Interpolate` | See section 9 — this is a *visual* control, not a physical one, and getting it wrong invalidates any judgement about feel. |
| Solver iterations, friction type, solver type | Project defaults (6, 0, 0) | Project-wide PhysX tuning. Not variables in this experiment. |
| Bounce threshold | 2 (project default) **for the first pass** | Then deliberately varied as a follow-up — see section 5. |

## 5. Measurements / observations

**Quantitative (test A):**

1. **Apex height of each successive bounce**, until bouncing stops. Read against
   `HeightMarkers`, and logged numerically.
2. **Number of bounces** before the body comes to rest.
3. **Whether bouncing stops abruptly or decays smoothly.** This is the direct test of H3.
4. **Time to rest**, in seconds.

The clean way to capture 1–4 is a small logging component on `Subject` that watches for
`linearVelocity.y` changing sign from positive to negative (an apex) and logs the height, plus
`OnCollisionEnter` for impact speed. **I would like you to write this one yourself** — it is
about fifteen lines, it forces the `Update` vs `FixedUpdate` distinction to become concrete,
and it is exactly the kind of small component you should not be outsourcing. I will review it.

**Quantitative (test B):** exit angle vs entry angle at the wall, and exit speed as a fraction
of entry speed.

**Quantitative (test C):** time to travel a fixed distance down the ramp, and whether the
sphere slides, rolls, or does both.

**Qualitative (all tests), recorded in writing:** does the rubber state produce trajectories
the rigid state cannot produce at all? Or only the same trajectories, scaled?

**Follow-up sweep, after the paired runs:** repeat test A with `Physics.bounceThreshold`
lowered (try `0.5`, then `0.1`) and with bounciness at `0.5`, `0.85`, `1.0`. This is what turns
the experiment from a yes/no into an understanding of the parameter space.

## 6. Expected outcomes

Concrete enough to be wrong:

- **Test A, rigid:** one visible impact, negligible rebound, at rest in well under 2 s.
- **Test A, rubber:** apex heights should fall by a factor of e-squared per bounce (a bounce
  off static geometry returns `e` times the impact speed, and height goes with the square of
  speed). At e = 0.85 that is about 0.72 per bounce: **5 m → 3.6 → 2.6 → 1.9 → 1.35 → 0.98 →
  0.70 → 0.51 → 0.37 → 0.26 → 0.19 m**.
- **The bounce threshold prediction (H3):** an impact speed below 2 m/s has its restitution
  discarded entirely. 2 m/s corresponds to an apex of v-squared / 2g, about **0.20 m**. So
  bouncing should **stop dead at an apex of roughly 0.2 m, after about 10 bounces**, rather
  than continuing into an ever-faster buzz. If the body instead settles smoothly, or keeps
  micro-bouncing, H3 is wrong and I want to know immediately.
- **Test B, rigid:** hits the wall and drops more or less where it hit.
- **Test B, rubber:** reflects at roughly the mirror angle, retaining most horizontal speed —
  visibly a *redirection* rather than a *stop*.
- **Test C, rigid:** slides, arriving at the bottom faster, with little rotation.
- **Test C, rubber:** grips and rolls almost immediately, arriving slower.

If test C shows no difference, suspect that the sphere is rolling in both cases and that
friction is not the dominant term — that would itself be a useful finding about what Physics
Materials can and cannot express.

## 7. What would make us proceed to Experiment 002

Proceed if **either** of these holds:

1. **Trajectory shape differs qualitatively.** The rubber state reaches positions, heights, or
   directions the rigid state cannot reach at all. Not "further" — *elsewhere*.
2. **The two states trade different things away.** Rubber gains vertical/redirected travel but
   loses horizontal momentum to friction; rigid keeps horizontal momentum but dissipates
   impacts. Two states that are each better at something different are the raw material for a
   decision; two states on a single "more/less" axis are not.

Watching the drop test should provoke a specific thought: *"if I could choose when to be in
that state, I could get somewhere."* If it does not, criterion 1 has not really been met,
whatever the numbers say.

## 8. What would make us abandon or redesign

**Redesign the approach (keep the mechanic)** if:

- Native restitution is too lossy or too threshold-clamped to sustain interesting motion. The
  response is *not* to abandon the mechanic — it is to open **Experiment 001b**, testing a
  custom impulse applied in `OnCollisionEnter` from the collision impulse, and compare it
  against this baseline. This experiment exists partly to earn the right to write that code.
- The difference is real but only appears at parameter values that behave badly elsewhere
  (jitter, tunnelling, never sleeping). That means Physics Materials are the wrong tool, not
  that the idea is wrong.

**Abandon or substantially rethink the mechanic** if:

- Both states produce the same trajectory *shape*, differing only in magnitude. Two points on
  one axis do not make a mechanic — that is a stat, not an ability.
- The rubber state is uncontrollable in a way that reads as broken rather than as challenging.
  Chaotic is fine; unreadable is not, because a player cannot form intent about it.
- The behaviour is not legible: an observer cannot tell *why* the body went where it went. A
  mechanic the player cannot build a mental model of cannot support the "wait, I can use it
  like that?" moment that motivates the whole direction.

Recording a negative result here and stopping is a **successful** experiment. It costs one
session and saves the alternative.

## 9. Technical questions to answer in Unity

**Unity cannot be run from the development assistant's environment, so every item below needs
manual verification in the Editor.** The first four are genuinely unknown; the rest are things
believed to be true that should be confirmed rather than assumed.

### Must be checked before designing anything further

1. **Which physics backend is active?** `ProjectSettings/DynamicsManager.asset` contains
   `m_CurrentBackendId: 4072204805`. That is an opaque hash and it cannot be resolved to a name
   from the file alone. The circumstantial evidence points to built-in PhysX: the only physics
   module in `Packages/manifest.json` is `com.unity.modules.physics` (no `com.unity.physics`,
   no Havok package), and the neighbouring keys in the same file — `m_SolverType`,
   `m_FrictionType`, `m_ImprovedPatchFriction`, `m_BroadphaseType`, `m_WorldSubdivisions` —
   are PhysX-specific. **Check Project Settings → Physics and report which backend is selected,
   and what the alternatives in the dropdown are.** Do not change it. It matters because
   restitution handling and the bounce threshold are backend behaviour, and every number in
   section 6 assumes PhysX.

2. **Is the Physics Material asset menu "Physics Material" or "Physic Material"?** Unity 6
   renamed the type from `PhysicMaterial` to `PhysicsMaterial`. Confirm what the create menu
   and the file extension actually say in 6.6, since the naming in section 3 depends on it.

3. **Confirm the Rigidbody property names in 6.6.** Unity 6 renamed `Rigidbody.velocity` to
   `linearVelocity`, `drag` to `linearDamping`, and `angularDrag` to `angularDamping`. Any
   tutorial written before Unity 6 will use the old names. Confirm which compile, because the
   logger in section 5 needs `linearVelocity`.

4. **Does the project open cleanly at all?** There is no `Library/` folder in this working
   copy, so the project has never been opened on this machine. The first launch will do a full
   import and package resolve. Report any console errors before trusting a single measurement.

### Check while setting the experiment up

5. **Is `Rigidbody.interpolation` set to `Interpolate`?** It defaults to `None`. Physics runs
   at 50 Hz; if rendering happens at 144 Hz with interpolation off, what is on screen is a
   stepped approximation of the motion. Judging "does this feel good" through that is judging
   the wrong thing. Set it before test A, not after.

6. **Does the sphere tunnel through the ground?** `collisionDetectionMode` defaults to
   `Discrete`. At 9.9 m/s impact speed and a 0.02 s step, the body moves about 20 cm per step.
   If the ground is thin, or if the follow-up sweep pushes speeds higher, expect tunnelling.
   Switching to `ContinuousDynamic` is the fix, but note it in the log if needed — it is a
   change to a controlled variable.

7. **Does the body fall asleep mid-experiment?** `m_SleepThreshold` is `0.005`. A decaying
   bounce will eventually put the Rigidbody to sleep. That is correct behaviour, but confirm it
   is *sleep* and not the bounce threshold, or the wrong cause will be recorded.

8. **What is `Ground`'s material actually set to?** `m_DefaultMaterial` in the project physics
   settings is `{fileID: 0}` — there is no project-wide default. A collider with no material
   assigned gets PhysX built-in defaults, which are not documented anywhere in the project
   files. This is the single most likely source of a confusing result in test A.

9. **Does bounciness above 1.0 work, and does it add energy?** Worth one quick run in the
   sweep. If PhysX clamps it, that is a hard ceiling on what native physics can give us, and it
   directly informs whether Experiment 001b is needed.

---

## 10. Result

*To be filled in after running. Record observed behaviour first, then the conclusion — in that
order, and do not edit sections 1–9 after starting.*

**Observed:**

**Conclusion:**

**Decision:** proceed to 002 / open 001b / redesign / abandon
