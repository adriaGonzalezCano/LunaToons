# APOLLO - V1: Orbital Spacecraft Simulator

Unity 6 LTS + URP prototype. Scope: spacecraft in Earth orbit (first-person interior) and a Mission Control view, both driven by one shared orbital simulation.

## Status
- **V1 complete (tag v0.1), validated by the owner in Unity.** Contents: orbital simulation core, walkable Apollo-inspired spacecraft interior with Earth window, Control Center 1 (3D orbit view, rotatable, telemetry), Control Center 2 (flat map with ground track), Earth rotation, real Earth map (NASA Blue Marble), simulation checks and validation checklist.
- Not done by design (V1 out of scope): multiplayer, launch, Moon, docking, manoeuvres, subsystems, failures, objectives.
- Known simplifications: spherical Earth, two-body orbit with no perturbations, fixed nadir-pointing attitude, artificial gravity in the cabin, Earth rotation angle 0 at MET 0.

## V2 (in progress, see docs/V2_PLAN.md)
- M0 (simulation foundation) written, not yet run in Unity. Debug keys: ] / [ = +/-10 m/s along the velocity, R = restart.

## Try it in Unity
1. Open the project and check the Console for compile errors.
2. Menu: Apollo > Run Simulation Checks (expect all PASS in the Console).
3. Menu: Apollo > Build V1 Scene (regenerates and overwrites Assets/Scenes/V1.unity).
4. Play: WASD walk, mouse look, Esc free/lock cursor, Tab cycles Spacecraft / Control Center 1 (3D orbit, drag to rotate, wheel to zoom) / Control Center 2 (flat map with ground track), F1-F3 jump directly, 1-4 time x1/10/100/1000, P pause.

## Folder layout
```
Assets/Scripts/
  OrbitalSim/       Pure C# orbital model (no UnityEngine dependency)
  Shared/           SimulationClock, shared simulation state
  Spacecraft/       First-person controller, spacecraft visuals
  MissionControl/   Earth/orbit visualization, telemetry UI
  Core/             Environment switching
Assets/{Scenes,Prefabs,Materials,Settings,Input}
```

## Conventions (working assumptions, not yet confirmed)
- SI units internally, `double` precision in the simulation.
- Earth-centered inertial frame, Z toward north, right-handed. Converted to Unity space only at the view layer.
- Simulation state is the single source of truth; no view reads orbital data from a Transform.

## Creating the Unity project
1. Install Unity Hub, then Unity 6 LTS (with Windows/Mac/Linux build support as needed).
2. In Unity Hub: New project > "Universal 3D" (URP) template, Unity 6 LTS.
3. Create it in a temporary folder, then copy its `Assets`, `Packages` and `ProjectSettings` folders into this repo, merging with the existing `Assets/` folders. Alternatively create it directly here if the folder is empty of Unity files.
4. Open the project once so Unity generates `.meta` files, then commit.
