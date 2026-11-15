# V2 plan: controllable spacecraft

Goal (owner brief, 2026-11-15): the player pilots the spacecraft from a control station inside the cabin, changing attitude and orbit through simulation-owned state. Apollo-style HUD is the secondary objective. Gameplay before accuracy. Single-player; architecture must allow co-op later.

## Decisions (owner, 2026-11-15)
| # | Topic | Decision |
|---|---|---|
| 1 | Orbit model | **Approach B**: coast analytically from the state vector (`CoastOrbit`, universal variables: no element singularities); integrate numerically (RK4, fixed step) during burns. A velocity change or cutoff just starts a new CoastOrbit from the new state. (Corrected 2026-11-15: an early M0 draft implemented approach A by mistake and was replaced) |
| 2 | Attitude | Realistic rigid body: angular momentum conserved, torque from controls, rotation persists after release. A "stop rotation" button applies counter-torque until rates are zero and the vehicle is static |
| 3 | Engine | Thrust along body +X (pushes occupants into the couches); propellant as a delta-v budget |
| 4 | Interaction | Start basic: crosshair press/hold on 3D controls. Seat mode undecided |
| 5 | Low periapsis | Simulation restarts. Working assumption: restart when actual altitude falls below 100 km; a warning shows when the predicted periapsis is below 100 km |
| 6 | Time warp | Auto-drop to x1 while engine or attitude controls are active |

## Milestones
- **M0 Simulation foundation** (this commit): `SpacecraftSimulation` (pure C#) owns clock and orbit; `CoastOrbit` (state-vector coasting, replaces element recomputation); recorded ground-track history; views follow orbit changes; restart logic; temporary debug velocity changes. Acceptance: V1 checks still pass, round-trip and burn checks pass (Apollo > Run Simulation Checks), debug burns visibly change telemetry, orbit ring and ground track.
- **M1 Attitude**: quaternion attitude and body rates in the state, Euler rigid-body dynamics, command interface (pitch/yaw/roll torque, stop-rotation), `ExternalView` driven by simulated attitude, temporary keyboard input, body axes on the Control Center 1 marker. Acceptance: angular momentum conserved when no torque; no drift when idle; same result at 30 and 144 fps.
- **M2 Propulsion**: finite-burn integration with throttle, engine arm, delta-v budget; start a new CoastOrbit on cutoff; warp policy; telemetry and views live. Acceptance: +100 m/s prograde gives apoapsis about 532 km, periapsis unchanged; propellant used equals delta-v; views stay in sync mid-burn.
- **M3 Control station**: crosshair interaction framework, station geometry (attitude controls, throttle, arm, stop-rotation), indicator lights.
- **M4 Instruments**: world-space gauges: attitude indicator, rates, propellant/thrust, digital readouts matching the telemetry panel.
- **M5 Integration**: scripted command playback is deterministic, extended checks, `VALIDATION_V2.md`, tag v0.2.

## Main risks
1. Views that assumed a fixed orbit (addressed in M0).
2. Handedness when mapping inertial rotations to Unity (build from mapped axis vectors, unit-test).
3. Finite-burn accuracy and determinism (fixed step, warp lockout).
4. Interaction ergonomics with mouse look; player disorientation without prograde cues.
5. Nothing is tested until run in Unity; maths is cross-checked in Python (`tools/`).
