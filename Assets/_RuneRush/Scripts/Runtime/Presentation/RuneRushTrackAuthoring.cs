using System;
using System.Collections.Generic;
using RuneRush;
using UnityEngine;

namespace RuneRush.UnityRuntime
{
    [DisallowMultipleComponent]
    public sealed class RuneRushTrackAuthoring : MonoBehaviour
    {
        [Tooltip("Children are read in hierarchy order when no explicit points are assigned.")]
        public Transform[] ControlPoints;
        public bool Closed = true;
        [Min(256)] public int ArcLengthDivisions = 2880;

        public TrackPathData CreatePath()
        {
            if (!Closed)
            {
                throw new InvalidOperationException("Rune Rush currently supports closed circuit tracks only. Enable Closed on the track authoring component.");
            }

            var points = new List<Vector3>();
            if (ControlPoints != null && ControlPoints.Length >= 4)
            {
                foreach (Transform point in ControlPoints)
                {
                    if (point != null) points.Add(point.position);
                }
            }
            else
            {
                for (int index = 0; index < transform.childCount; index++)
                {
                    points.Add(transform.GetChild(index).position);
                }
            }

            return points.Count >= 4
                ? new TrackPathData(points, true, ArcLengthDivisions)
                : TrackPathData.CreateHighcrag(ArcLengthDivisions);
        }
    }
}
