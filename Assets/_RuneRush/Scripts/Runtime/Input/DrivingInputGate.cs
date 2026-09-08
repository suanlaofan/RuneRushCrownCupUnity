using RuneRush;

namespace RuneRush.UnityRuntime
{
    public sealed class DrivingInputGate
    {
        private bool _waitForNeutral;
        public void RequireRelease() => _waitForNeutral = true;

        public RaceInputFrame Filter(RaceInputFrame raw, bool uiOwnsInput)
        {
            if (uiOwnsInput) _waitForNeutral = true;
            if (_waitForNeutral)
            {
                bool held = raw.Throttle > 0.1f || raw.Brake > 0.1f
                    || System.Math.Abs(raw.Steer) > 0.15f || raw.Drift || raw.Nitro || raw.UseItem || raw.Reset;
                if (!uiOwnsInput && !held) _waitForNeutral = false;
                return RaceInputFrame.Idle;
            }
            return raw;
        }
    }
}
