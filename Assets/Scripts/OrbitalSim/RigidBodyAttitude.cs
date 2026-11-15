using System;

namespace Apollo.OrbitalSim
{
    /// <summary>
    /// Attitude of a rigid body: quaternion (body to inertial) and body-frame angular rate, advanced with Euler's equations
    /// I dw/dt = torque - w x (I w) using the fixed-step RK8 integrator. Without torque the angular momentum is conserved and a
    /// rotation persists. Body axes match the cabin: +X forward (the way the couches face, main engine), +Y left, +Z up (right-handed); roll about X, pitch about Y, yaw about Z.
    /// Pure C#, deterministic: callers advance it in fixed steps.
    /// </summary>
    public sealed class RigidBodyAttitude
    {
        /// <summary>Rates below this (rad/s) are snapped to zero by the stop-rotation law.</summary>
        public const double StopSnapRate = 1e-4;

        private readonly Rk8Integrator integrator = new Rk8Integrator(7);
        private readonly double[] y = new double[7];
        private readonly OdeFunction derivatives;
        private Vector3d torque;

        public RigidBodyAttitude(Vector3d principalInertia, Quaterniond attitude, Vector3d bodyRate)
        {
            if (!(principalInertia.X > 0 && principalInertia.Y > 0 && principalInertia.Z > 0))
                throw new ArgumentOutOfRangeException(nameof(principalInertia));
            Inertia = principalInertia;
            Attitude = attitude.Normalized();
            Rate = bodyRate;
            derivatives = Derivatives;
        }

        /// <summary>Principal moments of inertia about body X, Y, Z (kg m^2).</summary>
        public Vector3d Inertia { get; }

        /// <summary>Body-to-inertial rotation.</summary>
        public Quaterniond Attitude { get; private set; }

        /// <summary>Angular rate in the body frame (rad/s).</summary>
        public Vector3d Rate { get; private set; }

        /// <summary>Angular momentum in the inertial frame (conserved with no torque).</summary>
        public Vector3d AngularMomentumInertial =>
            Attitude.Rotate(new Vector3d(Inertia.X * Rate.X, Inertia.Y * Rate.Y, Inertia.Z * Rate.Z));

        public double RotationalEnergy =>
            0.5 * (Inertia.X * Rate.X * Rate.X + Inertia.Y * Rate.Y * Rate.Y + Inertia.Z * Rate.Z * Rate.Z);

        /// <summary>Body torque (N m) applied during the following steps.</summary>
        public void SetTorque(Vector3d bodyTorque) => torque = bodyTorque;

        /// <summary>Advances the state by h seconds (one fixed step).</summary>
        public void Step(double h)
        {
            y[0] = Attitude.W; y[1] = Attitude.X; y[2] = Attitude.Y; y[3] = Attitude.Z;
            y[4] = Rate.X; y[5] = Rate.Y; y[6] = Rate.Z;
            integrator.Step(derivatives, 0.0, y, h);
            Attitude = new Quaterniond(y[0], y[1], y[2], y[3]).Normalized();
            Rate = new Vector3d(y[4], y[5], y[6]);
        }

        /// <summary>
        /// Stop-rotation torque: opposes the rate with a proportional law saturated at maxAngularAcceleration (rad/s^2), so the
        /// rate ramps down smoothly and never reverses. Rates below StopSnapRate are set to zero (call after Step).
        /// </summary>
        public Vector3d StopRotationTorque(double maxAngularAcceleration, double timeConstant = 1.0)
        {
            return new Vector3d(
                -Saturate(Rate.X / (maxAngularAcceleration * timeConstant)) * maxAngularAcceleration * Inertia.X,
                -Saturate(Rate.Y / (maxAngularAcceleration * timeConstant)) * maxAngularAcceleration * Inertia.Y,
                -Saturate(Rate.Z / (maxAngularAcceleration * timeConstant)) * maxAngularAcceleration * Inertia.Z);
        }

        /// <summary>Zero the rate if it is below the snap threshold. Returns true if the body is (now) not rotating.</summary>
        public bool SnapToRest()
        {
            if (Rate.Magnitude < StopSnapRate)
            {
                Rate = new Vector3d(0, 0, 0);
                return true;
            }
            return false;
        }

        private static double Saturate(double v) => Math.Max(-1.0, Math.Min(1.0, v));

        // y = [qw qx qy qz wx wy wz]
        private void Derivatives(double t, double[] s, double[] d)
        {
            double wx = s[4], wy = s[5], wz = s[6];
            // dq/dt = 0.5 * q * (0, w)
            d[0] = 0.5 * (-s[1] * wx - s[2] * wy - s[3] * wz);
            d[1] = 0.5 * (s[0] * wx + s[2] * wz - s[3] * wy);
            d[2] = 0.5 * (s[0] * wy - s[1] * wz + s[3] * wx);
            d[3] = 0.5 * (s[0] * wz + s[1] * wy - s[2] * wx);

            double ix = Inertia.X, iy = Inertia.Y, iz = Inertia.Z;
            double hx = ix * wx, hy = iy * wy, hz = iz * wz;
            d[4] = (torque.X - (wy * hz - wz * hy)) / ix;
            d[5] = (torque.Y - (wz * hx - wx * hz)) / iy;
            d[6] = (torque.Z - (wx * hy - wy * hx)) / iz;
        }
    }
}
