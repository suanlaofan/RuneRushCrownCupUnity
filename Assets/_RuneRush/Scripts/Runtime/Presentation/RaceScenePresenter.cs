using System;
using System.Collections.Generic;
using RuneRush;
using RuneRush.XR;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneRush.UnityRuntime
{
    public sealed class RaceScenePresenter : MonoBehaviour
    {
        private readonly Dictionary<string, RacerVisual> _racerVisuals = new();
        private readonly Dictionary<string, GameObject> _pickupVisuals = new();
        private readonly Dictionary<string, GameObject> _obstacleVisuals = new();
        private readonly Dictionary<string, GameObject> _padVisuals = new();
        private readonly Dictionary<string, GameObject> _mineVisuals = new();
        private readonly Dictionary<string, Quaternion> _sceneLayoutRotationOffsets = new();
        private readonly Dictionary<string, Quaternion> _racerModelRotations = new();
        private readonly Dictionary<RuneRushRaceLayoutMarker, bool> _sourceMarkerActiveStates = new();

        private TrackPathData _track;
        private RaceSimulation _simulation;
        private RuneRushSceneBindings _bindings;
        private RuneRushArtProfile _art;
        private IReadOnlyDictionary<string, RuneRushRaceLayoutMarker> _sceneLayoutMarkers;
        // Single XR motion authority: this vehicle root receives TrackSample
        // movement.  XROrigin and the HMD are fixed descendants and never receive
        // game-input-driven translation.
        private Transform _driverVehicleRoot;
        private Transform _cockpitAnchor;
        private Transform _cameraRig;
        private Camera _camera;
        private Behaviour _trackedPoseDriver;
        private GameObject _cockpit;
        private Transform _playerVisual;
        private float _impact;
        private float _menuDistance;
        private bool _xrWorldAnchorInitialized;
        private bool _xrGroundSafetyReported;
        private bool _xrProgressSyncReported;
        private bool _xrHierarchyReported;
        private TrackSample _lastValidPlayerRoad;
        private bool _hasLastValidPlayerRoad;

        private static readonly Vector3 FirstPersonCameraPosition = new(0f, 1.85f, -0.05f);
        // Floor-tracked PICO head pose normally contributes ~1.60m of the
        // desktop camera's 1.85m eye height.  The remaining stable seat offset
        // preserves the original composition without writing the HMD local pose.
        private static readonly Vector3 XrSeatOriginOffset = new(0f, 0.25f, -0.05f);
        // The cockpit model is a normalized vehicle prefab: keep its FBX axis conversion
        // and place only its lower, forward section in the driver's view.
        private static readonly Vector3 CockpitPosition = new(0f, -0.62f, 1.28f);
        private static readonly Vector3 CockpitScale = new(0.62f, 0.62f, 0.62f);

        private Material _road;
        private Material _shoulder;
        private Material _stone;
        private Material _iron;
        private Material _wood;
        private Material _foliageDark;
        private Material _foliageLight;
        private Material _mountain;
        private Material _gold;
        private Material _rune;

        public Camera WorldCamera => _camera;

        public void Initialize(
            TrackPathData track,
            RaceSimulation simulation,
            RuneRushSceneBindings bindings,
            IReadOnlyDictionary<string, RuneRushRaceLayoutMarker> sceneLayoutMarkers = null)
        {
            RestoreSceneLayoutSources();
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _bindings = bindings != null ? bindings : gameObject.AddComponent<RuneRushSceneBindings>();
            _bindings.EnsureRoots();
            _art = _bindings.ArtProfile;
            _sceneLayoutMarkers = sceneLayoutMarkers;
            ResetRuntimePresentation();

            CreateMaterials();
            RuneRushBakedSampleEnvironment bakedSample = _bindings.AuthoredEnvironmentRoot != null
                ? _bindings.AuthoredEnvironmentRoot.GetComponentInChildren<RuneRushBakedSampleEnvironment>(true)
                : null;
            bool bakedSampleActive = bakedSample != null && bakedSample.isActiveAndEnabled;
            bool useBakedTrack = bakedSampleActive && bakedSample.UseBakedTrackAtRuntime;
            bool useBakedEnvironment = bakedSampleActive && bakedSample.UseBakedEnvironmentAtRuntime;
            bool useBakedLighting = bakedSampleActive && bakedSample.UseBakedLightingAtRuntime;

            if (!useBakedLighting && (_art == null || _art.OverrideSceneLighting)) ConfigureWorldLighting();
            CreateCamera();
            if (!useBakedTrack && (_art == null || _art.GenerateProceduralTrack)) BuildTrack();
            if (!useBakedEnvironment) BuildEnvironment();
            InitializeXrWorldAnchor();
            BuildRaceObjects();
            BuildRacers();
            BuildCockpit();
            Sync(RaceInputFrame.Idle, 0f);
            ValidateSceneLayoutVisuals();
            ValidateRacerVisualOrientation();
            HideSceneLayoutSources();
        }

        private void OnDestroy()
        {
            RestoreSceneLayoutSources();
        }

        private void ResetRuntimePresentation()
        {
            _racerVisuals.Clear();
            _pickupVisuals.Clear();
            _obstacleVisuals.Clear();
            _padVisuals.Clear();
            _mineVisuals.Clear();
            _sceneLayoutRotationOffsets.Clear();
            _racerModelRotations.Clear();
            _trackedPoseDriver = null;
            _driverVehicleRoot = null;
            _cockpitAnchor = null;
            _cockpit = null;
            _playerVisual = null;
            _impact = 0f;
            _menuDistance = 0f;
            _xrWorldAnchorInitialized = false;
            _xrGroundSafetyReported = false;
            _xrProgressSyncReported = false;
            _xrHierarchyReported = false;
            _hasLastValidPlayerRoad = false;

            ClearChildren(_bindings.EnvironmentRoot);
            ClearChildren(_bindings.RaceObjectRoot);
            ClearChildren(_bindings.RacerRoot);

            GameObject oldSun = GameObject.Find("Moonlit Sun");
            if (oldSun != null) DestroyImmediate(oldSun);
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                DestroyImmediate(root.GetChild(index).gameObject);
            }
        }

        public void Sync(RaceInputFrame input, float deltaTime)
        {
            if (_simulation == null) return;
            float dt = Mathf.Clamp(deltaTime, 0f, 0.05f);
            bool menu = _simulation.Phase == RacePhase.Menu;
            bool xrActive = RefreshCameraMode();

            UpdateRacers(input, dt);
            UpdateRaceObjects(dt);
            UpdateMines(dt);

            if (menu)
            {
                _menuDistance += dt * 31f;
                TrackSample menuSample = _track.Sample(_menuDistance, -3.6f, 0f);
                SetDriverVehiclePose(menuSample, xrActive, dt);
                if (!xrActive)
                {
                    _camera.transform.localPosition = FirstPersonCameraPosition;
                    _camera.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                // The XR origin and player vehicle must use the same validated road
                // sample.  Do not reintroduce a separate, height-locked camera path.
                TrackSample sample = GetSafeRoadSample(_simulation.Player.Progress, _simulation.Player.Lane, 0f, true);
                SetDriverVehiclePose(sample, xrActive, dt);
                if (!xrActive)
                {
                    // Controls are race-only.  The desktop fallback preserves the
                    // same fixed seat without steer, throttle, collision, or nitro
                    // altering its local transform or FOV.
                    _camera.transform.localPosition = FirstPersonCameraPosition;
                    _camera.transform.localRotation = Quaternion.identity;
                    _camera.fieldOfView = 72f;
                }
            }

            _impact = Mathf.Max(0f, _impact - dt * 2.6f);
        }

        // Desktop still drives its non-XR camera rig directly.  XR deliberately
        // does not: TrackSample moves only Driver Vehicle Root.  Beneath it the
        // fixed Cockpit Anchor carries XROrigin -> Camera Offset -> tracked HMD.
        // Thus game controls cannot translate a camera or overwrite HMD 6DoF.
        private void SetDriverVehiclePose(TrackSample sample, bool xrActive, float deltaTime)
        {
            if (_cameraRig == null) return;
            if (!xrActive)
            {
                _cameraRig.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Tangent, Vector3.up));
                return;
            }

            EnsureXrDrivingHierarchy();
            if (_driverVehicleRoot == null || _cockpitAnchor == null) return;
            Vector3 flatForward = Vector3.ProjectOnPlane(sample.Tangent, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = _driverVehicleRoot.forward;
            float targetYaw = Quaternion.LookRotation(flatForward.normalized, Vector3.up).eulerAngles.y;
            float yaw = Mathf.MoveTowardsAngle(_driverVehicleRoot.eulerAngles.y, targetYaw, 75f * deltaTime);
            // Exact TrackSample XYZ avoids any below-road lag.  Yaw-only rotation
            // keeps road pitch/roll out of the user's head pose.
            _driverVehicleRoot.SetPositionAndRotation(sample.Position, Quaternion.Euler(0f, yaw, 0f));

            if (!_xrProgressSyncReported && _simulation != null && _track != null
                && _simulation.Phase != RacePhase.Menu
                && _simulation.Player.Progress >= Mathf.Min(24f, _track.Length * 0.03f))
            {
                _xrProgressSyncReported = true;
                float expectedRootY = sample.Position.y;
                Debug.Log($"RUNE_RUSH_XR_PROGRESS_SYNC: progress={_simulation.Player.Progress:F1}; trackY={sample.Position.y:F2}; vehicleRootY={_driverVehicleRoot.position.y:F2}; deltaY={(_driverVehicleRoot.position.y - expectedRootY):F3}; cockpitLocal={_cockpitAnchor.localPosition:F2}; xrOriginLocal={_cameraRig.localPosition:F2}; inputCameraTranslation=false; yawOnly=true; groundFallback=false.");
            }
        }

        private void InitializeXrWorldAnchor()
        {
            if (_xrWorldAnchorInitialized || !IsPicoXrRuntime() || _cameraRig == null || _track == null) return;
            EnsureXrDrivingHierarchy();
            if (_driverVehicleRoot == null || _cockpitAnchor == null) return;

            TrackSample start = _track.Sample(0f, 0f, 0f);
            _lastValidPlayerRoad = start;
            _hasLastValidPlayerRoad = true;
            Vector3 flatForward = Vector3.ProjectOnPlane(start.Tangent, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            Quaternion yawRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            _driverVehicleRoot.SetPositionAndRotation(start.Position, yawRotation);
            _xrWorldAnchorInitialized = true;
            Debug.Log($"RUNE_RUSH_XR_CAMERA_FIXED: hierarchy=Driver Vehicle Root > Cockpit Anchor > XROrigin > Camera Offset > HMD; road={start.Position:F2}; desktopEye={FirstPersonCameraPosition:F2}; cockpitLocal={XrSeatOriginOffset:F2}; inputCameraTranslation=false; yaw-only vehicle root.");
        }

        public void Impact(float strength)
        {
            if (!IsXrActive()) _impact = Mathf.Max(_impact, Mathf.Clamp01(strength));
        }

        private void BuildTrack()
        {
            CreateRibbon("Stone shoulders", 24f, -0.12f, _shoulder);
            CreateRibbon("Royal road", 18f, 0f, _road);
            CreateRoadMarkings();
            CreateGuardrails();
            CreateGate(0f, "Crown Start", true);
            CreateGate(_track.Length * 0.25f, "Milestone I", false);
            CreateGate(_track.Length * 0.5f, "Milestone II", false);
            CreateGate(_track.Length * 0.75f, "Milestone III", false);
        }

        private void BuildEnvironment()
        {
            if (_art != null && _art.Environment != null)
            {
                Instantiate(_art.Environment, _bindings.EnvironmentRoot);
                if (!_art.GenerateProceduralEnvironment || !_art.UseProceduralFallback) return;
            }

            if (_art != null && !_art.GenerateProceduralEnvironment) return;

            var random = new System.Random(0x48494748);
            Transform root = _bindings.EnvironmentRoot;
            for (int index = 0; index < 108; index++)
            {
                float distance = (float)random.NextDouble() * _track.Length;
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float lane = side * Mathf.Lerp(18f, 62f, (float)random.NextDouble());
                TrackSample sample = _track.Sample(distance, lane, -0.15f);
                CreateTree(root, sample.Position, Mathf.Lerp(0.75f, 1.5f, (float)random.NextDouble()), index % 3 == 0);
            }

            for (int index = 0; index < 24; index++)
            {
                float distance = index / 24f * _track.Length + 13f;
                float lane = (index % 2 == 0 ? -1f : 1f) * (38f + index % 4 * 7f);
                TrackSample sample = _track.Sample(distance, lane, 0f);
                CreateWatchTower(root, sample.Position, 0.8f + index % 3 * 0.14f);
            }

            for (int index = 0; index < 28; index++)
            {
                float angle = index / 28f * Mathf.PI * 2f;
                float radius = 285f + Mathf.Sin(index * 1.7f) * 26f;
                Vector3 position = new(Mathf.Cos(angle) * radius, -14f, Mathf.Sin(angle) * radius * 0.82f);
                GameObject peak = ProceduralAssetFactory.AddPrimitive(root, PrimitiveType.Cube, "Highcrag Peak",
                    position, new Vector3(44f, 70f + index % 5 * 14f, 44f), _mountain,
                    new Vector3(18f + index % 4 * 4f, angle * Mathf.Rad2Deg, 45f));
                peak.isStatic = true;
            }

            CreateCastle(root);
            CreateMoon(root);
            StaticBatchingUtility.Combine(root.gameObject);
        }

        private void BuildRaceObjects()
        {
            foreach (PickupState pickup in _simulation.Pickups)
            {
                GameObject visual = CloneSceneLayoutVisual(pickup.Id, pickup.Progress);
                if (visual == null && _sceneLayoutMarkers == null)
                {
                    visual = SpawnOrFallback(_art?.Pickup, ProceduralAssetFactory.CreatePickup, _bindings.RaceObjectRoot);
                }
                if (visual == null) continue;
                visual.name = pickup.Id;
                _pickupVisuals[pickup.Id] = visual;
            }

            foreach (ObstacleState obstacle in _simulation.Obstacles)
            {
                GameObject visual = CloneSceneLayoutVisual(obstacle.Id, obstacle.Progress);
                if (visual == null && _sceneLayoutMarkers == null)
                {
                    GameObject prefab = ObstaclePrefab(obstacle.Kind);
                    visual = SpawnOrFallback(prefab, () => ProceduralAssetFactory.CreateObstacle(obstacle.Kind), _bindings.RaceObjectRoot);
                }
                if (visual == null) continue;
                visual.name = obstacle.Id;
                _obstacleVisuals[obstacle.Id] = visual;
            }

            foreach (BoostPadState pad in _simulation.Pads)
            {
                GameObject visual = CloneSceneLayoutVisual(pad.Id, pad.Progress);
                if (visual == null && _sceneLayoutMarkers == null)
                {
                    GameObject prefab = pad.Kind == PadKind.Boost ? _art?.BoostPad : _art?.RepairPad;
                    visual = SpawnOrFallback(prefab, () => ProceduralAssetFactory.CreatePad(pad.Kind), _bindings.RaceObjectRoot);
                }
                if (visual == null) continue;
                visual.name = pad.Id;
                _padVisuals[pad.Id] = visual;
            }
        }

        private GameObject CloneSceneLayoutVisual(string id, float progress)
        {
            if (_sceneLayoutMarkers == null
                || !_sceneLayoutMarkers.TryGetValue(id, out RuneRushRaceLayoutMarker marker)
                || marker == null)
            {
                return null;
            }

            Vector3 worldScale = marker.transform.lossyScale;
            TrackSample sample = _track.Sample(progress);
            Quaternion trackRotation = Quaternion.LookRotation(sample.Tangent, Vector3.up);
            _sceneLayoutRotationOffsets[id] = Quaternion.Inverse(trackRotation) * marker.transform.rotation;

            GameObject visual = Instantiate(marker.gameObject);
            RuneRushRaceLayoutMarker clonedMarker = visual.GetComponent<RuneRushRaceLayoutMarker>();
            if (clonedMarker != null)
            {
                clonedMarker.enabled = false;
                Destroy(clonedMarker);
            }
            visual.transform.SetParent(_bindings.RaceObjectRoot, true);
            SetWorldScale(visual.transform, worldScale);
            visual.SetActive(true);
            return visual;
        }

        private void HideSceneLayoutSources()
        {
            if (_sceneLayoutMarkers == null) return;
            foreach (RuneRushRaceLayoutMarker marker in _sceneLayoutMarkers.Values)
            {
                if (marker == null || _sourceMarkerActiveStates.ContainsKey(marker)) continue;
                _sourceMarkerActiveStates[marker] = marker.gameObject.activeSelf;
                marker.gameObject.SetActive(false);
            }
        }

        private void RestoreSceneLayoutSources()
        {
            foreach (KeyValuePair<RuneRushRaceLayoutMarker, bool> pair in _sourceMarkerActiveStates)
            {
                if (pair.Key != null) pair.Key.gameObject.SetActive(pair.Value);
            }
            _sourceMarkerActiveStates.Clear();
        }

        private static void SetWorldScale(Transform target, Vector3 worldScale)
        {
            Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(
                DivideScale(worldScale.x, parentScale.x),
                DivideScale(worldScale.y, parentScale.y),
                DivideScale(worldScale.z, parentScale.z));
        }

        private static float DivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.000001f ? value / divisor : value;
        }

        private void ValidateSceneLayoutVisuals()
        {
            if (_sceneLayoutMarkers == null) return;

            int validCount = 0;
            foreach (KeyValuePair<string, RuneRushRaceLayoutMarker> pair in _sceneLayoutMarkers)
            {
                GameObject visual = FindRaceObjectVisual(pair.Key);
                RuneRushRaceLayoutMarker source = pair.Value;
                if (visual == null || source == null)
                {
                    Debug.LogError($"RUNE_RUSH_SCENE_LAYOUT_RUNTIME_FAILED: missing runtime visual for {pair.Key}.");
                    continue;
                }

                bool hierarchyMatches = RendererHierarchyMatches(source.transform, visual.transform);
                bool transformHierarchyMatches = TransformHierarchyMatches(source.transform, visual.transform);
                bool scaleMatches = Approximately(source.transform.lossyScale, visual.transform.lossyScale);
                bool rotationMatches = Approximately(source.transform.rotation, visual.transform.rotation);
                bool remainsUpright = Vector3.Dot(source.transform.up, Vector3.up) > 0.95f
                    && Vector3.Dot(visual.transform.up, Vector3.up) > 0.95f;
                if (!hierarchyMatches || !transformHierarchyMatches || !scaleMatches || !rotationMatches || !remainsUpright)
                {
                    Debug.LogError(
                        $"RUNE_RUSH_SCENE_LAYOUT_RUNTIME_FAILED: {pair.Key} did not preserve its model hierarchy, "
                        + "world scale, world rotation or Y-up orientation.");
                    continue;
                }

                validCount += 1;
            }

            if (validCount == _sceneLayoutMarkers.Count)
            {
                Debug.Log(
                    $"RUNE_RUSH_SCENE_LAYOUT_RUNTIME_OK: {_pickupVisuals.Count} pickups, "
                    + $"{_obstacleVisuals.Count} obstacles and {_padVisuals.Count} pads preserve the current Scene model hierarchy, "
                    + "scale, rotation and Y-up orientation.");
            }
        }

        private void ValidateRacerVisualOrientation()
        {
            int validCount = 0;
            foreach (KeyValuePair<string, RacerVisual> pair in _racerVisuals)
            {
                Transform root = pair.Value.Root != null ? pair.Value.Root.transform : null;
                Transform model = root != null ? root.Find("VisualSlot/Model") : null;
                bool upright = root != null && Vector3.Dot(root.up, Vector3.up) > 0.95f;
                bool preservesAxisConversion = model != null
                    && Quaternion.Angle(model.localRotation, Quaternion.identity) > 1f;
                if (!upright || !preservesAxisConversion)
                {
                    Debug.LogError(
                        $"RUNE_RUSH_RACER_ORIENTATION_FAILED: {pair.Key} has no active FBX model or is not Y-up.");
                    continue;
                }

                validCount += 1;
            }

            if (validCount == _racerVisuals.Count)
            {
                Debug.Log(
                    $"RUNE_RUSH_RACER_ORIENTATION_OK: {validCount} racer models preserve their FBX axis conversion and remain Y-up.");
            }
        }

        private GameObject FindRaceObjectVisual(string id)
        {
            if (_pickupVisuals.TryGetValue(id, out GameObject pickup)) return pickup;
            if (_obstacleVisuals.TryGetValue(id, out GameObject obstacle)) return obstacle;
            if (_padVisuals.TryGetValue(id, out GameObject pad)) return pad;
            return null;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.0001f
                && Mathf.Abs(left.y - right.y) < 0.0001f
                && Mathf.Abs(left.z - right.z) < 0.0001f;
        }

        private static bool RendererHierarchyMatches(Transform sourceRoot, Transform visualRoot)
        {
            Renderer[] sourceRenderers = sourceRoot.GetComponentsInChildren<Renderer>(false);
            Renderer[] visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(false);
            if (sourceRenderers.Length != visualRenderers.Length) return false;

            for (int index = 0; index < sourceRenderers.Length; index += 1)
            {
                Renderer source = sourceRenderers[index];
                Renderer visual = visualRenderers[index];
                if (source.GetType() != visual.GetType()
                    || source.enabled != visual.enabled
                    || HierarchyPath(sourceRoot, source.transform) != HierarchyPath(visualRoot, visual.transform))
                {
                    return false;
                }

                if (source.transform != sourceRoot
                    && (!Approximately(source.transform.localPosition, visual.transform.localPosition)
                        || !Approximately(source.transform.localScale, visual.transform.localScale)
                        || !Approximately(source.transform.localRotation, visual.transform.localRotation)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TransformHierarchyMatches(Transform sourceRoot, Transform visualRoot)
        {
            if (sourceRoot.childCount != visualRoot.childCount) return false;
            for (int index = 0; index < sourceRoot.childCount; index += 1)
            {
                Transform source = sourceRoot.GetChild(index);
                Transform visual = visualRoot.GetChild(index);
                if (source.name != visual.name
                    || !Approximately(source.localPosition, visual.localPosition)
                    || !Approximately(source.localScale, visual.localScale)
                    || !Approximately(source.localRotation, visual.localRotation)
                    || !TransformHierarchyMatches(source, visual))
                {
                    return false;
                }
            }

            return true;
        }

        private static string HierarchyPath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;
            var segments = new List<int>();
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static bool Approximately(Quaternion left, Quaternion right)
        {
            return Mathf.Abs(Quaternion.Dot(left, right)) > 0.99999f;
        }

        private void BuildRacers()
        {
            int vehicleIndex = 0;
            CreateRacer(_simulation.Player, true, vehicleIndex++);
            foreach (RacerState racer in _simulation.Racers) CreateRacer(racer, false, vehicleIndex++);
        }

        private void BuildCockpit()
        {
            Transform cockpitAnchor = IsPicoXrRuntime() ? _cockpitAnchor : GetStableXrAnchor();
            Transform existing = cockpitAnchor != null ? cockpitAnchor.Find("Cockpit Visual Slot") : null;
            if (existing != null) DestroyImmediate(existing.gameObject);
            if (_art == null || _art.Cockpit == null) return;

            _cockpit = Instantiate(_art.Cockpit);
            _cockpit.name = "Cockpit Visual Slot";
            _cockpit.transform.SetParent(cockpitAnchor, false);
            _cockpit.transform.localPosition = CockpitPosition;
            _cockpit.transform.localRotation = Quaternion.identity;
            _cockpit.transform.localScale = CockpitScale;
            _cockpit.SetActive(_simulation.Phase != RacePhase.Menu);
        }

        private void EnsureXrDrivingHierarchy()
        {
            if (!IsPicoXrRuntime() || _cameraRig == null) return;

            if (_driverVehicleRoot == null)
            {
                GameObject root = GameObject.Find("Driver Vehicle Root");
                if (root == null) root = new GameObject("Driver Vehicle Root");
                _driverVehicleRoot = root.transform;
                _driverVehicleRoot.SetParent(transform, true);
            }

            if (_cockpitAnchor == null)
            {
                Transform existing = _driverVehicleRoot.Find("Cockpit Anchor");
                if (existing == null)
                {
                    var anchor = new GameObject("Cockpit Anchor");
                    anchor.transform.SetParent(_driverVehicleRoot, false);
                    existing = anchor.transform;
                }
                _cockpitAnchor = existing;
                _cockpitAnchor.localPosition = XrSeatOriginOffset;
                _cockpitAnchor.localRotation = Quaternion.identity;
            }

            if (_cameraRig.parent != _cockpitAnchor)
            {
                // The XR origin begins exactly at the fixed seat anchor.  Its HMD
                // child still receives the runtime local Floor pose; no gameplay
                // input subsequently writes either local position.
                _cameraRig.SetParent(_cockpitAnchor, false);
            }

            if (!_xrHierarchyReported)
            {
                _xrHierarchyReported = true;
                Transform cameraOffset = _camera != null ? _camera.transform.parent : null;
                Debug.Log($"RUNE_RUSH_XR_HIERARCHY: Driver Vehicle Root(world) > Cockpit Anchor(local={_cockpitAnchor.localPosition:F2}) > XROrigin(local={_cameraRig.localPosition:F2}) > Camera Offset(local={(cameraOffset != null ? cameraOffset.localPosition : Vector3.zero):F2}) > HMD(local={(_camera != null ? _camera.transform.localPosition : Vector3.zero):F2}); motionAuthority=Driver Vehicle Root TrackSample XYZ+yaw only; inputCameraTranslation=false.");
            }
        }

        private void UpdateRacers(RaceInputFrame input, float dt)
        {
            UpdateRacer(_simulation.Player, true, input.Steer, dt);
            foreach (RacerState racer in _simulation.Racers) UpdateRacer(racer, false, 0f, dt);
        }

        private void UpdateRacer(RacerState racer, bool player, float steer, float dt)
        {
            if (racer == null || !_racerVisuals.TryGetValue(racer.Id, out RacerVisual visual)) return;
            TrackSample sample = GetSafeRoadSample(racer.Progress, racer.Lane, 0.28f, player);
            visual.Root.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Tangent, Vector3.up));
            Transform model = visual.Root.transform.Find("VisualSlot");
            if (model != null)
            {
                float targetRoll = -steer * 5.5f;
                Quaternion baseRotation = _racerModelRotations.TryGetValue(racer.Id, out Quaternion savedRotation)
                    ? savedRotation
                    : Quaternion.identity;
                Quaternion targetRotation = baseRotation * Quaternion.Euler(0f, 0f, targetRoll);
                model.localRotation = Quaternion.Slerp(model.localRotation, targetRotation, 1f - Mathf.Exp(-7f * dt));
            }

            bool shield = racer.ShieldUntil > _simulation.GameTime;
            visual.Shield.SetActive(shield);
            visual.Shield.transform.Rotate(Vector3.up, 62f * dt, Space.Self);
            bool boosting = player && (_simulation.Player.BoostUntil > _simulation.GameTime || _simulation.Player.DriftBoostUntil > _simulation.GameTime);
            visual.Tail.SetActive(boosting);
            if (visual.Wheels != null)
            {
                float spin = racer.Speed * dt * 38f;
                foreach (Transform wheel in visual.Wheels) wheel.Rotate(Vector3.up, spin, Space.Self);
            }
        }

        private void UpdateRaceObjects(float dt)
        {
            foreach (PickupState pickup in _simulation.Pickups)
            {
                if (!_pickupVisuals.TryGetValue(pickup.Id, out GameObject visual)) continue;
                visual.SetActive(pickup.Active);
                if (!pickup.Active) continue;
                PlaceOnTrack(visual.transform, pickup.Progress, pickup.Lane, 1.2f, pickup.Id);
                visual.transform.Rotate(Vector3.up, 80f * dt, Space.Self);
            }

            foreach (ObstacleState obstacle in _simulation.Obstacles)
            {
                if (!_obstacleVisuals.TryGetValue(obstacle.Id, out GameObject visual)) continue;
                float lane = _simulation.GetObstacleLane(obstacle);
                PlaceOnTrack(visual.transform, obstacle.Progress, lane, 0f, obstacle.Id);
                Transform motion = visual.transform.Find("MotionRoot");
                if (motion != null && obstacle.Kind == ObstacleKind.Spinner)
                {
                    motion.Rotate(Vector3.up, 145f * dt, Space.Self);
                }
            }

            foreach (BoostPadState pad in _simulation.Pads)
            {
                if (_padVisuals.TryGetValue(pad.Id, out GameObject visual)) PlaceOnTrack(visual.transform, pad.Progress, pad.Lane, 0.03f, pad.Id);
            }
        }

        private void UpdateMines(float dt)
        {
            var activeIds = new HashSet<string>();
            foreach (MineState mine in _simulation.Mines)
            {
                if (!mine.Active) continue;
                activeIds.Add(mine.Id);
                if (!_mineVisuals.TryGetValue(mine.Id, out GameObject visual))
                {
                    visual = SpawnOrFallback(_art?.Mine, ProceduralAssetFactory.CreateMine, _bindings.RaceObjectRoot);
                    visual.name = mine.Id;
                    _mineVisuals[mine.Id] = visual;
                }
                PlaceOnTrack(visual.transform, mine.Progress, mine.Lane, 0.6f);
                visual.transform.Rotate(Vector3.up, 115f * dt, Space.Self);
            }

            var stale = new List<string>();
            foreach (KeyValuePair<string, GameObject> pair in _mineVisuals)
            {
                if (!activeIds.Contains(pair.Key)) stale.Add(pair.Key);
            }
            foreach (string id in stale)
            {
                if (_mineVisuals[id] != null) Destroy(_mineVisuals[id]);
                _mineVisuals.Remove(id);
            }
        }

        private void CreateRacer(RacerState racer, bool player, int vehicleIndex)
        {
            Color color = ColorFromInt(racer.Color);
            GameObject prefab = RacerPrefab(player, vehicleIndex);
            GameObject root = SpawnOrFallback(prefab, () => ProceduralAssetFactory.CreateVehicle(racer.Name, color, player), _bindings.RacerRoot);
            root.name = racer.Id;
            Transform visual = root.transform.Find("VisualSlot");
            if (visual != null) _racerModelRotations[racer.Id] = visual.localRotation;
            if (player)
            {
                _playerVisual = visual;
                if (_playerVisual != null) _playerVisual.gameObject.SetActive(false);
            }

            Transform shieldAnchor = EnsureAnchor(root.transform, "ShieldVfxAnchor", new Vector3(0f, 1f, 0f));
            GameObject shield = ProceduralAssetFactory.AddPrimitive(shieldAnchor, PrimitiveType.Cylinder, "Shield Rune",
                Vector3.zero, new Vector3(3.25f, 0.045f, 3.25f), _rune);
            shield.SetActive(false);
            Transform tailAnchor = EnsureAnchor(root.transform, "TailVfxAnchor", new Vector3(0f, 0.55f, -2.4f));
            GameObject tail = ProceduralAssetFactory.AddPrimitive(tailAnchor, PrimitiveType.Cube, "Wind Trail",
                new Vector3(0f, 0f, -1.6f), new Vector3(0.38f, 0.18f, 3.2f), _rune);
            tail.SetActive(false);

            var wheels = new List<Transform>();
            if (visual != null)
            {
                foreach (Transform child in visual)
                {
                    if (child.name.StartsWith("Wheel_", StringComparison.Ordinal)) wheels.Add(child);
                }
            }

            _racerVisuals[racer.Id] = new RacerVisual(root, shield, tail, wheels);
        }

        private GameObject RacerPrefab(bool player, int vehicleIndex)
        {
            GameObject[] vehicles = _art != null ? _art.RacerVehicles : null;
            if (vehicles != null && vehicles.Length > 0)
            {
                for (int offset = 0; offset < vehicles.Length; offset += 1)
                {
                    GameObject candidate = vehicles[(vehicleIndex + offset) % vehicles.Length];
                    if (candidate != null) return candidate;
                }
            }

            return player ? _art?.PlayerVehicle : _art?.AiVehicle;
        }

        private void CreateCamera()
        {
            Camera existing = Camera.main;
            if (existing != null)
            {
                _camera = existing;
                _cameraRig = FindCameraRig(existing.transform);
                ConfigureCamera(existing);
                RefreshCameraMode();
                return;
            }

            var rig = new GameObject("DriverRig");
            rig.transform.SetParent(transform, false);
            _cameraRig = rig.transform;
            var cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(_cameraRig, false);
            _camera = cameraObject.AddComponent<Camera>();
            ConfigureCamera(_camera);
            RefreshCameraMode();
        }

        private bool RefreshCameraMode()
        {
            bool xrActive = XRRuntimeState.IsPicoXrPresentation(_camera);
            if (_camera != null && _trackedPoseDriver == null)
            {
                _trackedPoseDriver = _camera.GetComponent("TrackedPoseDriver") as Behaviour;
            }
            if (_trackedPoseDriver != null && _trackedPoseDriver.enabled != xrActive)
            {
                _trackedPoseDriver.enabled = xrActive;
            }
            bool showCockpit = _simulation != null && _simulation.Phase != RacePhase.Menu;
            if (_cockpit != null && _cockpit.activeSelf != showCockpit)
            {
                _cockpit.SetActive(showCockpit);
            }
            if (_playerVisual != null && _playerVisual.gameObject.activeSelf)
            {
                _playerVisual.gameObject.SetActive(false);
            }
            return xrActive;
        }

        private static Transform FindCameraRig(Transform cameraTransform)
        {
            Transform current = cameraTransform;
            while (current != null)
            {
                if (current.GetComponent("XROrigin") != null)
                {
                    return current;
                }

                current = current.parent;
            }

            return cameraTransform.parent != null ? cameraTransform.parent : cameraTransform;
        }

        private Transform GetStableXrAnchor()
        {
            return _camera != null && _camera.transform.parent != null
                ? _camera.transform.parent
                : _cameraRig;
        }

        private static void ConfigureCamera(Camera target)
        {
            target.enabled = true;
            target.gameObject.SetActive(true);
            target.gameObject.tag = "MainCamera";
            target.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            target.backgroundColor = new Color(0.035f, 0.065f, 0.085f);
            target.nearClipPlane = 0.05f;
            target.farClipPlane = 690f;
            target.fieldOfView = 72f;
            if (target.GetComponent<AudioListener>() == null)
            {
                target.gameObject.AddComponent<AudioListener>();
            }
        }

        private void ConfigureWorldLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.26f, 0.3f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.16f, 0.14f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.055f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.07f, 0.12f, 0.12f);
            RenderSettings.fogStartDistance = 125f;
            RenderSettings.fogEndDistance = 545f;

            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.gameObject.SetActive(false);
            var sun = new GameObject("Moonlit Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.72f, 0.82f, 0.92f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, -42f, 0f);
        }

        private void CreateMaterials()
        {
            _road = _art != null && _art.RoadMaterial != null ? _art.RoadMaterial : ProceduralAssetFactory.Material("Rune road", new Color(0.12f, 0.145f, 0.145f), 0.05f, 0.38f);
            _shoulder = _art != null && _art.ShoulderMaterial != null ? _art.ShoulderMaterial : ProceduralAssetFactory.Material("Stone shoulder", new Color(0.27f, 0.28f, 0.26f), 0.03f, 0.2f);
            _stone = ProceduralAssetFactory.Material("Castle stone", new Color(0.34f, 0.36f, 0.35f), 0.03f, 0.2f);
            _iron = ProceduralAssetFactory.Material("Mithril iron", new Color(0.14f, 0.17f, 0.18f), 0.76f, 0.4f);
            _wood = ProceduralAssetFactory.Material("Oak", new Color(0.19f, 0.085f, 0.035f), 0.02f, 0.2f);
            _foliageDark = ProceduralAssetFactory.Material("Fir dark", new Color(0.045f, 0.18f, 0.105f), 0f, 0.18f);
            _foliageLight = ProceduralAssetFactory.Material("Fir light", new Color(0.08f, 0.29f, 0.15f), 0f, 0.2f);
            _mountain = ProceduralAssetFactory.Material("Mountain", new Color(0.18f, 0.22f, 0.23f), 0f, 0.13f);
            _gold = ProceduralAssetFactory.Material("Royal gold", new Color(0.82f, 0.57f, 0.18f), 0.68f, 0.5f);
            _rune = _art != null && _art.RuneMaterial != null ? _art.RuneMaterial : ProceduralAssetFactory.Material("Mithril rune", new Color(0.19f, 0.71f, 0.92f), 0.12f, 0.82f, true);
        }

        private void CreateRibbon(string name, float width, float yOffset, Material material)
        {
            const int segments = 720;
            var vertices = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            float half = width * 0.5f;
            for (int index = 0; index <= segments; index++)
            {
                float distance = index / (float)segments * _track.Length;
                TrackSample sample = _track.Sample(distance);
                int vertex = index * 2;
                vertices[vertex] = sample.Position - sample.Right * half + Vector3.up * yOffset;
                vertices[vertex + 1] = sample.Position + sample.Right * half + Vector3.up * yOffset;
                normals[vertex] = Vector3.up;
                normals[vertex + 1] = Vector3.up;
                uvs[vertex] = new Vector2(0f, distance / 9f);
                uvs[vertex + 1] = new Vector2(1f, distance / 9f);
                if (index == segments) continue;
                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }

            var mesh = new Mesh { name = name + " Mesh" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_bindings.EnvironmentRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            go.isStatic = true;
        }

        private void CreateRoadMarkings()
        {
            for (float distance = 0f; distance < _track.Length; distance += 18f)
            {
                TrackSample sample = _track.Sample(distance, 0f, 0.055f);
                GameObject mark = ProceduralAssetFactory.AddPrimitive(_bindings.EnvironmentRoot, PrimitiveType.Cube, "Mithril Lane Rune",
                    sample.Position, new Vector3(0.16f, 0.025f, 6.5f), _rune);
                mark.transform.rotation = Quaternion.LookRotation(sample.Tangent, Vector3.up);
                mark.isStatic = true;
            }
        }

        private void CreateGuardrails()
        {
            for (float distance = 0f; distance < _track.Length; distance += 12f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    TrackSample sample = _track.Sample(distance, side * 10.3f, 0.72f);
                    GameObject rail = ProceduralAssetFactory.AddPrimitive(_bindings.EnvironmentRoot, PrimitiveType.Cube, "Guardrail",
                        sample.Position, new Vector3(0.24f, 0.58f, 11.8f), _iron);
                    rail.transform.rotation = Quaternion.LookRotation(sample.Tangent, Vector3.up);
                    rail.isStatic = true;
                }
            }
        }

        private void CreateGate(float distance, string gateName, bool royal)
        {
            TrackSample sample = _track.Sample(distance, 0f, 0f);
            var root = new GameObject(gateName);
            root.transform.SetParent(_bindings.EnvironmentRoot, false);
            root.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Tangent, Vector3.up));
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cube, "Left pier", new Vector3(-11f, 3.4f, 0f), new Vector3(1.7f, 6.8f, 1.7f), _stone);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cube, "Right pier", new Vector3(11f, 3.4f, 0f), new Vector3(1.7f, 6.8f, 1.7f), _stone);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cube, "Arch", new Vector3(0f, 6.2f, 0f), new Vector3(23f, 1.05f, 1.5f), royal ? _gold : _iron);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cylinder, "Rune seal", new Vector3(0f, 6.1f, -0.85f), new Vector3(1.2f, 0.09f, 1.2f), _rune, new Vector3(90f, 0f, 0f));
            root.isStatic = true;
        }

        private void CreateTree(Transform parent, Vector3 position, float scale, bool lighter)
        {
            var root = new GameObject("Highcrag Fir");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2.4f, 0f), new Vector3(0.45f, 2.4f, 0.45f), _wood);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Sphere, "Crown low", new Vector3(0f, 5.2f, 0f), new Vector3(3.2f, 4.4f, 3.2f), lighter ? _foliageLight : _foliageDark);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Sphere, "Crown high", new Vector3(0f, 8.1f, 0f), new Vector3(2.15f, 3.35f, 2.15f), lighter ? _foliageLight : _foliageDark);
            root.isStatic = true;
        }

        private void CreateWatchTower(Transform parent, Vector3 position, float scale)
        {
            var root = new GameObject("Kingdom Watchtower");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cylinder, "Tower", new Vector3(0f, 6.2f, 0f), new Vector3(3.8f, 6.2f, 3.8f), _stone);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cylinder, "Battlement", new Vector3(0f, 12.5f, 0f), new Vector3(4.8f, 0.9f, 4.8f), _iron);
            ProceduralAssetFactory.AddPrimitive(root.transform, PrimitiveType.Cylinder, "Beacon", new Vector3(0f, 14.1f, 0f), new Vector3(0.55f, 0.7f, 0.55f), _gold);
            root.isStatic = true;
        }

        private void CreateCastle(Transform parent)
        {
            var castle = new GameObject("Highcrag Crown Keep");
            castle.transform.SetParent(parent, false);
            castle.transform.position = new Vector3(0f, 3f, 0f);
            ProceduralAssetFactory.AddPrimitive(castle.transform, PrimitiveType.Cube, "Keep", new Vector3(0f, 16f, 0f), new Vector3(28f, 32f, 24f), _stone);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    ProceduralAssetFactory.AddPrimitive(castle.transform, PrimitiveType.Cylinder, "Crown tower", new Vector3(x * 18f, 15f, z * 15f), new Vector3(7f, 15f, 7f), _stone);
                    ProceduralAssetFactory.AddPrimitive(castle.transform, PrimitiveType.Cylinder, "Tower crown", new Vector3(x * 18f, 31f, z * 15f), new Vector3(8.3f, 1.2f, 8.3f), _gold);
                }
            }
            castle.isStatic = true;
        }

        private void CreateMoon(Transform parent)
        {
            GameObject moon = ProceduralAssetFactory.AddPrimitive(parent, PrimitiveType.Sphere, "Silver Moon",
                new Vector3(-245f, 190f, -330f), Vector3.one * 38f,
                ProceduralAssetFactory.UnlitMaterial("Moon", new Color(0.82f, 0.9f, 0.96f)));
            moon.isStatic = true;
        }

        private void PlaceOnTrack(Transform target, float progress, float lane, float height, string layoutId = null)
        {
            TrackSample sample = GetSafeRoadSample(progress, lane, height, false);
            Quaternion rotation = Quaternion.LookRotation(sample.Tangent, Vector3.up);
            if (layoutId != null && _sceneLayoutRotationOffsets.TryGetValue(layoutId, out Quaternion offset))
            {
                rotation *= offset;
            }
            target.SetPositionAndRotation(sample.Position, rotation);
        }

        private TrackSample GetSafeRoadSample(float progress, float lane, float height, bool player)
        {
            TrackSample sample = _track.Sample(progress, lane, height);
            bool finite = float.IsFinite(sample.Position.x)
                && float.IsFinite(sample.Position.y)
                && float.IsFinite(sample.Position.z)
                && sample.Position.y > -25f
                && sample.Position.y < 25f;
            if (finite)
            {
                if (player)
                {
                    _lastValidPlayerRoad = sample;
                    _hasLastValidPlayerRoad = true;
                    if (IsPicoXrRuntime() && !_xrGroundSafetyReported)
                    {
                        _xrGroundSafetyReported = true;
                        Debug.Log($"RUNE_RUSH_XR_GROUND_SAFE: roadY={sample.Position.y:F2}; model=TrackSample XYZ origin/vehicle follow + yaw-only origin rotation.");
                    }
                }
                return sample;
            }

            if (player && _hasLastValidPlayerRoad)
            {
                Debug.LogWarning("RUNE_RUSH_XR_GROUND_RECOVERY: invalid road sample; restoring the last valid track pose.");
                return _lastValidPlayerRoad;
            }

            return _track.Sample(0f, 0f, height);
        }

        private GameObject ObstaclePrefab(ObstacleKind kind)
        {
            if (_art == null) return null;
            return kind switch
            {
                ObstacleKind.Crate => _art.Crate,
                ObstacleKind.Barricade => _art.Barricade,
                ObstacleKind.Oil => _art.Oil,
                ObstacleKind.Spinner => _art.Spinner,
                ObstacleKind.Gate => _art.Gate,
                _ => null
            };
        }

        private static GameObject SpawnOrFallback(GameObject prefab, Func<GameObject> fallback, Transform parent)
        {
            GameObject instance = prefab != null ? Instantiate(prefab, parent) : fallback();
            if (instance.transform.parent != parent) instance.transform.SetParent(parent, false);
            return instance;
        }

        private static Transform EnsureAnchor(Transform root, string name, Vector3 fallbackPosition)
        {
            Transform anchor = root.Find(name);
            if (anchor != null) return anchor;
            var created = new GameObject(name);
            created.transform.SetParent(root, false);
            created.transform.localPosition = fallbackPosition;
            return created.transform;
        }

        private static Color ColorFromInt(int packed)
        {
            float r = ((packed >> 16) & 0xFF) / 255f;
            float g = ((packed >> 8) & 0xFF) / 255f;
            float b = (packed & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }

        private static bool IsXrActive()
        {
            return XRRuntimeState.IsHeadsetActive;
        }

        private bool IsPicoXrRuntime()
        {
            return XRRuntimeState.IsPicoXrPresentation(_camera);
        }

        private sealed class RacerVisual
        {
            public readonly GameObject Root;
            public readonly GameObject Shield;
            public readonly GameObject Tail;
            public readonly List<Transform> Wheels;

            public RacerVisual(GameObject root, GameObject shield, GameObject tail, List<Transform> wheels)
            {
                Root = root;
                Shield = shield;
                Tail = tail;
                Wheels = wheels;
            }
        }
    }
}
