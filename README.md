# Ragdoll Tech

An original Unity 6 package for animation-driven impact reactions, constrained knockdowns and checked recovery under arbitrary gravity. Developed alongside [Astromancer](https://github.com/OrionFranklin/astromancer).

This first version gives a humanoid 13 physics bodies connected by limited, driven joints. Small impacts keep the pelvis attached to locomotion while the limbs react. Strong impacts release the body, weaken the muscles and use a simple arm-bracing reflex near the floor. Once the body settles, the game can choose a clear standing space and blend back to animation.

## Install

Tested with **Unity 6000.6.2f1**, built-in rendering and PhysX on Windows. In Package Manager, choose **Install package from Git URL**:

```
https://github.com/OrionFranklin/Ragdoll-Tech.git#feature/active-ragdoll
```

Pin a reviewed commit instead of the branch for reproducible builds. Alternatively, clone this repository and install its `package.json` from disk. The package includes no Astromancer source, purchased models or animation packs.

## Connect a character

Initialize once from a neutral, upright pose before locomotion evaluates. The visible skeleton remains under the Animator; separate, unit-scale rigidbodies supply the physical pose. Disable root motion. Use normal Animator update mode and keep it evaluating during reactions (`AlwaysAnimate` when needed).

```csharp
using RagdollTech;
using UnityEngine;

// model is the character's upright, forward-facing root.
var puppet = gameObject.AddComponent<ActiveRagdoll>();
puppet.Initialize(model, HumanoidRig.Describe(animator), new RagdollSettings());
puppet.Gravity = point => Physics.gravity; // Replace with planet-local gravity.
puppet.ApplyImpact(hitPoint, hitDirection * 30f, movementVelocity);
```

`HumanoidRig.Describe` supports valid Humanoid avatars, with Chest/Neck fallbacks. Custom rigs can supply parent-before-child `BoneDefinition[]`. `localEnd` is a **metre** offset in the bone's rotational frame, not its scaled local position. Shape sizes, bend directions and limits are starting values: inspect and tune them per rig, especially arms authored in different neutral poses. `lowBend` / `highBend` control signed X limits; Y/Z limits are symmetric. Root right/up establish joint axes.

Impulses are **Newton-seconds**, inherited velocity is **metres/second**, and gravity is **metres/second squared**. The default 72 kg body enters a full fall at an impulse/mass ratio of 0.9, or when `forceKnockdown` is true. Impulses and velocities are bounded. `ApplyImpact` is the entry point for gameplay hits; idle proxy colliders are disabled, so ordinary contacts with an animated character do not automatically generate hits.

## Motor and recovery ownership

| State | Ownership |
| --- | --- |
| Animated | Animation and host motor; physics colliders disabled |
| Reacting | Pelvis follows animation; driven limbs respond to the impulse |
| Falling | Dynamic pelvis and limbs; stop motor and foot IK |
| Settled | Supported, quiet body; `RecoveryRequested` asks the host to find space |
| Recovering | Physics pauses; pose blends into animation; motor remains paused |

Listen to `StateChanged` to enable your motor only in `Animated`/`Reacting`. During `ControlsRoot`, follow `PelvisPosition` with the camera and gameplay root. Keep animation running as a target, but suspend foot IK and other post-animation bone writers. The package restores animation in `Update` and writes physics poses in `LateUpdate` at execution order 10000.

```csharp
puppet.RecoveryRequested += () =>
{
    if (!puppet.TryFindRecoveryPose(character.forward, out var pose)) return;
    puppet.BeginRecovery(); // Capture the physical pose first.
    character.SetPositionAndRotation(pose.position, pose.rotation);
};
```

The query checks support slope, nearby alternatives and capsule clearance; it rejects a floor above the lying body. Pass your real standing height/radius for nonstandard characters. If blocked, leave the character down and retry. The package never teleports to a checkpoint by itself. Call `Cancel()` before teleporting, changing worlds or handing control to swimming. Handle `SimulationFault` with a game-specific recovery policy.

Default body layer: **2 (Ignore Raycast)**. Rigidbodies still collide, while default ground/motor casts exclude them. For a custom layer, exclude it from `groundMask`, camera casts and motor queries. Disable collisions with any host controller collider through your collision matrix. Self-collisions within one puppet are disabled; scenery still collides with the body.

## Playable lab

In a **disposable Unity project**, select **Ragdoll Tech → Create playable lab**, then press Play. This creates `Assets/RagdollLab/Lab.unity` and configures the project for a Windows lab build with legacy input. Save other work first. The mannequin and room use original primitive geometry.

| Input | Action |
| --- | --- |
| Left click | Small push toward the cursor ray |
| Shift + left click | Stronger push |
| K | Knock down |
| R | Reset |
| G | Rotate gravity and rebuild the room |

Build from that project:

```powershell
& $Unity -batchmode -quit -projectPath $Project `
  -executeMethod RagdollTech.Editor.LabBuilder.Build `
  -labOutput "$Project/Builds/RagdollTechLab.exe" -logFile "$Project/build.log"
```

Run the executable with `-ragdollSmoke -ragdollOutput <absolute-folder>` for six-direction impact/recovery acceptance. It writes a result and offscreen fall capture, then exits. `Builds/` is ignored by Git.

## Tests and design

Add `"com.orion.ragdoll-tech"` to the consuming project's `testables` array in `Packages/manifest.json`, install Unity Test Framework, and run Edit Mode tests:

```powershell
& $Unity -batchmode -nographics -projectPath $Project -runTests `
  -testPlatform EditMode -testResults "$Project/results.xml" -logFile "$Project/tests.log"
```

The 13 tests cover mass/idle collision, joint-space rotation, impact modes, invalid input/gravity, teardown ordering, blocked recovery and real physics collision/settling in all six axis directions. They restore the previous simulation mode afterward. The standalone lab also exercises frame-by-frame recovery.

Configurable joint drives, conservative depenetration, per-body solver iterations and limited angular motion follow Unity's [ragdoll stability guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/RagdollStability.html) and [joint target rotation reference](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ConfigurableJoint-targetRotation.html). The supplied VFX breakdown inspired the separation of constraints, muscle strength and reaction states. This is original code, not Rockstar/Euphoria technology.

## Current limits

This is an active reaction/knockdown foundation, not an autonomous balancing or stepping controller. Bracing is a bounded heuristic, not hand placement IK. Recovery blends to a checked standing pose; authored face-up/face-down get-up animations are future work. Clothing, hair, staffs and hands have no independent physics. The first game integration controls the local traveler; ragdoll bones are not network replicated. No deterministic cross-machine simulation is promised. Crowds, moving platforms and extreme nonuniform rigs need more profiling and tuning.

The repository does not currently include a license grant.
