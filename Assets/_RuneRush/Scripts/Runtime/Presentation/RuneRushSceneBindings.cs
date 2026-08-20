using UnityEngine;

namespace RuneRush.UnityRuntime
{
    /// <summary>
    /// Connects persistent, artist-editable scene content to the runtime race presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuneRushSceneBindings : MonoBehaviour
    {
        public RuneRushArtProfile ArtProfile;

        [Header("Persistent scene authoring")]
        [Tooltip("Put hand-authored terrain, buildings, lighting and static set dressing here. The race runtime never clears this root.")]
        public Transform AuthoredEnvironmentRoot;

        [Tooltip("Place editable pickup, obstacle and pad markers here. Their Scene positions drive the runtime layout.")]
        public Transform LayoutMarkerRoot;

        [Tooltip("Track-relative pickup, obstacle and pad data synchronized from the editable scene markers.")]
        public RaceLayout Layout = new RaceLayout();

        [Header("Runtime-managed roots")]
        public Transform EnvironmentRoot;
        public Transform RaceObjectRoot;
        public Transform RacerRoot;
        public Transform PlayerSpawn;

        public void EnsureRoots()
        {
            AuthoredEnvironmentRoot = EnsureChild(AuthoredEnvironmentRoot, "AUTHORED ENVIRONMENT - Put Editable Models Here");
            LayoutMarkerRoot = EnsureChild(LayoutMarkerRoot, "RACE LAYOUT - Editable Markers");
            EnvironmentRoot = EnsureChild(EnvironmentRoot, "EnvironmentRoot");
            RaceObjectRoot = EnsureChild(RaceObjectRoot, "RaceObjectRoot");
            RacerRoot = EnsureChild(RacerRoot, "RacerRoot");
        }

        private Transform EnsureChild(Transform current, string childName)
        {
            if (current != null)
            {
                return current;
            }

            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.transform;
        }
    }
}
