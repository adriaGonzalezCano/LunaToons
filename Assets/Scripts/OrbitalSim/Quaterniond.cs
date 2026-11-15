using System;

namespace Apollo.OrbitalSim
{
    /// <summary>Unit quaternion (w, x, y, z), double precision. Used as body-to-inertial rotation.</summary>
    public readonly struct Quaterniond
    {
        public readonly double W, X, Y, Z;

        public Quaterniond(double w, double x, double y, double z) { W = w; X = x; Y = y; Z = z; }

        public static Quaterniond Identity => new Quaterniond(1, 0, 0, 0);

        public double Norm => Math.Sqrt(W * W + X * X + Y * Y + Z * Z);

        public Quaterniond Normalized()
        {
            double n = Norm;
            return new Quaterniond(W / n, X / n, Y / n, Z / n);
        }

        public static Quaterniond operator *(Quaterniond a, Quaterniond b) => new Quaterniond(
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

        public Quaterniond Conjugate() => new Quaterniond(W, -X, -Y, -Z);

        /// <summary>Rotation of angle radians about a unit axis.</summary>
        public static Quaterniond FromAxisAngle(Vector3d axis, double angle)
        {
            double s = Math.Sin(angle / 2) / axis.Magnitude;
            return new Quaterniond(Math.Cos(angle / 2), axis.X * s, axis.Y * s, axis.Z * s);
        }

        /// <summary>Rotates a vector (body frame to inertial frame when this is the body-to-inertial attitude).</summary>
        public Vector3d Rotate(Vector3d v)
        {
            Quaterniond p = this * new Quaterniond(0, v.X, v.Y, v.Z) * Conjugate();
            return new Vector3d(p.X, p.Y, p.Z);
        }
    }
}
