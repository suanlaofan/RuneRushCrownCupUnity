using UnityEngine;

namespace RuneRush.UnityRuntime
{
    public enum RuneRushLayoutMarkerKind
    {
        Pickup,
        Obstacle,
        Pad
    }

    /// <summary>
    /// A persistent, Scene-view friendly marker.  Its position is projected onto the
    /// authoring track and copied into RuneRushSceneBindings.Layout by the editor tool.
    /// Runtime clones the current hierarchy, so model deletion, replacement and scale
    /// changes made beneath this GameObject are preserved in Play mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuneRushRaceLayoutMarker : MonoBehaviour
    {
        public RuneRushLayoutMarkerKind Kind;
        public ObstacleKind ObstacleKind = ObstacleKind.Crate;
        public PadKind PadKind = PadKind.Boost;
        [Range(-9f, 9f)] public float Lane;
        [Range(0f, 1f)] public float ProgressRatio;
        public float Phase;
        [Tooltip("Optional source prefab reference. Play mode uses the model hierarchy currently beneath this marker.")]
        public GameObject EditorVisualPrefab;

    }
}
