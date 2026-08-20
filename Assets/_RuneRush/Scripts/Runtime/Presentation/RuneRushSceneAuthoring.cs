using UnityEngine;

namespace RuneRush.UnityRuntime
{
    /// <summary>
    /// Persistent scene-level editing options.  The editor uses it to show a useful
    /// full-scene preview without running gameplay.  It is intentionally inert at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuneRushSceneAuthoring : MonoBehaviour
    {
        [Header("Scene view preview")]
        public bool ShowTrackPreview = true;
        public bool ShowGeneratedLandmarks = true;
        public bool ShowRaceLayoutMarkers = true;
        [Range(6f, 30f)] public float PreviewRoadWidth = 18f;
        public Color RoadPreviewColor = new Color(0.16f, 0.38f, 0.48f, 0.58f);
        public Color ShoulderPreviewColor = new Color(0.34f, 0.31f, 0.24f, 0.34f);
    }
}
