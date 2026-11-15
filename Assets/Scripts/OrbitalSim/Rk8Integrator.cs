using System;

namespace Apollo.OrbitalSim
{
    /// <summary>Right-hand side of y' = f(t, y). Writes the derivative into <paramref name="derivative"/>.</summary>
    public delegate void OdeFunction(double t, double[] y, double[] derivative);

    /// <summary>
    /// Fixed-step eighth-order Runge-Kutta integrator (the 12-stage core of Dormand-Prince DOP853, coefficients in
    /// Rk8Coefficients). Fixed steps keep the result independent of frame rate and deterministic. Pure C#, no allocation per step.
    /// </summary>
    public sealed class Rk8Integrator
    {
        private readonly int dimension;
        private readonly double[][] k;
        private readonly double[] stageState;

        public Rk8Integrator(int dimension)
        {
            this.dimension = dimension;
            k = new double[Rk8Coefficients.Stages][];
            for (int i = 0; i < k.Length; i++) k[i] = new double[dimension];
            stageState = new double[dimension];
        }

        /// <summary>Advances <paramref name="y"/> in place from t to t + h.</summary>
        public void Step(OdeFunction f, double t, double[] y, double h)
        {
            for (int i = 0; i < Rk8Coefficients.Stages; i++)
            {
                Array.Copy(y, stageState, dimension);
                double[] row = Rk8Coefficients.A[i];
                for (int j = 0; j < i; j++)
                {
                    double a = row[j];
                    if (a == 0.0) continue;
                    double ah = a * h;
                    double[] kj = k[j];
                    for (int n = 0; n < dimension; n++) stageState[n] += ah * kj[n];
                }
                f(t + Rk8Coefficients.C[i] * h, stageState, k[i]);
            }

            for (int i = 0; i < Rk8Coefficients.Stages; i++)
            {
                double b = Rk8Coefficients.B[i];
                if (b == 0.0) continue;
                double bh = b * h;
                double[] ki = k[i];
                for (int n = 0; n < dimension; n++) y[n] += bh * ki[n];
            }
        }
    }
}
