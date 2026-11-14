# Coordinates and units

- Units: SI internally (m, s, m/s, rad). Angles are degrees only at Inspector fields.
- Frame: Earth-centered inertial (ECI). Origin at Earth center; Z toward the north pole; X toward the reference direction (vernal equinox); Y completes a right-handed system.
- Earth model: sphere, mean radius 6,371,000 m. Altitude = |position| - radius.
- mu(Earth) = 3.986004418e14 m^3/s^2.
- Unity is left-handed, Y-up. Conversion happens only in the view layer (planned mapping: Unity (x, y, z) = ECI (x, z, y)), never in the simulation.
- Earth rotation is not modeled in V1 (inertial frame, no ground track).

## View scales (Step 4)
- Interior and player: 1 unit = 1 m, at the world origin.
- Window view (ExternalView): outside world scaled 1 unit = 1000 m around the camera. Uniform scaling keeps viewing angles correct; Earth radius = 6371 units.
- Mission Control view (OrbitView): 1 unit = 100 km, placed at (100000, 0, 0) so the player camera (far plane 20000) never sees it.
- Spacecraft attitude in V1 (fixed, not simulated): +X along velocity, -Z toward Earth (window wall), Y = orbit normal. Earth is not rotated; the Earth texture is procedural noise, not real map data.
