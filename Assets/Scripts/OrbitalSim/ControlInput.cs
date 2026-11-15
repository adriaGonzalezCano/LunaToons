namespace Apollo.OrbitalSim
{
    /// <summary>
    /// What the pilot is commanding, independent of keys and of Unity. Axis values are -1..1.
    /// Attitude: Pitch +1 = nose up, Yaw +1 = nose right, Roll +1 = roll right (right side down).
    /// Translation (body axes, used from V2 step 4): X +1 = forward (main engine), Y +1 = right, Z +1 = up.
    /// </summary>
    public struct ControlInput
    {
        public double Pitch, Yaw, Roll;
        public double TranslateX, TranslateY, TranslateZ;
        public bool StopRotation;

        public bool AnyAttitudeAxis => Pitch != 0 || Yaw != 0 || Roll != 0;
        public bool AnyTranslationAxis => TranslateX != 0 || TranslateY != 0 || TranslateZ != 0;

        public static ControlInput None => default;
    }
}
