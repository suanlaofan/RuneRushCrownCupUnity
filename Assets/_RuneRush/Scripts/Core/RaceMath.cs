using System;
using System.Globalization;

namespace RuneRush
{
    public static class RaceMath
    {
        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static float Lerp(float from, float to, float amount)
        {
            return from + (to - from) * amount;
        }

        public static float Damp(float current, float target, float rate, float deltaTime)
        {
            return Lerp(current, target, 1f - (float)Math.Exp(-rate * deltaTime));
        }

        public static float Wrap(float value, float length)
        {
            if (length <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");
            }

            return ((value % length) + length) % length;
        }

        public static float SignedTrackDelta(float from, float to, float trackLength)
        {
            float raw = Wrap(to - from, trackLength);
            return raw > trackLength * 0.5f ? raw - trackLength : raw;
        }

        public static int JavaScriptRound(float value)
        {
            return value >= 0f
                ? (int)Math.Floor(value + 0.5f)
                : (int)Math.Ceiling(value - 0.5f);
        }

        public static string FormatTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                return "--:--.---";
            }

            int minutes = (int)Math.Floor(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, remainder);
        }
    }

    public sealed class SeededRandom
    {
        private uint _state;

        public SeededRandom(uint seed = 0x0051F15Eu)
        {
            _state = seed;
        }

        public double NextDouble()
        {
            uint value;
            unchecked
            {
                _state += 0x6D2B79F5u;
                value = _state;
                value = (value ^ (value >> 15)) * (value | 1u);
                value ^= value + (value ^ (value >> 7)) * (value | 61u);
            }

            return (value ^ (value >> 14)) / 4294967296.0;
        }

        public float NextFloat()
        {
            return (float)NextDouble();
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        public int NextIndex(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return Math.Min(count - 1, (int)(NextDouble() * count));
        }
    }
}
