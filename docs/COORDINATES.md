# Coordinates and units

- Units: SI internally (m, s, m/s, rad). Angles are degrees only at Inspector fields.
- Frame: Earth-centered inertial (ECI). Origin at Earth center; Z toward the north pole; X toward the reference direction (vernal equinox); Y completes a right-handed system.
- Earth model: sphere, mean radius 6,371,000 m. Altitude = |position| - radius.
- mu(Earth) = 3.986004418e14 m^3/s^2.
- Unity is left-handed, Y-up. Conversion happens only in the view layer (planned mapping: Unity (x, y, z) = ECI (x, z, y)), never in the simulation.
- Earth rotation is not modeled in V1 (inertial frame, no ground track).
