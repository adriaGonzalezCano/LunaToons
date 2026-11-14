# Decisions log

Statuses: CONFIRMED (approved by owner), ASSUMPTION (working, can change), OPEN.

| Topic | Status | Decision |
|---|---|---|
| Stack: Unity 6 LTS, C#, URP, Input System, Rider, Git | CONFIRMED | per V1 brief |
| V1 scope and exclusions | CONFIRMED | per V1 brief |
| Propagation | ASSUMPTION | Analytic Kepler (function of time) |
| Frame | ASSUMPTION | ECI, Z north, right-handed, SI, double |
| Scene layout | ASSUMPTION | One scene, two rigs toggled |
| Mission Control form | ASSUMPTION | 3D orbit view + telemetry HUD |
| UI | ASSUMPTION | uGUI with legacy Text (changed from TextMeshPro to avoid the TMP resource import step); Canvas built at runtime |
| Default orbit | ASSUMPTION | Circular, 185 km, 32.5 deg inclination |
| Earth radius | ASSUMPTION | Mean sphere, 6,371,000 m |
| Unity version | OPEN | Project is on 6000.6.9f1 (URP 17.6.0, Input System 1.20.1, ugui 2.6.0, Rider 3.1.0). Brief says "Unity 6 LTS": owner to confirm 6000.6 is acceptable |
| Player movement in V1 | ASSUMPTION | Walking with local artificial gravity (CharacterController); microgravity deferred |
| Input handling | ASSUMPTION | Direct Keyboard/Mouse polling via Input System package; no action asset yet |
| Scene creation | ASSUMPTION | Editor menu Apollo > Build V1 Scene generates the blockout scene (no hand-authored scene YAML) |
| Windows in interior | OPEN | Not in Step 3; best-effort later (needs scaled Earth camera) |
| Spacecraft attitude (V1) | ASSUMPTION | Nadir-pointing, derived from orbit state each frame; window wall faces Earth |
| Earth visuals | ASSUMPTION | Procedural noise texture, unlit; real texture can replace it later |
| Environment switch | ASSUMPTION | Tab key, built in Step 4 to allow testing Mission Control early |
| Look/walk controls | RESOLVED | Owner confirmed controls work after Step 4 fixes; cause of earlier report not identified |
| Telemetry set | ASSUMPTION | MET, orbit #, altitude, speed, period, inclination, eccentricity, apsis altitudes, semi-major axis, latitude, ECI position and velocity. No longitude or ground track (Earth rotation not modeled) |
| Temporary UI | ASSUMPTION | Debug overlay and mouse diagnostic removed; small controls hint (IMGUI) kept as placeholder |
| Earth rotation | ASSUMPTION | Sidereal rate, angle 0 at MET 0, spherical Earth; orbit remains inertial (no J2) |
| Control Center 1 view control | ASSUMPTION | Mouse drag orbits the view, wheel zooms (owner request: rotate Earth in control center) |
| Control Center 2 | ASSUMPTION | Flat equirectangular map, past track (1 orbit back, clamped at MET 0) and predicted track (1.5 orbits) from the analytic orbit |
| View switching keys | ASSUMPTION | Tab cycles Spacecraft, CC1, CC2; F1/F2/F3 jump directly |
| Interior style | ASSUMPTION | Command-Module-inspired cabin from primitives (3 couches, instrument panel, hatch, framed window); still a blockout, not accurate to Apollo |
| Real Earth map | ASSUMPTION | NASA Blue Marble (owner-supplied, 21600x10800) downscaled to 8192x4096 as Assets/Resources/EarthMap.jpg; original kept in Assets/SourceArt~/ (ignored by Unity). Procedural map remains as fallback |
