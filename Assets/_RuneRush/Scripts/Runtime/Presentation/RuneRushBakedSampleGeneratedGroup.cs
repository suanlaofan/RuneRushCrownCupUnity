using UnityEngine;

namespace RuneRush.UnityRuntime
{
    /// <summary>
    /// Identifies generated sample geometry so rebuilding never removes custom artist content.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuneRushBakedSampleGeneratedGroup : MonoBehaviour
    {
        [HideInInspector] public string GroupId;
    }
}
