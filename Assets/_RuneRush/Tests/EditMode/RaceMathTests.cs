using NUnit.Framework;

namespace RuneRush.Tests
{
    public sealed class RaceMathTests
    {
        [Test]
        public void WrapAndSignedDeltaHandleStartLine()
        {
            Assert.That(RaceMath.Wrap(12f, 10f), Is.EqualTo(2f).Within(0.000001f));
            Assert.That(RaceMath.Wrap(-1f, 10f), Is.EqualTo(9f).Within(0.000001f));
            Assert.That(RaceMath.SignedTrackDelta(98f, 3f, 100f), Is.EqualTo(5f).Within(0.000001f));
            Assert.That(RaceMath.SignedTrackDelta(3f, 98f, 100f), Is.EqualTo(-5f).Within(0.000001f));
        }

        [Test]
        public void SeededRandomMatchesWebSequence()
        {
            var random = new SeededRandom(0x12345678u);
            double[] expected =
            {
                0.10615200875326991,
                0.941276284167543,
                0.9398706152569503,
                0.2338848018553108,
                0.9045877147000283
            };

            for (int index = 0; index < expected.Length; index += 1)
            {
                Assert.That(random.NextDouble(), Is.EqualTo(expected[index]).Within(1e-15));
            }
        }
    }
}
