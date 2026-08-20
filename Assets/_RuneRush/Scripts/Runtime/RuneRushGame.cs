using System;
using System.Collections.Generic;
using RuneRush;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuneRush.UnityRuntime
{
    [DefaultExecutionOrder(-500)]
    public sealed class RuneRushGame : MonoBehaviour
    {
        private const string RaceSceneName = "RuneRushRace";
        private const string RaceScenePath = "Assets/_RuneRush/Scenes/RuneRushRace.unity";
        [NonSerialized] private TrackPathData _track;
        [NonSerialized] private RaceSimulation _simulation;
        [NonSerialized] private RaceInputSource _input;
        [NonSerialized] private RaceScenePresenter _presenter;
        [NonSerialized] private RaceHudView _hud;
        [NonSerialized] private RaceAudioController _audio;
        [NonSerialized] private RaceLayout _runtimeLayout;
        [NonSerialized] private Dictionary<string, RuneRushRaceLayoutMarker> _runtimeLayoutMarkers;
        [NonSerialized] private RaceInputFrame _frame;
        [NonSerialized] private RacePhase _lastPhase;
        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _initializing;

        public RaceSimulation Simulation => _simulation;
        public TrackPathData Track => _track;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<RuneRushGame>() != null) return;
            Scene active = SceneManager.GetActiveScene();
            if (!active.isLoaded) return;
            if (FindAnyObjectByType<RuneRushSceneBindings>() == null)
            {
#if UNITY_EDITOR
                // An unsaved Untitled scene is often the user's protected authoring
                // workspace. Load the playable scene only for this Play session; Unity
                // restores the editor setup when Play mode ends and no scene is saved.
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    RaceScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                if (Application.CanStreamedLevelBeLoaded(RaceSceneName))
                {
                    SceneManager.LoadScene(RaceSceneName, LoadSceneMode.Single);
                }
                else
                {
                    Debug.LogError("RUNE_RUSH_BOOTSTRAP_FAILED: the playable RuneRushRace scene is not available.");
                }
#endif
                return;
            }
            var root = new GameObject("RuneRushGame");
            root.AddComponent<RuneRushGame>();
        }

        private void Awake()
        {
            if (FindObjectsByType<RuneRushGame>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            Time.fixedDeltaTime = Application.isMobilePlatform ? 1f / 90f : RaceSimulation.FixedStep;
            Time.maximumDeltaTime = 0.1f;
            Application.targetFrameRate = Application.isMobilePlatform ? 90 : 120;
            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (Application.isPlaying) EnsureInitialized();
        }

        private void Update()
        {
            if (!EnsureInitialized()) return;
            RaceInputFrame nextFrame = _input.ReadFrame();
            nextFrame.UseItem |= _frame.UseItem;
            nextFrame.Reset |= _frame.Reset;
            _frame = nextFrame;
            if (_input.PausePressedThisFrame)
            {
                TogglePause();
            }

            if (_simulation.Phase == RacePhase.Menu)
            {
                if (_input.MenuLeftPressedThisFrame) _hud.CycleDifficulty(-1);
                if (_input.MenuRightPressedThisFrame) _hud.CycleDifficulty(1);
                if (_input.MenuPrimaryPressedThisFrame) _hud.ConfirmStart();
            }
            else if (_simulation.Phase == RacePhase.Paused)
            {
                if (_input.MenuPrimaryPressedThisFrame) ResumeRace();
                if (_input.MenuSecondaryPressedThisFrame) RestartRace();
            }
            else if (_simulation.Phase == RacePhase.Finished)
            {
                if (_input.MenuPrimaryPressedThisFrame) RestartRace();
                if (_input.MenuSecondaryPressedThisFrame) ReturnToMenu();
            }
        }

        private void FixedUpdate()
        {
            if (!EnsureInitialized()) return;
            _simulation.Step(Time.fixedDeltaTime, _frame);
            _frame.UseItem = false;
            _frame.Reset = false;
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized()) return;
            RaceEvent[] events = _simulation.DrainEvents();
            if (events.Length > 0)
            {
                _hud.PushEvents(events);
                foreach (RaceEvent raceEvent in events)
                {
                    _audio.PlayEvent(raceEvent);
                    if (raceEvent.Kind == RaceEventKind.Danger) _presenter.Impact(raceEvent.Code == RaceEventCode.MineHitPlayer ? 1f : 0.72f);
                }
            }

            _presenter.Sync(_frame, Time.unscaledDeltaTime);
            RaceSnapshot snapshot = _simulation.CreateSnapshot();
            _hud.UpdateView(snapshot);
            _audio.UpdateEngine(_simulation.Player.Speed, _frame.Throttle, _frame.Nitro);

            if (_simulation.Phase == RacePhase.Finished && _lastPhase != RacePhase.Finished)
            {
                SaveRecords();
            }
            _lastPhase = _simulation.Phase;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && HasValidRuntime() && (_simulation.Phase == RacePhase.Racing || _simulation.Phase == RacePhase.Countdown))
            {
                PauseRace();
            }
        }

        private void OnDestroy()
        {
            UnbindHudEvents();
        }

        private bool EnsureInitialized()
        {
            if (HasValidRuntime()) return true;
            if (_initializing || !Application.isPlaying) return false;

            _initializing = true;
            _initialized = false;
            try
            {
                InitializeGame();
            }
            finally
            {
                _initializing = false;
            }

            return HasValidRuntime();
        }

        private bool HasValidRuntime()
        {
            return _initialized
                && _track != null
                && _simulation != null
                && _input != null
                && _presenter != null
                && _hud != null
                && _audio != null;
        }

        private void InitializeGame()
        {
            RuneRushTrackAuthoring authoredTrack = FindAnyObjectByType<RuneRushTrackAuthoring>();
            _track = authoredTrack != null ? authoredTrack.CreatePath() : TrackPathData.CreateHighcrag();
            RuneRushSceneBindings bindings = FindAnyObjectByType<RuneRushSceneBindings>();
            if (bindings == null)
            {
                var sceneRoot = new GameObject("RuneRush Scene Bindings");
                bindings = sceneRoot.AddComponent<RuneRushSceneBindings>();
            }
            bindings.EnsureRoots();
            _runtimeLayout = CreateRuntimeLayout(bindings, _track, out _runtimeLayoutMarkers);
            _simulation = new RaceSimulation(_track.Length, _runtimeLayout);

            _input = GetComponent<RaceInputSource>();
            if (_input == null) _input = gameObject.AddComponent<RaceInputSource>();
            _presenter = GetComponent<RaceScenePresenter>();
            if (_presenter == null) _presenter = gameObject.AddComponent<RaceScenePresenter>();
            _presenter.Initialize(_track, _simulation, bindings, _runtimeLayoutMarkers);
            _audio = GetComponent<RaceAudioController>();
            if (_audio == null) _audio = gameObject.AddComponent<RaceAudioController>();
            _audio.Initialize();
            _hud = GetComponent<RaceHudView>();
            if (_hud == null) _hud = gameObject.AddComponent<RaceHudView>();
            UnbindHudEvents();
            _hud.Initialize(_presenter.WorldCamera, _input);
            BindHudEvents();

            _frame = RaceInputFrame.Idle;
            _lastPhase = _simulation.Phase;
            _initialized = true;
            _hud.UpdateView(_simulation.CreateSnapshot());
        }

        private void BindHudEvents()
        {
            if (_hud == null) return;
            _hud.StartRequested += StartRace;
            _hud.PauseRequested += PauseRace;
            _hud.ResumeRequested += ResumeRace;
            _hud.RestartRequested += RestartRace;
            _hud.MenuRequested += ReturnToMenu;
        }

        private void UnbindHudEvents()
        {
            if (_hud == null) return;
            _hud.StartRequested -= StartRace;
            _hud.PauseRequested -= PauseRace;
            _hud.ResumeRequested -= ResumeRace;
            _hud.RestartRequested -= RestartRace;
            _hud.MenuRequested -= ReturnToMenu;
        }

        private void StartRace(Difficulty difficulty)
        {
            if (!EnsureInitialized()) return;
            _simulation.Start(difficulty, false, _runtimeLayout);
            _audio.StartRaceAudio();
            _audio.SetPaused(false);
        }

        private static RaceLayout CreateRuntimeLayout(
            RuneRushSceneBindings bindings,
            TrackPathData track,
            out Dictionary<string, RuneRushRaceLayoutMarker> markerVisuals)
        {
            markerVisuals = null;
            if (bindings.LayoutMarkerRoot == null) return bindings.Layout;

            RuneRushRaceLayoutMarker[] markers =
                bindings.LayoutMarkerRoot.GetComponentsInChildren<RuneRushRaceLayoutMarker>(true);
            if (markers.Length == 0 && !UsesAuthoredLayout(bindings.Layout)) return bindings.Layout;

            markerVisuals = new Dictionary<string, RuneRushRaceLayoutMarker>(markers.Length);
            var layout = new RaceLayout
            {
                UseAuthoredPickups = true,
                UseAuthoredObstacles = true,
                UseAuthoredPads = true
            };

            // The saved marker transform is the final artist-authored placement. Project it
            // onto the track in memory so Play mode never depends on a separate sync command.
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex += 1)
            {
                RuneRushRaceLayoutMarker marker = markers[markerIndex];
                if (marker == null || !marker.gameObject.activeInHierarchy || !HasVisibleModel(marker)) continue;

                string id = "scene-marker-" + markerIndex;
                float progressRatio = marker.ProgressRatio;
                float lane = marker.Lane;
                if (track != null) ProjectOntoTrack(track, marker.transform.position, ref progressRatio, ref lane);
                markerVisuals[id] = marker;
                switch (marker.Kind)
                {
                    case RuneRushLayoutMarkerKind.Pickup:
                        layout.Pickups.Add(new RacePickupLayout
                        {
                            Id = id,
                            ProgressRatio = progressRatio,
                            Lane = lane
                        });
                        break;
                    case RuneRushLayoutMarkerKind.Obstacle:
                        layout.Obstacles.Add(new RaceObstacleLayout
                        {
                            Id = id,
                            Kind = marker.ObstacleKind,
                            ProgressRatio = progressRatio,
                            Lane = lane,
                            Phase = marker.Phase
                        });
                        break;
                    case RuneRushLayoutMarkerKind.Pad:
                        layout.Pads.Add(new RacePadLayout
                        {
                            Id = id,
                            Kind = marker.PadKind,
                            ProgressRatio = progressRatio,
                            Lane = lane
                        });
                        break;
                }
            }

            return layout;
        }

        private static bool UsesAuthoredLayout(RaceLayout layout)
        {
            return layout != null
                && (layout.UseAuthoredPickups || layout.UseAuthoredObstacles || layout.UseAuthoredPads);
        }

        private static void ProjectOntoTrack(
            TrackPathData track,
            Vector3 worldPosition,
            ref float progressRatio,
            ref float lane)
        {
            const int samples = 960;
            float bestDistance = float.MaxValue;
            float bestProgress = Mathf.Clamp01(progressRatio) * track.Length;
            float bestLane = lane;
            for (int index = 0; index < samples; index += 1)
            {
                float progress = index / (float)samples * track.Length;
                TrackSample sample = track.Sample(progress);
                Vector3 offset = worldPosition - sample.Position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestProgress = progress;
                bestLane = Mathf.Clamp(
                    Vector3.Dot(offset, sample.Right),
                    -RaceSimulation.RoadHalfWidth,
                    RaceSimulation.RoadHalfWidth);
            }

            progressRatio = Mathf.Repeat(bestProgress / track.Length, 1f);
            lane = bestLane;
        }

        private static bool HasVisibleModel(RuneRushRaceLayoutMarker marker)
        {
            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(false);
            for (int index = 0; index < renderers.Length; index += 1)
            {
                if (renderers[index] != null && renderers[index].enabled) return true;
            }
            return false;
        }

        private void RestartRace()
        {
            StartRace(_simulation.Difficulty);
        }

        private void ReturnToMenu()
        {
            _simulation.ReturnToMenu();
            _audio.SetPaused(true);
        }

        private void TogglePause()
        {
            if (_simulation.Phase == RacePhase.Paused) ResumeRace();
            else PauseRace();
        }

        private void PauseRace()
        {
            if (_simulation.Phase != RacePhase.Racing && _simulation.Phase != RacePhase.Countdown) return;
            _simulation.SetPaused(true);
            _audio.SetPaused(true);
        }

        private void ResumeRace()
        {
            if (_simulation.Phase != RacePhase.Paused) return;
            _simulation.SetPaused(false);
            _audio.SetPaused(false);
        }

        private void SaveRecords()
        {
            const string bestLapKey = "RuneRush.BestLap";
            const string highScoreKey = "RuneRush.HighScore";
            float existingLap = PlayerPrefs.GetFloat(bestLapKey, 0f);
            if (_simulation.Stats.BestLap > 0f && (existingLap <= 0f || _simulation.Stats.BestLap < existingLap))
            {
                PlayerPrefs.SetFloat(bestLapKey, _simulation.Stats.BestLap);
            }
            if (_simulation.Player.Score > PlayerPrefs.GetInt(highScoreKey, 0))
            {
                PlayerPrefs.SetInt(highScoreKey, _simulation.Player.Score);
            }
            PlayerPrefs.Save();
        }
    }
}
