using System;

namespace Apollo.OrbitalSim
{
    /// <summary>Minimal double-precision 3D vector (Unity's Vector3 is float and loses precision at orbital distances).</summary>
    public readonly struct Vector3d
    {
        public readonly double X, Y, Z;

        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

        public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3d Cross(Vector3d a, Vector3d b) =>
            new Vector3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3d operator *(Vector3d a, double s) => new Vector3d(a.X * s, a.Y * s, a.Z * s);

        public override string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
    }
}
