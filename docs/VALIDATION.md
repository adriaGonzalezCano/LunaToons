# V1 validation checklist (Step 6)

Status legend: AUTO = run by the code, MANUAL = you check in Unity. Nothing here counts as passed until it has been run in Unity.

## A. Automatic: Unity menu "Apollo > Run Simulation Checks"
Runs the real C# orbital code and logs PASS/FAIL per check in the Console:
- Period matches Kepler's third law; circular apsis altitudes equal the orbit altitude.
- Circular orbit: altitude and speed constant over 3 orbits; latitude peaks at the inclination.
- Elliptical orbit: energy and angular momentum conserved; returns to start after one period; vis-viva holds.
- Determinism: a given mission time always gives the identical state.
- Clock: time scale, pause and resume behave.
- ViewMapping: Unity (x,y,z) = ECI (x,z,y) and length is preserved.
Expected result: "Apollo simulation checks: 15 passed, 0 failed."

## B. Manual: in Play mode (scene from "Apollo > Build V1 Scene")
1. Stable orbit: in Mission Control, Altitude stays at about 185.000 km and Speed at about 7797 m/s for several minutes (use key 4 for x1000).
2. Position vs time: the red marker moves along the yellow orbit line; one full lap takes about 88.05 min of MET (at x1000, about 5.3 s).
3. Telemetry vs orbit: Period reads 88.05 min, Inclination 32.50 deg, Eccentricity 0.00000, Periapsis and Apoapsis both 185.0 km, and Latitude swings between about -32.5 and +32.5 deg.
4. No interruption on switching: note MET in Mission Control, press Tab, wait 10 s, press Tab again; MET must have advanced by about 10 s (not reset, not frozen).
5. Interior: you can walk and look around, and the window shows Earth moving past.
6. Consistency between views: with Earth in the window, the marker in Mission Control is in motion at the same time (both read the same state; there is no second simulation in the project).
7. Pause: P stops MET, the marker and the Earth motion together; P again resumes.

## Structural guarantees (by design, not by test)
- One `SimulationHost` on an always-active object. Tab only toggles the two rigs; it never touches the host.
- No orbital value is read from a Transform. Window view, orbit view and telemetry all read `SimulationHost.CurrentState`.
