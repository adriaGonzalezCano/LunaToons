# APOLLO - V1: Orbital Spacecraft Simulator

Unity 6 LTS + URP prototype. Scope: spacecraft in Earth orbit (first-person interior) and a Mission Control view, both driven by one shared orbital simulation.

## Status
- Step 1 (setup): repo skeleton only. The Unity project itself must be created in Unity Hub (see below).
- Nothing else implemented or tested yet.

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
