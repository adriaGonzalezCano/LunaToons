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

        /// <summary>Rotation whose columns are the given orthonormal, right-handed body axes expressed in the inertial frame.</summary>
        public static Quaterniond FromBasis(Vector3d x, Vector3d y, Vector3d z)
        {
            double m00 = x.X, m10 = x.Y, m20 = x.Z;
            double m01 = y.X, m11 = y.Y, m21 = y.Z;
            double m02 = z.X, m12 = z.Y, m22 = z.Z;
            double trace = m00 + m11 + m22;
            if (trace > 0)
            {
                double s = 0.5 / Math.Sqrt(trace + 1.0);
                return new Quaterniond(0.25 / s, (m21 - m12) * s, (m02 - m20) * s, (m10 - m01) * s).Normalized();
            }
            if (m00 > m11 && m00 > m22)
            {
                double s = 2.0 * Math.Sqrt(1.0 + m00 - m11 - m22);
                return new Quaterniond((m21 - m12) / s, 0.25 * s, (m01 + m10) / s, (m02 + m20) / s).Normalized();
            }
            if (m11 > m22)
            {
                double s = 2.0 * Math.Sqrt(1.0 + m11 - m00 - m22);
                return new Quaterniond((m02 - m20) / s, (m01 + m10) / s, 0.25 * s, (m12 + m21) / s).Normalized();
            }
            double s2 = 2.0 * Math.Sqrt(1.0 + m22 - m00 - m11);
            return new Quaterniond((m10 - m01) / s2, (m02 + m20) / s2, (m12 + m21) / s2, 0.25 * s2).Normalized();
        }

        /// <summary>Rotates a vector (body frame to inertial frame when this is the body-to-inertial attitude).</summary>
        public Vector3d Rotate(Vector3d v)
        {
            Quaterniond p = this * new Quaterniond(0, v.X, v.Y, v.Z) * Conjugate();
            return new Vector3d(p.X, p.Y, p.Z);
        }
    }
}
