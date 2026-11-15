# V1 validation checklist (Step 6)

Status legend: AUTO = run by the code, MANUAL = you check in Unity. Nothing here counts as passed until it has been run in Unity.

## A. Automatic: Unity menu "Apollo > Run Simulation Checks"
Runs the real C# orbital code and logs PASS/FAIL per check in the Console:
- Period matches Kepler's third law; circular apsis altitudes equal the orbit altitude.
- Circular orbit: altitude and speed constant over 3 orbits; latitude peaks at the inclination.
- Elliptical orbit: energy and angular momentum conserved; returns to start after one period; vis-viva holds.
- Determinism: a given mission time always gives the identical state.
- Earth rotation: start point lat 0 / lon 0; ground track shifts west about 22.07 deg per orbit; one turn per sidereal day.
- Coasting from a state vector (V2 M0): matches the element-based propagation on 8 orbit types over short, long and negative time spans, conserves energy and angular momentum, +100 m/s velocity change matches vis-viva, escape refused, restart below 100 km (also at x1000 warp), no false restarts, ground-track history recording and pruning.
- Powered flight (V2): 20 s burn gives delta-v 60 m/s, apoapsis matches the impulsive prediction (390.75 km), result identical at 30/60/144 fps, release gives a continuous coast, retrograde burn gives periapsis 85 km, escape cutoff, restart clears engine state; coasting solver converges with conserved energy/angular momentum up to e = 0.995.
- Clock: time scale, pause and resume behave.
- ViewMapping: Unity (x,y,z) = ECI (x,z,y) and length is preserved.
Expected result: "Apollo simulation checks: N passed, 0 failed." (N is the number of checks; there must be no FAIL line).

## B. Manual: in Play mode (scene from "Apollo > Build V1 Scene")
1. Stable orbit: in Mission Control, Altitude stays at about 185.000 km and Speed at about 7797 m/s for several minutes (use key 4 for x1000).
2. Position vs time: the red marker moves along the yellow orbit line; one full lap takes about 88.05 min of MET (at x1000, about 5.3 s).
3. Telemetry vs orbit: Period reads 88.05 min, Inclination 32.50 deg, Eccentricity 0.00000, Periapsis and Apoapsis both 185.0 km, and Latitude swings between about -32.5 and +32.5 deg.
4. No interruption on switching: note MET in Mission Control, press Tab, wait 10 s, press Tab again; MET must have advanced by about 10 s (not reset, not frozen).
5. Interior: you can walk and look around, and the window shows Earth moving past.
6. Consistency between views: with Earth in the window, the marker in Mission Control is in motion at the same time (both read the same state; there is no second simulation in the project).
7. Pause: P stops MET, the marker and the Earth motion together; P again resumes.

8. Orbit camera (Control Center 1): hold left or right mouse button and drag to rotate around Earth; mouse wheel zooms.
9. Earth rotation (Control Center 1): Earth turns slowly under the fixed yellow orbit ring (one turn per 23.93 h of MET, about 86 s at x1000) while the marker keeps tracing the ring.
10. Ground track (Control Center 2, F3 or Tab twice): flat map with cyan past track, yellow predicted track and a red spacecraft marker; each successive orbit is displaced west by about 22 deg; no horizontal line crosses the map where the track wraps at the +-180 edge; the bottom strip shows lat/lon matching the marker.
11. Window view: ground below moves and its direction is consistent with the Earth rotation (the Earth turns eastward under the spacecraft path).
12. Interior: three couches face the dark instrument panel, indicator lights glow, the round hatch is at the back, and you can walk along both side aisles to the panel without getting stuck.

13. Burn (V2): rebuild the scene (Apollo > Build V1 Scene), press Play, hold T: Engine ON, Delta-v used grows at 3 m/s per second, apoapsis rises live and the yellow orbit ring and cyan/yellow ground tracks update each frame; release T: Engine OFF and orbit elements freeze. Shift+T lowers the periapsis; below 100 km the simulation restarts. Holding T for ~18 min cuts the engine (escape).

## Structural guarantees (by design, not by test)
- One `SimulationHost` on an always-active object. Tab only toggles the two rigs; it never touches the host.
- No orbital value is read from a Transform. Window view, orbit view and telemetry all read `SimulationHost.CurrentState`.
