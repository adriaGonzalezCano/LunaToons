# V2 plan: controllable spacecraft

Goal (owner brief, 2026-11-15): the player pilots the spacecraft from a control station inside the cabin, changing attitude and orbit through simulation-owned state. Apollo-style HUD is the secondary objective. Gameplay before accuracy. Single-player; architecture must allow co-op later.

## Decisions (owner, 2026-11-15)
| # | Topic | Decision |
|---|---|---|
| 1 | Orbit model | **Approach B**: coast analytically from the state vector (`CoastOrbit`, universal variables: no element singularities); during a burn integrate numerically with fixed-step **RK8** (DOP853 tableau, 0.05 s step) and solve the osculating Kepler orbit after every step for display. Burn = continuous finite thrust while **T** is held (thrust 1 or 0); releasing T starts a coast arc from the last state. (Corrected twice on 2026-11-15: an early draft was approach A, then RK4 impulsive-style burns; the owner asked for continuous RK8 burns) |
| 2 | Attitude | Realistic rigid body: angular momentum conserved, torque from controls, rotation persists after release. A "stop rotation" button applies counter-torque until rates are zero and the vehicle is static |
| 3 | Engine | Thrust along body +X (pushes occupants into the couches); propellant as a delta-v budget |
| 4 | Interaction | Start basic: crosshair press/hold on 3D controls. Seat mode undecided |
| 5 | Low periapsis | Simulation restarts. Working assumption: restart when actual altitude falls below 100 km; a warning shows when the predicted periapsis is below 100 km |
| 6 | Time warp | Auto-drop to x1 while engine or attitude controls are active |

## Milestones
- **M0 Simulation foundation** (this commit): `SpacecraftSimulation` (pure C#) owns clock and orbit; `CoastOrbit` (state-vector coasting with a bracketed Kepler solver); `Rk8Integrator`; continuous T-key burns (prograde, Shift+T retrograde as a placeholder until attitude exists; default 3.0 m/s^2; automatic cutoff before the orbit becomes unbound); recorded ground-track history; views follow orbit changes; restart logic. Acceptance: all checks pass (Apollo > Run Simulation Checks); holding T visibly changes telemetry, orbit ring and ground track live; releasing T leaves a stable coast.
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

## Attitude and controls plan (owner, 2026-11-15)
Decisions: realistic attitude (angular momentum conserved), X = stop rotation, equal inertia for now (diagonal inertia supported), I switches translation/attitude mode, WASD in both modes (attitude adds Q/E roll; translation uses Q down, E up), ship controls only after pressing a command-panel button (walking disabled), E enters control mode, Shift exits (interpretation to confirm: E enters when looking at the panel button; Shift leaves).
Owner update (same day): T is dropped. Translation mode: W/S = thrust forward/back (the old T and Shift+T), A/D = lateral, Q down, E up. Attitude mode: W/S pitch, A/D yaw, Q/E roll. I toggles mode. E (pressed at the cabin button) enters control mode, Shift exits. T and Shift+T stay as placeholders until step 5. Initial attitude: +X along velocity, +Z radial, inertial hold (zero rate).
Steps: 1 attitude core (RigidBodyAttitude, Python-validated) | 2 command interface + sim integration + telemetry | 3 see it (ExternalView from attitude, starfield, CC1 body axes, temporary keys) | 4 thrust along body +X | 5 control modes and keys | 6 walk lock / control ownership | 7 command panel button | 8 translation modes, docs, tag.

## Body axes (decision recorded with step 3, ASSUMPTION)
The simulated body frame matches the cabin: +X forward (couches and panel direction, main engine), +Y left, +Z up (right-handed). Cabin Unity axes: +X = body X, +Z = body Y, +Y = body Z (ViewMapping.CabinToWorld). Start attitude: +X along the velocity, +Y away from Earth, so the window (right-hand wall, cabin -Z) looks down at Earth. Command signs: pitch +1 = nose up (about -Y), yaw +1 = nose right (about -Z), roll +1 = roll right (about +X). Attitude starts as inertial hold: the Earth drifts past the window unless the ship rotates to follow it (a "hold nadir" mode is a possible later addition).
