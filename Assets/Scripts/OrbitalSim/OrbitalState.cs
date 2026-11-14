namespace Apollo.OrbitalSim
{
    /// <summary>
    /// Snapshot of the spacecraft orbital state. SI units, Earth-centered inertial frame
    /// (see docs/COORDINATES.md). Single source of truth for every system that shows orbital data.
    /// </summary>
    public readonly struct OrbitalState
    {
        public readonly double MissionElapsedTime; // s
        public readonly Vector3d Position;         // m, relative to Earth center
        public readonly Vector3d Velocity;         // m/s, relative to Earth center
        public readonly double Altitude;           // m above spherical Earth surface
        public readonly double Speed;              // m/s
        public readonly double Period;             // s

        public OrbitalState(double met, Vector3d position, Vector3d velocity, double altitude, double speed, double period)
        {
            MissionElapsedTime = met;
            Position = position;
            Velocity = velocity;
            Altitude = altitude;
            Speed = speed;
            Period = period;
        }
    }
}
