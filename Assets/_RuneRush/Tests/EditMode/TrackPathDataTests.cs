using NUnit.Framework;
using UnityEngine;

namespace RuneRush.Tests
{
    public sealed class TrackPathDataTests
    {
        [Test]
        public void HighcragPathIsClosedAndKeepsOriginalLength()
        {
            TrackPathData path = TrackPathData.CreateHighcrag();

            Assert.That(path.ControlPoints.Count, Is.EqualTo(18));
            Assert.That(path.Closed, Is.True);
            Assert.That(path.Length, Is.EqualTo(1150.203f).Within(0.35f));
            Assert.That(
                Vector3.Distance(path.EvaluateByDistance(0f), path.EvaluateByDistance(path.Length)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void SampleProducesStableOrthonormalTrackFrame()
        {
            TrackPathData path = TrackPathData.CreateHighcrag(720);
            TrackSample sample = path.Sample(path.Length * 0.37f, 3f, 1.2f);

            Assert.That(sample.Tangent.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(sample.Right.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Vector3.Dot(sample.Tangent, sample.Right), Is.EqualTo(0f).Within(0.001f));
            Assert.That(sample.Position, Is.Not.EqualTo(Vector3.zero));
        }
    }
}
