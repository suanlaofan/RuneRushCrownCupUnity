using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneRush
{
    public struct TrackSample
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Right;
        public Quaternion Rotation;

        public TrackSample(Vector3 position, Vector3 tangent, Vector3 right, Quaternion rotation)
        {
            Position = position;
            Tangent = tangent;
            Right = right;
            Rotation = rotation;
        }
    }

    [Serializable]
    public sealed class TrackPathData
    {
        private readonly Vector3[] _controlPoints;
        private readonly float[] _arcLengths;
        private readonly bool _closed;

        public IReadOnlyList<Vector3> ControlPoints
        {
            get { return _controlPoints; }
        }

        public bool Closed
        {
            get { return _closed; }
        }

        public float Length
        {
            get { return _arcLengths[_arcLengths.Length - 1]; }
        }

        public int ArcLengthDivisions
        {
            get { return _arcLengths.Length - 1; }
        }

        public TrackPathData(IReadOnlyList<Vector3> controlPoints, bool closed = true, int arcLengthDivisions = 2880)
        {
            if (controlPoints == null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            if (controlPoints.Count < 4)
            {
                throw new ArgumentException("A track needs at least four control points.", nameof(controlPoints));
            }

            _controlPoints = new Vector3[controlPoints.Count];
            for (int index = 0; index < controlPoints.Count; index += 1)
            {
                _controlPoints[index] = controlPoints[index];
            }

            _closed = closed;
            int divisions = Math.Max(32, arcLengthDivisions);
            _arcLengths = new float[divisions + 1];
            BuildArcLengthTable();
        }

        public static TrackPathData CreateHighcrag(int arcLengthDivisions = 2880)
        {
            const int count = 18;
            var points = new Vector3[count];
            for (int index = 0; index < count; index += 1)
            {
                float angle = index / (float)count * Mathf.PI * 2f;
                float radius = 190f
                    + Mathf.Sin(angle * 3f + 0.45f) * 29f
                    + Mathf.Sin(angle * 5f - 0.9f) * 13f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius * 0.79f;
                float y = Mathf.Sin(angle * 2f - 0.3f) * 3.8f
                    + Mathf.Sin(angle * 5f) * 1.5f;
                points[index] = new Vector3(x, y, z);
            }

            return new TrackPathData(points, true, arcLengthDivisions);
        }

        public TrackSample Sample(float distance, float lane = 0f, float height = 0f)
        {
            float parameter = DistanceToParameter(distance);
            Vector3 position = Evaluate(parameter);
            Vector3 tangent = EvaluateTangent(parameter);
            Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);
            if (right.sqrMagnitude < 0.000001f)
            {
                right = Vector3.right;
            }
            else
            {
                right.Normalize();
            }

            position += right * lane;
            position.y += height;
            Quaternion rotation = RotationFromModelForward(tangent);
            return new TrackSample(position, tangent, right, rotation);
        }

        public Vector3 EvaluateByDistance(float distance)
        {
            return Evaluate(DistanceToParameter(distance));
        }

        public Vector3[] CreateMapPoints(int count = 180)
        {
            int sampleCount = Math.Max(2, count);
            var points = new Vector3[sampleCount + 1];
            for (int index = 0; index <= sampleCount; index += 1)
            {
                points[index] = Evaluate(index / (float)sampleCount);
            }

            return points;
        }

        public float DistanceToParameter(float distance)
        {
            float target = _closed
                ? RaceMath.Wrap(distance, Length)
                : RaceMath.Clamp(distance, 0f, Length);

            int low = 0;
            int high = _arcLengths.Length - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                float value = _arcLengths[middle];
                if (value < target)
                {
                    low = middle + 1;
                }
                else if (value > target)
                {
                    high = middle - 1;
                }
                else
                {
                    high = middle;
                    break;
                }
            }

            int index = Math.Max(0, high);
            if (_arcLengths[index] == target)
            {
                return index / (float)ArcLengthDivisions;
            }

            int next = Math.Min(index + 1, ArcLengthDivisions);
            float segmentLength = _arcLengths[next] - _arcLengths[index];
            float fraction = segmentLength <= 0.000001f
                ? 0f
                : (target - _arcLengths[index]) / segmentLength;
            return (index + fraction) / ArcLengthDivisions;
        }

        public Vector3 Evaluate(float parameter)
        {
            int count = _controlPoints.Length;
            float t = _closed
                ? parameter - Mathf.Floor(parameter)
                : Mathf.Clamp01(parameter);
            float scaled = (count - (_closed ? 0 : 1)) * t;
            int pointIndex = Mathf.FloorToInt(scaled);
            float weight = scaled - pointIndex;

            if (!_closed && weight == 0f && pointIndex == count - 1)
            {
                pointIndex = count - 2;
                weight = 1f;
            }

            Vector3 p0;
            Vector3 p1;
            Vector3 p2;
            Vector3 p3;
            if (_closed)
            {
                pointIndex = PositiveModulo(pointIndex, count);
                p0 = _controlPoints[PositiveModulo(pointIndex - 1, count)];
                p1 = _controlPoints[pointIndex];
                p2 = _controlPoints[(pointIndex + 1) % count];
                p3 = _controlPoints[(pointIndex + 2) % count];
            }
            else
            {
                p1 = _controlPoints[pointIndex];
                p2 = _controlPoints[pointIndex + 1];
                p0 = pointIndex > 0
                    ? _controlPoints[pointIndex - 1]
                    : _controlPoints[0] * 2f - _controlPoints[1];
                p3 = pointIndex + 2 < count
                    ? _controlPoints[pointIndex + 2]
                    : _controlPoints[count - 1] * 2f - _controlPoints[count - 2];
            }

            float dt0 = Mathf.Pow((p0 - p1).sqrMagnitude, 0.25f);
            float dt1 = Mathf.Pow((p1 - p2).sqrMagnitude, 0.25f);
            float dt2 = Mathf.Pow((p2 - p3).sqrMagnitude, 0.25f);
            if (dt1 < 0.0001f) dt1 = 1f;
            if (dt0 < 0.0001f) dt0 = dt1;
            if (dt2 < 0.0001f) dt2 = dt1;

            return new Vector3(
                EvaluateCentripetal(p0.x, p1.x, p2.x, p3.x, dt0, dt1, dt2, weight),
                EvaluateCentripetal(p0.y, p1.y, p2.y, p3.y, dt0, dt1, dt2, weight),
                EvaluateCentripetal(p0.z, p1.z, p2.z, p3.z, dt0, dt1, dt2, weight));
        }

        private void BuildArcLengthTable()
        {
            Vector3 previous = Evaluate(0f);
            float length = 0f;
            _arcLengths[0] = 0f;
            for (int index = 1; index < _arcLengths.Length; index += 1)
            {
                Vector3 current = Evaluate(index / (float)ArcLengthDivisions);
                length += Vector3.Distance(previous, current);
                _arcLengths[index] = length;
                previous = current;
            }
        }

        private Vector3 EvaluateTangent(float parameter)
        {
            const float delta = 0.0001f;
            float before = _closed
                ? parameter - delta
                : Mathf.Max(0f, parameter - delta);
            float after = _closed
                ? parameter + delta
                : Mathf.Min(1f, parameter + delta);
            Vector3 tangent = Evaluate(after) - Evaluate(before);
            if (tangent.sqrMagnitude < 0.000001f)
            {
                return Vector3.forward;
            }

            return tangent.normalized;
        }

        private static float EvaluateCentripetal(
            float x0,
            float x1,
            float x2,
            float x3,
            float dt0,
            float dt1,
            float dt2,
            float t)
        {
            float tangent1 = (x1 - x0) / dt0
                - (x2 - x0) / (dt0 + dt1)
                + (x2 - x1) / dt1;
            float tangent2 = (x2 - x1) / dt1
                - (x3 - x1) / (dt1 + dt2)
                + (x3 - x2) / dt2;
            tangent1 *= dt1;
            tangent2 *= dt1;

            float c0 = x1;
            float c1 = tangent1;
            float c2 = -3f * x1 + 3f * x2 - 2f * tangent1 - tangent2;
            float c3 = 2f * x1 - 2f * x2 + tangent1 + tangent2;
            float squared = t * t;
            return c0 + c1 * t + c2 * squared + c3 * squared * t;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            return ((value % divisor) + divisor) % divisor;
        }

        private static Quaternion RotationFromModelForward(Vector3 tangent)
        {
            Vector3 from = Vector3.back;
            float dot = Vector3.Dot(from, tangent);
            if (dot > 0.999999f)
            {
                return Quaternion.identity;
            }

            if (dot < -0.999999f)
            {
                return new Quaternion(0f, 1f, 0f, 0f);
            }

            Vector3 cross = Vector3.Cross(from, tangent);
            float scale = (float)Math.Sqrt((1f + dot) * 2f);
            float inverse = 1f / scale;
            Quaternion rotation = new Quaternion(
                cross.x * inverse,
                cross.y * inverse,
                cross.z * inverse,
                scale * 0.5f);
            float magnitude = (float)Math.Sqrt(
                rotation.x * rotation.x
                + rotation.y * rotation.y
                + rotation.z * rotation.z
                + rotation.w * rotation.w);
            float inverseMagnitude = magnitude > 0.000001f ? 1f / magnitude : 1f;
            return new Quaternion(
                rotation.x * inverseMagnitude,
                rotation.y * inverseMagnitude,
                rotation.z * inverseMagnitude,
                rotation.w * inverseMagnitude);
        }
    }
}
