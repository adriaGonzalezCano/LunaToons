# APOLLO - V1: Orbital Spacecraft Simulator

Unity 6 LTS + URP prototype. Scope: spacecraft in Earth orbit (first-person interior) and a Mission Control view, both driven by one shared orbital simulation.

## Status
- Steps 1-3 done and confirmed running in Unity.
- Step 4 (Earth, orbit visualization): written, not yet run in Unity. Adds a window onto Earth in the interior and a Mission Control orbit view (switch with Tab).
- Next: Step 5 (Mission Control telemetry interface), then Step 6 (integration checks).

## Try it in Unity
1. Open the project and check the Console for compile errors.
2. Menu: Apollo > Build V1 Scene (regenerates and overwrites Assets/Scenes/V1.unity).
3. Play: WASD walk, mouse look, Esc free/lock cursor, Tab spacecraft/Mission Control, 1-4 time x1/10/100/1000, P pause.
4. Walk to the left wall window (it is on the left, around the middle) and look out: Earth should move past at about 2.4 deg/s at x1. Mission Control shows Earth, the yellow orbit and a red spacecraft marker moving along it.

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
