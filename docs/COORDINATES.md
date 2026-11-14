# Coordinates and units

- Units: SI internally (m, s, m/s, rad). Angles are degrees only at Inspector fields.
- Frame: Earth-centered inertial (ECI). Origin at Earth center; Z toward the north pole; X toward the reference direction (vernal equinox); Y completes a right-handed system.
- Earth model: sphere, mean radius 6,371,000 m. Altitude = |position| - radius.
- mu(Earth) = 3.986004418e14 m^3/s^2.
- Unity is left-handed, Y-up. Conversion happens only in the view layer (planned mapping: Unity (x, y, z) = ECI (x, z, y)), never in the simulation.
- Earth rotation: constant sidereal rate 7.2921159e-5 rad/s about +Z; rotation angle 0 at MET 0 (Greenwich meridian along +X, a working assumption). The orbit stays inertial; only the ground rotates. Longitude is east-positive.

## View scales (Step 4)
- Interior and player: 1 unit = 1 m, at the world origin.
- Window view (ExternalView): outside world scaled 1 unit = 1000 m around the camera. Uniform scaling keeps viewing angles correct; Earth radius = 6371 units.
- Mission Control view (OrbitView): 1 unit = 100 km, placed at (100000, 0, 0) so the player camera (far plane 20000) never sees it.
- Spacecraft attitude in V1 (fixed, not simulated): +X along velocity, -Z toward Earth (window wall), Y = orbit normal. Earth is not rotated; the Earth texture is procedural noise, not real map data.
- Earth rotation in Unity: ECI rotation +theta about Z = Unity rotation -theta about Y (the axis mapping flips handedness).
- Earth map texture: equirectangular, longitude -180 at the left edge. The UV sphere has u = 0 at longitude 0, so sphere materials use a 0.5 u-offset. The game loads `Assets/Resources/EarthMap.jpg` (NASA Blue Marble, 8192 x 4096, derived from the 21600 x 10800 original kept in `Assets/SourceArt~/`); the procedural map is only a fallback if that file is missing.
- Control Center 1 (orbit view) rig at (100000, 0, 0); Control Center 2 (flat map, orthographic camera) rig at (200000, 0, 0), map 200 x 100 units (x = longitude/180 * 100, y = latitude/90 * 50).
