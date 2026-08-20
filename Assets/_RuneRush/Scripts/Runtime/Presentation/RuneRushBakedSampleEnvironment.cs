using UnityEngine;

namespace RuneRush.UnityRuntime
{
    /// <summary>
    /// Marks the optional, scene-persistent sample environment created by the editor baking tool.
    /// The marker is intentionally lightweight: all of the visible road and scenery are ordinary
    /// scene GameObjects that an artist can move, delete, replace or augment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuneRushBakedSampleEnvironment : MonoBehaviour
    {
        [Header("Runtime presentation")]
        [Tooltip("Use the baked road pieces instead of creating a second procedural runtime road.")]
        public bool UseBakedTrackAtRuntime = true;

        [Tooltip("Use the baked terrain and landmarks instead of creating duplicate procedural runtime scenery.")]
        public bool UseBakedEnvironmentAtRuntime = true;

        [Tooltip("Keep the editable sample lights instead of allowing the runtime to replace all scene lighting.")]
        public bool UseBakedLightingAtRuntime = true;

        [Header("Safe authoring area")]
        [Tooltip("Put replacement models and hand-authored set dressing here. Rebuild Sample Geometry never removes this transform or its children.")]
        public Transform CustomContentRoot;

        [HideInInspector] public int BakeVersion = 1;
    }

}
