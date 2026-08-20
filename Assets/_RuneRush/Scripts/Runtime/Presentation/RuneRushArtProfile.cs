using UnityEngine;

namespace RuneRush.UnityRuntime
{
    [CreateAssetMenu(menuName = "Rune Rush/Art Profile", fileName = "RuneRushArtProfile")]
    public sealed class RuneRushArtProfile : ScriptableObject
    {
        [Header("Vehicles")]
        [Tooltip("Ordered vehicle pool: player uses element 0, then each AI uses the next available element.")]
        public GameObject[] RacerVehicles = new GameObject[0];
        public GameObject PlayerVehicle;
        public GameObject AiVehicle;
        public GameObject Cockpit;

        [Header("Race objects")]
        public GameObject Pickup;
        public GameObject Mine;
        public GameObject BoostPad;
        public GameObject RepairPad;
        public GameObject Crate;
        public GameObject Barricade;
        public GameObject Oil;
        public GameObject Spinner;
        public GameObject Gate;

        [Header("Scene")]
        public GameObject Environment;
        public Material RoadMaterial;
        public Material ShoulderMaterial;
        public Material RuneMaterial;
        [Tooltip("Disable when your replacement environment already contains the road, gates and barriers.")]
        public bool GenerateProceduralTrack = true;
        [Tooltip("Disable when your replacement environment supplies all terrain and set dressing.")]
        public bool GenerateProceduralEnvironment = true;
        public bool UseProceduralFallback = true;
        [Tooltip("Enable only when this project should override scene lighting and fog. Disable to keep lighting authored under AUTHORED ENVIRONMENT.")]
        public bool OverrideSceneLighting = true;
    }

}
