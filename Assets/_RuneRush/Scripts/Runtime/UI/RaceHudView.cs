using System;
using System.Collections.Generic;
using RuneRush;
using RuneRush.XR;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RuneRush.UnityRuntime
{
    public sealed class RaceHudView : MonoBehaviour
    {
        private static readonly Color Ink = Hex("080C10");
        private static readonly Color Panel = Hex("121A22");
        private static readonly Color PanelSoft = Hex("1D2934");
        private static readonly Color Line = Hex("394957");
        private static readonly Color Stone = Hex("96A2AA");
        private static readonly Color Gold = Hex("E1A83B");
        private static readonly Color GoldLight = Hex("F4C45D");
        private static readonly Color Silver = Hex("EEF2F3");
        private static readonly Color RuneBlue = Hex("35B7D7");
        private static readonly Color Healing = Hex("49CE85");
        private static readonly Color DriftPurple = Hex("9258DA");
        private static readonly Color Crimson = Hex("E25A5A");
        private static readonly Color FigmaCard = Hex("12171E");
        private static readonly Color FigmaLine = Hex("3D4653");
        private static readonly Color FigmaText = Hex("E8DFC9");
        private static readonly Color FigmaInstrument = Hex("171319");
        private static readonly Color FigmaCopper = Hex("735A38");
        private static readonly Color FigmaPopup = Hex("0A1816");
        private static readonly Color FigmaGold = Hex("EAB23D");
        private static readonly Color FigmaOrange = Hex("E39B46");
        private static readonly Color FigmaGreen = Hex("7AE596");
        private static readonly Color FigmaGray = Hex("A8AAA7");
        private static readonly Color FigmaRed = Hex("D95A57");
        private static readonly Color FigmaBoostBackground = Hex("172B3B");
        private static readonly Color FigmaBoost = Hex("5DC8FF");
        private static readonly Color FigmaDamageBackground = Hex("2A1D21");
        private static readonly Color FigmaItemBackground = Hex("291F3A");
        private static readonly Color FigmaItem = Hex("B988FF");
        private static readonly Color FigmaLapBackground = Hex("28292B");
        private static readonly Color FigmaCountdown = Hex("1A222B");

        private Canvas _canvas;
        private RuneRushXrPointerModule _xrPointer;
        private CanvasScaler _canvasScaler;
        private Camera _worldCamera;
        private bool _isXrPresentation;
        private FullscreenMenuPresentation _menuPresentation;
        private Font _font;
        private GameObject _menu;
        private GameObject _hud;
        private GameObject _pause;
        private GameObject _finish;
        private GameObject _countdown;
        private Text _countdownText;
        private Text _lapText;
        private Text _rankText;
        private Text _timeText;
        private Text _bestLapText;
        private Text _speedText;
        private Text _gearText;
        private Text _itemText;
        private Text _itemDescriptionText;
        private Text _itemStatusText;
        private Text _itemIconText;
        private GameObject _noticeRoot;
        private Text _noticeText;
        private Text _finishTitle;
        private Text _finishStats;
        private Image _armorFill;
        private Image _nitroFill;
        private Image _driftFill;
        private Image _itemIconBackground;
        private Image _itemStatusBackground;
        private Image _noticeBackground;
        private Image _noticeAccent;
        private Image _noticeProgress;
        private Text _noticeIconText;
        private RectTransform _noticeOutlineTop;
        private RectTransform _noticeOutlineBottom;
        private RectTransform _noticeOutlineLeft;
        private RectTransform _noticeOutlineRight;
        private RectTransform _countdownRect;
        private Sprite _menuBackdropSprite;
        private Sprite _menuLogoSprite;
        private Sprite _menuSubtitleSprite;
        private Sprite _nitroItemSprite;
        private Sprite _shieldItemSprite;
        private Sprite _empItemSprite;
        private Sprite _pulseItemSprite;
        private Sprite _mineItemSprite;
        private Sprite _repairItemSprite;
        private Sprite _emptyItemSprite;
        private float _noticeHideAt = -1f;
        private bool _nitroReadyWasShown;
        private Sprite _chamferSprite;
        private Texture2D _chamferTexture;
        private Difficulty _selectedDifficulty = Difficulty.Pro;
        private readonly Dictionary<Difficulty, Image> _difficultyImages = new();

        public event Action<Difficulty> StartRequested;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action MenuRequested;

        public void Initialize(Camera worldCamera, RaceInputSource input)
        {
            ResetRuntimeUi();
            _font = CreateRuntimeFont();
            _chamferSprite = CreateChamferSprite(out _chamferTexture);
            _menuBackdropSprite = LoadUiSprite("UI/FigmaMenuBackdrop", "Figma menu backdrop");
            _menuLogoSprite = LoadUiSprite("UI/FigmaRuneRushLogo", "Figma Rune Rush logo");
            _menuSubtitleSprite = LoadUiSprite("UI/FigmaRuneRushSubtitle", "Figma Rune Rush subtitle");
            _repairItemSprite = LoadUiSprite("UI/Items/FigmaItemRepair", "Figma item repair");
            _nitroItemSprite = LoadUiSprite("UI/Items/FigmaItemNitro", "Figma item nitro");
            _shieldItemSprite = LoadUiSprite("UI/Items/FigmaItemShield", "Figma item shield");
            _empItemSprite = LoadUiSprite("UI/Items/FigmaItemEmp", "Figma item EMP");
            _pulseItemSprite = LoadUiSprite("UI/Items/FigmaItemPulse", "Figma item pulse");
            _mineItemSprite = LoadUiSprite("UI/Items/FigmaItemMine", "Figma item mine");
            _emptyItemSprite = LoadUiSprite("UI/Items/FigmaItemEmpty", "Figma empty item");
            EnsureEventSystem();

            GameObject canvasObject = new("RuneRush UI");
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _worldCamera = worldCamera;
            _isXrPresentation = XRRuntimeState.IsPicoXrPresentation(worldCamera);
            _canvas.renderMode = _isXrPresentation ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = worldCamera;
            _canvas.planeDistance = 0.42f;
            _canvas.sortingOrder = 30;
            _canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasScaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            if (_isXrPresentation)
            {
                ConfigureXrCanvas(canvasObject.transform, worldCamera);
            }

            BuildMenu();
            BuildHud();
            BuildPause();
            BuildFinish();
            SelectDifficulty(Difficulty.Pro);
            ConfigurePointer();
            _menuPresentation = new FullscreenMenuPresentation(_canvas, _worldCamera);
            _menuPresentation.SetVisible(true, _isXrPresentation);
        }

        private void ConfigurePointer()
        {
            EventSystem system = FindAnyObjectByType<EventSystem>();
            if (system == null) return;
            InputSystemUIInputModule desktop = system.GetComponent<InputSystemUIInputModule>();
            if (desktop == null) desktop = system.gameObject.AddComponent<InputSystemUIInputModule>();
            desktop.enabled = !_isXrPresentation;
            foreach (BaseInputModule module in system.GetComponents<BaseInputModule>())
                if (module != desktop && !(module is RuneRushXrPointerModule)) module.enabled = false;
            _xrPointer = system.GetComponent<RuneRushXrPointerModule>();
            if (_xrPointer == null && _isXrPresentation) _xrPointer = system.gameObject.AddComponent<RuneRushXrPointerModule>();
            if (_xrPointer != null)
            {
                _xrPointer.enabled = _isXrPresentation;
                if (_isXrPresentation) _xrPointer.Configure(_canvas, _worldCamera.transform.parent);
            }
            GraphicRaycaster desktopRay = _canvas.GetComponent<GraphicRaycaster>();
            if (desktopRay != null) desktopRay.enabled = !_isXrPresentation;
            if (_isXrPresentation)
            {
                foreach (Graphic graphic in _canvas.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = graphic.GetComponent<Button>() != null;
            }
        }

        public void SetInteractionMode(RacePhase phase, bool pointerHeld)
        {
            if (_xrPointer == null) return;
            _xrPointer.SetInteractionEnabled(phase == RacePhase.Menu || phase == RacePhase.Paused
                || phase == RacePhase.Finished || pointerHeld);
        }

        // Keep the desktop 1920x1080 layout contract intact in VR.  Only the
        // canvas root changes to World Space; HUD children retain their authored
        // anchors, avoiding the race panels collapsing toward one point.
        private static void ConfigureXrCanvas(Transform canvasTransform, Camera worldCamera)
        {
            Transform anchor = worldCamera != null && worldCamera.transform.parent != null
                ? worldCamera.transform.parent
                : worldCamera != null ? worldCamera.transform : null;
            if (anchor == null) return;

            canvasTransform.SetParent(anchor, false);
            RectTransform canvasRect = canvasTransform as RectTransform;
            if (canvasRect != null)
            {
                canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            }
            canvasTransform.localPosition = new Vector3(0f, 1.55f, 2.1f);
            canvasTransform.localRotation = Quaternion.identity;
            // Race HUD, pause and finish retain their authored physical layout.
            // The initial menu temporarily fits the headset view separately.
            canvasTransform.localScale = Vector3.one * 0.00130f;
            Canvas canvas = canvasTransform.GetComponent<Canvas>();
            CanvasScaler scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (canvas != null) canvas.renderMode = RenderMode.WorldSpace;
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }
            Debug.Log("RUNE_RUSH_XR_UI_ANCHOR: rootRect=1920x1080; rootScale=0.00130; physical=2.496m x 1.404m; distance=2.1m on Camera Offset; scaler=ConstantPixelSize. 2x all UI: menu/race/pause/finish. HUD zones=top-left race, bottom-left speed, bottom-center gear, bottom-right item, top-center notice.");
        }

        private void ResetRuntimeUi()
        {
            _menuPresentation?.Restore();
            _menuPresentation = null;
            if (_canvas != null) DestroyRuntimeObject(_canvas.gameObject);
            Transform existing = transform.Find("RuneRush UI");
            if (existing != null) DestroyRuntimeObject(existing.gameObject);
            if (_chamferSprite != null) DestroyRuntimeObject(_chamferSprite);
            if (_chamferTexture != null) DestroyRuntimeObject(_chamferTexture);
            if (_menuBackdropSprite != null) DestroyRuntimeObject(_menuBackdropSprite);
            if (_menuLogoSprite != null) DestroyRuntimeObject(_menuLogoSprite);
            if (_menuSubtitleSprite != null) DestroyRuntimeObject(_menuSubtitleSprite);
            DestroyItemSprites();

            _difficultyImages.Clear();
            _canvas = null;
            _canvasScaler = null;
            _menu = null;
            _hud = null;
            _pause = null;
            _finish = null;
            _countdown = null;
            _noticeRoot = null;
            _noticeText = null;
            _noticeBackground = null;
            _noticeAccent = null;
            _noticeProgress = null;
            _noticeIconText = null;
            _noticeOutlineTop = null;
            _noticeOutlineBottom = null;
            _noticeOutlineLeft = null;
            _noticeOutlineRight = null;
            _countdownRect = null;
            _bestLapText = null;
            _menuBackdropSprite = null;
            _menuLogoSprite = null;
            _menuSubtitleSprite = null;
            _nitroItemSprite = null;
            _shieldItemSprite = null;
            _empItemSprite = null;
            _pulseItemSprite = null;
            _mineItemSprite = null;
            _repairItemSprite = null;
            _emptyItemSprite = null;
            _noticeHideAt = -1f;
            _nitroReadyWasShown = false;
            _chamferSprite = null;
            _chamferTexture = null;
        }

        private void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void DestroyItemSprites()
        {
            DestroyRuntimeObject(_nitroItemSprite);
            DestroyRuntimeObject(_shieldItemSprite);
            DestroyRuntimeObject(_empItemSprite);
            DestroyRuntimeObject(_pulseItemSprite);
            DestroyRuntimeObject(_mineItemSprite);
            DestroyRuntimeObject(_repairItemSprite);
            DestroyRuntimeObject(_emptyItemSprite);
        }

        public void UpdateView(RaceSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Player == null) return;
            RefreshPresentationMode();
            bool menu = snapshot.Phase == RacePhase.Menu;
            _menuPresentation?.SetVisible(menu, _isXrPresentation);
            bool racingUi = snapshot.Phase is RacePhase.Countdown or RacePhase.Racing;
            _menu.SetActive(menu);
            _hud.SetActive(racingUi);
            _pause.SetActive(snapshot.Phase == RacePhase.Paused);
            _finish.SetActive(snapshot.Phase == RacePhase.Finished);
            _countdown.SetActive(snapshot.Phase == RacePhase.Countdown);

            if (snapshot.Phase != RacePhase.Racing)
            {
                HideNotice();
                if (snapshot.Phase != RacePhase.Paused) _nitroReadyWasShown = false;
            }

            int total = (snapshot.Racers?.Count ?? 5) + 1;
            _lapText.text = $"圈数  {Mathf.Clamp(snapshot.Lap, 1, 3)} / 3";
            _rankText.text = $"排名  {Mathf.Max(1, snapshot.Rank)} / {Mathf.Max(1, total)}";
            _timeText.text = $"赛时  {RaceMath.FormatTime(snapshot.Stats.Elapsed)}";
            _bestLapText.text = $"最佳圈速  {RaceMath.FormatTime(snapshot.Stats.BestLap)}";
            _speedText.text = Mathf.RoundToInt(snapshot.Player.Speed * 3.6f).ToString("000");
            _gearText.text = snapshot.Player.Speed < 1f ? "N" : Mathf.Clamp(Mathf.CeilToInt(snapshot.Player.Speed / 13f), 1, 6).ToString();
            _itemText.text = snapshot.Player.HasItem ? ItemName(snapshot.Player.Item) : "未持有圣物";
            _itemDescriptionText.text = snapshot.Player.HasItem ? ItemDescription(snapshot.Player.Item) : string.Empty;
            Sprite itemSprite = snapshot.Player.HasItem ? ItemSprite(snapshot.Player.Item) : _emptyItemSprite;
            bool usingFigmaSprite = itemSprite != null;
            _itemIconBackground.sprite = itemSprite;
            _itemIconBackground.type = Image.Type.Simple;
            _itemIconBackground.preserveAspect = true;
            _itemIconBackground.color = usingFigmaSprite ? Color.white : snapshot.Player.HasItem ? ItemColor(snapshot.Player.Item) : PanelSoft;
            _itemIconText.text = usingFigmaSprite ? string.Empty : snapshot.Player.HasItem ? ItemGlyph(snapshot.Player.Item) : "◇";
            Transform itemAccent = _itemIconBackground.transform.Find("Item Icon Accent");
            if (itemAccent != null) itemAccent.gameObject.SetActive(!usingFigmaSprite);
            float itemCooldown = Mathf.Max(0f, snapshot.Player.ItemCooldownUntil - snapshot.GameTime);
            bool coolingDown = !snapshot.Player.HasItem && itemCooldown > 0.01f;
            _itemStatusText.text = snapshot.Player.HasItem ? "可用" : coolingDown ? $"冷却 {Mathf.CeilToInt(itemCooldown):00}s" : "未持有圣物";
            _itemStatusBackground.color = snapshot.Player.HasItem ? Hex("1E3A2B") : coolingDown ? Hex("1A1F2A") : Hex("191B22");
            _armorFill.fillAmount = Mathf.Clamp01((100f - snapshot.Player.Damage) / 100f);
            _nitroFill.fillAmount = Mathf.Clamp01(snapshot.Player.Nitro / 100f);
            _driftFill.fillAmount = Mathf.Clamp01(snapshot.Player.DriftCharge / 100f);

            bool nitroReady = snapshot.Phase == RacePhase.Racing && snapshot.Player.Nitro >= 99.5f;
            if (nitroReady && !_nitroReadyWasShown)
            {
                ShowNotice("增压满值", RuneBlue, NoticeStyle.Boost, 1.7f);
            }
            _nitroReadyWasShown = nitroReady;

            if (_noticeRoot != null && _noticeRoot.activeSelf && Time.unscaledTime >= _noticeHideAt)
            {
                _noticeRoot.SetActive(false);
            }

            if (snapshot.Phase == RacePhase.Countdown)
            {
                bool go = snapshot.Countdown <= 0.55f;
                _countdownText.text = go ? "GO!" : Mathf.CeilToInt(snapshot.Countdown).ToString();
                if (_countdownRect != null) _countdownRect.sizeDelta = new Vector2(214f, 133f);
            }

            if (snapshot.Phase == RacePhase.Finished)
            {
                _finishTitle.text = snapshot.Rank == 1 ? "王冠归于你！" : $"王冠杯 · 第 {snapshot.Rank} 名";
                _finishStats.text = $"总用时  {RaceMath.FormatTime(snapshot.Stats.Elapsed)}\n" +
                                    $"最佳圈  {RaceMath.FormatTime(snapshot.Stats.BestLap)}\n" +
                                    $"最高速  {Mathf.RoundToInt(snapshot.Stats.TopSpeed)} km/h\n" +
                                    $"擦身而过  {snapshot.Stats.NearMisses}   命中  {snapshot.Stats.RivalsHit}";
            }
        }

        private void RefreshPresentationMode()
        {
            if (_canvas == null) return;
            bool xrPresentation = XRRuntimeState.IsPicoXrPresentation(_worldCamera);
            if (xrPresentation == _isXrPresentation) return;

            _menuPresentation?.Restore();
            _isXrPresentation = xrPresentation;
            _canvas.renderMode = xrPresentation ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _worldCamera;
            if (xrPresentation)
            {
                ConfigureXrCanvas(_canvas.transform, _worldCamera);
                Debug.Log("RUNE_RUSH_XR_UI_RECONFIGURED: switched to World Space after XR became active.");
            }
            ConfigurePointer();
        }

        private void OnDisable() => _menuPresentation?.Restore();

        public void PushEvents(IReadOnlyList<RaceEvent> raceEvents)
        {
            if (raceEvents == null) return;
            RaceEvent latest = null;
            int latestPriority = -1;
            for (int i = raceEvents.Count - 1; i >= 0; i--)
            {
                if (raceEvents[i] == null) continue;
                RaceEvent candidate = raceEvents[i];
                int priority = candidate.Code switch
                {
                    RaceEventCode.LapCompleted => 3,
                    RaceEventCode.ItemPickedUp => 2,
                    RaceEventCode.ObstacleHit or RaceEventCode.MineHitPlayer or RaceEventCode.RacerCollision or
                        RaceEventCode.OilSpin or RaceEventCode.ResetToTrack => 1,
                    _ => -1
                };
                if (priority > latestPriority)
                {
                    latestPriority = priority;
                    latest = candidate;
                }
            }

            if (_noticeRoot == null || _noticeText == null) return;
            if (latest == null) return;

            if (latest.Code == RaceEventCode.LapCompleted)
                ShowNotice("完成一圈", Gold, NoticeStyle.Lap, 2.3f);
            else if (latest.Code == RaceEventCode.ItemPickedUp)
                ShowNotice($"获得道具  {latest.Message}", DriftPurple, NoticeStyle.Item, 2.2f);
            else
                ShowNotice("赛车受损", Crimson, NoticeStyle.Damage, 1.8f);
        }

        public void CycleDifficulty(int direction)
        {
            int next = ((int)_selectedDifficulty + Math.Sign(direction) + 3) % 3;
            SelectDifficulty((Difficulty)next);
        }

        public void ConfirmStart()
        {
            StartRequested?.Invoke(_selectedDifficulty);
        }

        private void BuildMenu()
        {
            _menu = StretchPanel("Main Menu", _canvas.transform, Ink);
            BuildMenuBackdrop(_menu.transform);

            if (_menuLogoSprite != null)
            {
                SpriteImage("Rune Rush Logo", _menu.transform, _menuLogoSprite, new Vector2(0.5f, 0.5f),
                    new Vector2(850f, 310f), new Vector2(0f, 306f), true);
            }
            else
            {
                AnchoredLabel("RUNE RUSH", _menu.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 150f),
                    new Vector2(0f, 306f), 96, TextAnchor.MiddleCenter, GoldLight, FontStyle.Bold);
            }

            if (_menuSubtitleSprite != null)
            {
                SpriteImage("Rune Rush Subtitle", _menu.transform, _menuSubtitleSprite, new Vector2(0.5f, 0.5f),
                    new Vector2(700f, 184f), new Vector2(0f, 174f), true);
            }
            else
            {
                AnchoredLabel("— 符文疾驰：王冠杯 —", _menu.transform, new Vector2(0.5f, 0.5f), new Vector2(820f, 74f),
                    new Vector2(0f, 174f), 42, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            }

            RectTransform challengePanel = FramedPanel("Challenge Level", _menu.transform, new Vector2(0.5f, 0.5f),
                new Vector2(760f, 248f), new Vector2(0f, -64f), new Color(0.018f, 0.07f, 0.065f, 0.92f), Gold);
            AnchoredLabel("挑战等级", challengePanel, new Vector2(0.5f, 1f), new Vector2(360f, 48f),
                new Vector2(0f, -28f), 27, TextAnchor.MiddleCenter, Silver, FontStyle.Bold);
            CreateDifficultyButton(challengePanel, Difficulty.Rookie, "侍从", "稳健护佑", -230f);
            CreateDifficultyButton(challengePanel, Difficulty.Pro, "骑士", "王国标准", 0f);
            CreateDifficultyButton(challengePanel, Difficulty.Apex, "领主", "无情追猎", 230f);

            Button start = CommandButton("开始王冠杯", _menu.transform, new Vector2(0.5f, 0.5f),
                new Vector2(500f, 92f), Gold, Ink, new Vector2(0f, -326f));
            Text startLabel = start.GetComponentInChildren<Text>();
            if (startLabel != null) startLabel.fontSize = 34;
            start.onClick.AddListener(() => StartRequested?.Invoke(_selectedDifficulty));
            AnchoredLabel("手柄指向按钮 · 扳机确认    |    比赛中按住左握键显示指针", _menu.transform,
                new Vector2(0.5f, 0f), new Vector2(1250f, 55f), new Vector2(0f, 66f), 24, TextAnchor.MiddleCenter, Silver);
        }

        private void BuildMenuBackdrop(Transform parent)
        {
            if (_menuBackdropSprite != null)
            {
            RectTransform backdrop = SpriteImage("Figma Menu Backdrop", parent, _menuBackdropSprite,
                    new Vector2(0.5f, 0.5f), new Vector2(1920f, 1123f), Vector2.zero, true);
                backdrop.SetAsFirstSibling();
            }
            else
            {
                PanelRect("Backdrop fallback", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Hex("25313D"));
            }

            PanelRect("Bottom readability", parent, Vector2.zero, new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero,
                new Color(0f, 0.02f, 0.025f, 0.25f));
        }

        private void BuildHud()
        {
            _hud = StretchPanel("Race HUD", _canvas.transform, Color.clear);
            // The reference board is only a presentation surface. The runtime HUD itself stays fully transparent.
            RectTransform raceInfo = PanelRect("Race Information", _hud.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(372f, 125f), new Vector2(210f, -86.5f), Color.clear, false);
            RectTransform lapPanel = FigmaCardPanel("Lap", raceInfo, new Vector2(0f, 1f), new Vector2(133f, 53f),
                new Vector2(66.5f, -26.5f), FigmaCard, FigmaLine);
            _lapText = Label("圈数 1 / 3", lapPanel, 17, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);

            RectTransform timePanel = FigmaCardPanel("Time", raceInfo, new Vector2(0f, 1f), new Vector2(163f, 53f),
                new Vector2(221.5f, -26.5f), FigmaCard, FigmaLine);
            _timeText = Label("赛时 00:00.000", timePanel, 16, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);

            RectTransform rankPanel = FigmaCardPanel("Rank", raceInfo, new Vector2(0f, 1f), new Vector2(133f, 53f),
                new Vector2(66.5f, -98.5f), FigmaCard, FigmaLine);
            _rankText = Label("排名 1 / 6", rankPanel, 17, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);

            RectTransform bestLapPanel = FigmaCardPanel("Best Lap", raceInfo, new Vector2(0f, 1f), new Vector2(163f, 53f),
                new Vector2(221.5f, -98.5f), FigmaCard, FigmaLine);
            _bestLapText = Label("最佳圈速 00:00.000", bestLapPanel, 15, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);

            Button pauseButton = CommandButton("Ⅱ", raceInfo, new Vector2(0f, 1f), new Vector2(76f, 76f), FigmaGold, Ink,
                new Vector2(348f, -27f));
            pauseButton.onClick.AddListener(() => PauseRequested?.Invoke());

            RectTransform instrument = FigmaCardPanel("Instrument", _hud.transform, new Vector2(0f, 0f), new Vector2(316f, 131f),
                new Vector2(182f, 89.5f), FigmaInstrument, FigmaCopper);
            AnchoredLabel("SPEED", instrument, new Vector2(0f, 1f), new Vector2(80f, 18f),
                new Vector2(50f, -19f), 10, TextAnchor.MiddleLeft, FigmaGray, FontStyle.Bold);
            _speedText = AnchoredLabel("000", instrument, new Vector2(0f, 0.5f), new Vector2(120f, 55f),
                new Vector2(67f, 5f), 44, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);
            AnchoredLabel("km/h", instrument, new Vector2(0f, 0.5f), new Vector2(52f, 20f),
                new Vector2(127f, -5f), 10, TextAnchor.MiddleLeft, FigmaGray, FontStyle.Bold);
            _armorFill = CompactStatusBar(instrument, "赛车耐久度", 35f, FigmaGreen);
            _nitroFill = CompactStatusBar(instrument, "氮气储蓄量", 2f, FigmaBoost);
            _driftFill = CompactStatusBar(instrument, "漂移蓄能", -31f, FigmaGold);

            RectTransform gear = FigmaCardPanel("Gear", _hud.transform, new Vector2(0f, 0f), new Vector2(173f, 105f),
                new Vector2(478.5f, 76.5f), FigmaCard, FigmaLine);
            AnchoredLabel("当前档位", gear, new Vector2(0f, 1f), new Vector2(92f, 21f), new Vector2(56f, -17f),
                10, TextAnchor.MiddleLeft, FigmaGray, FontStyle.Bold);
            AnchoredLabel("6", gear, new Vector2(1f, 1f), new Vector2(28f, 25f), new Vector2(-19f, -17f),
                17, TextAnchor.MiddleCenter, FigmaGold, FontStyle.Bold);
            RectTransform gearBadge = FigmaCardPanel("Gear Badge", gear, new Vector2(0.5f, 0.5f), new Vector2(57f, 64f),
                new Vector2(0f, -7f), Hex("241E1D"), FigmaCopper);
            _gearText = Label("N", gearBadge, 35, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);

            RectTransform item = FigmaCardPanel("Current Item", _hud.transform, new Vector2(1f, 0f), new Vector2(150f, 158f),
                new Vector2(-99f, 103f), FigmaInstrument, FigmaCopper);
            RectTransform icon = PanelRect("Item Icon", item, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(78f, 78f), new Vector2(0f, -45f), Color.white);
            _itemIconBackground = icon.GetComponent<Image>();
            _itemIconBackground.raycastTarget = false;
            _itemIconText = Label("◇", icon, 32, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);
            _itemText = AnchoredLabel("未持有圣物", item, new Vector2(0.5f, 0.5f), new Vector2(140f, 28f),
                new Vector2(0f, -24f), 16, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);
            _itemDescriptionText = AnchoredLabel(string.Empty, item, new Vector2(0.5f, 0.5f), new Vector2(138f, 20f),
                new Vector2(0f, -46f), 10, TextAnchor.MiddleCenter, FigmaGray);
            RectTransform itemStatus = PanelRect("Item Status", item, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 18f), new Vector2(0f, 13f), Hex("31363B"));
            ApplyChamfer(itemStatus.GetComponent<Image>());
            _itemStatusBackground = itemStatus.GetComponent<Image>();
            _itemStatusText = Label("未持有圣物", itemStatus, 10, TextAnchor.MiddleCenter, FigmaText, FontStyle.Bold);

            RectTransform notice = PanelRect("Temporary Notice", _hud.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(203f, 42f), new Vector2(0f, -104f), FigmaBoostBackground);
            ApplyChamfer(notice.GetComponent<Image>());
            _noticeBackground = notice.GetComponent<Image>();
            RectTransform noticeAccent = PanelRect("Temporary Notice Accent", notice, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(4f, 0f), new Vector2(2f, 0f), FigmaBoost);
            _noticeAccent = noticeAccent.GetComponent<Image>();
            RectTransform noticeProgress = PanelRect("Temporary Notice Progress", notice, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(96f, 7f), new Vector2(61f, 0f), FigmaBoost);
            _noticeProgress = noticeProgress.GetComponent<Image>();
            _noticeRoot = notice.gameObject;
            _noticeText = Label(string.Empty, notice, 15, TextAnchor.MiddleCenter, FigmaBoost, FontStyle.Bold);
            _noticeIconText = AnchoredLabel(string.Empty, notice, new Vector2(1f, 0.5f), new Vector2(38f, 36f),
                new Vector2(-22f, 0f), 20, TextAnchor.MiddleCenter, FigmaGold, FontStyle.Bold);
            _noticeOutlineTop = PanelRect("Notice outline top", notice, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 2f), new Vector2(0f, -1f), FigmaBoost);
            _noticeOutlineBottom = PanelRect("Notice outline bottom", notice, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 2f), new Vector2(0f, 1f), FigmaBoost);
            _noticeOutlineLeft = PanelRect("Notice outline left", notice, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(2f, 0f), new Vector2(1f, 0f), FigmaBoost);
            _noticeOutlineRight = PanelRect("Notice outline right", notice, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(2f, 0f), new Vector2(-1f, 0f), FigmaBoost);
            _noticeRoot.SetActive(false);

            _countdownRect = PanelRect("Countdown", _hud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(214f, 133f), new Vector2(0f, 25f), FigmaCountdown);
            ApplyChamfer(_countdownRect.GetComponent<Image>());
            _countdown = _countdownRect.gameObject;
            _countdownText = Label("3", _countdown.transform, 78, TextAnchor.MiddleCenter, FigmaOrange, FontStyle.Bold);
            AddTextShadow(_countdownText, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));
        }

        private void BuildPause()
        {
            _pause = StretchPanel("Pause", _canvas.transform, new Color(0f, 0f, 0f, 0.52f));
            RectTransform panel = SolidChamferPanel("Pause commands", _pause.transform, new Vector2(0.5f, 0.5f),
                new Vector2(460f, 390f), Vector2.zero, FigmaPopup);
            AnchoredLabel("王冠杯暂停", panel, new Vector2(0.5f, 1f), new Vector2(285f, 45f), new Vector2(0f, -30f),
                24, TextAnchor.MiddleCenter, FigmaGold, FontStyle.Bold);
            Button resume = CommandButton("继续驰骋", panel, new Vector2(0.5f, 1f), new Vector2(330f, 76f), FigmaGreen, Ink,
                new Vector2(0f, -119f));
            Button restart = CommandButton("重新发车", panel, new Vector2(0.5f, 1f), new Vector2(330f, 76f), FigmaGray, Ink,
                new Vector2(0f, -215f));
            Button menu = CommandButton("返回赛事台", panel, new Vector2(0.5f, 1f), new Vector2(330f, 76f), FigmaRed, Silver,
                new Vector2(0f, -311f));
            resume.onClick.AddListener(() => ResumeRequested?.Invoke());
            restart.onClick.AddListener(() => RestartRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
        }

        private void BuildFinish()
        {
            _finish = StretchPanel("Finish", _canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            RectTransform panel = SolidChamferPanel("Finish panel", _finish.transform, new Vector2(0.5f, 0.5f),
                new Vector2(520f, 380f), Vector2.zero, FigmaPopup);
            _finishTitle = AnchoredLabel("王冠归于你！", panel, new Vector2(0.5f, 1f), new Vector2(360f, 68f),
                new Vector2(0f, -48f), 34, TextAnchor.MiddleCenter, FigmaOrange, FontStyle.Bold);
            _finishStats = AnchoredLabel(string.Empty, panel, new Vector2(0.5f, 0.5f), new Vector2(350f, 148f),
                new Vector2(0f, 7f), 18, TextAnchor.MiddleCenter, FigmaText);
            Button restart = CommandButton("再来一局", panel, new Vector2(0.5f, 0f), new Vector2(210f, 76f), FigmaGold, Ink,
                new Vector2(-115f, 56f));
            Button menu = CommandButton("赛事台", panel, new Vector2(0.5f, 0f), new Vector2(180f, 76f), FigmaGray, Ink,
                new Vector2(110f, 56f));
            restart.onClick.AddListener(() => RestartRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
        }

        private void CreateDifficultyButton(Transform parent, Difficulty difficulty, string title, string subtitle, float x)
        {
            Button button = CommandButton($"{title}\n{subtitle}", parent, new Vector2(0.5f, 0.5f),
                new Vector2(190f, 126f), Stone, Ink, new Vector2(x, -35f));
            _difficultyImages[difficulty] = button.GetComponent<Image>();
            button.onClick.AddListener(() => SelectDifficulty(difficulty));
        }

        private void SelectDifficulty(Difficulty difficulty)
        {
            _selectedDifficulty = difficulty;
            foreach (KeyValuePair<Difficulty, Image> pair in _difficultyImages)
            {
                pair.Value.color = pair.Key == difficulty ? Gold : Stone;
            }
        }

        private Image StatusBar(Transform parent, string label, float y, Color fillColor, float labelX, float barX)
        {
            AnchoredLabel(label, parent, new Vector2(0f, 0.5f), new Vector2(132f, 22f), new Vector2(labelX, y),
                12, TextAnchor.MiddleLeft, Silver, FontStyle.Bold);
            RectTransform background = PanelRect(label + " bg", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(196f, 13f), new Vector2(barX, y), new Color(0.025f, 0.035f, 0.045f, 1f));
            RectTransform fill = PanelRect(label + " fill", background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fillColor);
            Image image = fill.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = 1f;
            return image;
        }

        private Image CompactStatusBar(Transform parent, string label, float y, Color fillColor)
        {
            AnchoredLabel(label, parent, new Vector2(0f, 0.5f), new Vector2(88f, 16f),
                new Vector2(183f, y + 7f), 10, TextAnchor.MiddleLeft, FigmaGray, FontStyle.Bold);
            RectTransform background = PanelRect(label + " bg", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(104f, 7f), new Vector2(250f, y - 6f), Hex("362D2A"));
            RectTransform fill = PanelRect(label + " fill", background, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, fillColor);
            Image image = fill.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = 1f;
            return image;
        }

        private Button CommandButton(string text, Transform parent, Vector2 anchor, Vector2 size, Color background,
            Color foreground, Vector2? position = null)
        {
            RectTransform rect = PanelRect(text, parent, anchor, anchor, size, position ?? Vector2.zero, background);
            ApplyChamfer(rect.GetComponent<Image>());
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.65f, 1f, 0.85f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Text label = Label(text, rect, 23, TextAnchor.MiddleCenter, foreground, FontStyle.Bold);
            AddTextShadow(label, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, -1f));
            return button;
        }

        private enum NoticeStyle
        {
            Boost,
            Damage,
            Item,
            Lap
        }

        private void ShowNotice(string message, Color accent, NoticeStyle style, float duration)
        {
            if (_noticeRoot == null || _noticeText == null) return;
            RectTransform rect = _noticeRoot.GetComponent<RectTransform>();
            bool large = style == NoticeStyle.Lap;
            Color figmaAccent = style switch
            {
                NoticeStyle.Boost => FigmaBoost,
                NoticeStyle.Damage => FigmaRed,
                NoticeStyle.Item => FigmaItem,
                NoticeStyle.Lap => FigmaOrange,
                _ => accent
            };
            Color figmaBackground = style switch
            {
                NoticeStyle.Boost => FigmaBoostBackground,
                NoticeStyle.Damage => FigmaDamageBackground,
                NoticeStyle.Item => FigmaItemBackground,
                NoticeStyle.Lap => FigmaLapBackground,
                _ => FigmaCard
            };
            if (rect != null) rect.sizeDelta = new Vector2(large ? 271f : 203f, large ? 79f : 42f);
            if (_noticeBackground != null) _noticeBackground.color = figmaBackground;
            if (_noticeAccent != null)
            {
                _noticeAccent.color = figmaAccent;
                _noticeAccent.gameObject.SetActive(style is NoticeStyle.Damage or NoticeStyle.Item);
            }
            if (_noticeProgress != null)
            {
                _noticeProgress.color = figmaAccent;
                _noticeProgress.gameObject.SetActive(style == NoticeStyle.Boost);
            }
            bool outlined = style is NoticeStyle.Boost or NoticeStyle.Damage or NoticeStyle.Item;
            SetNoticeOutline(_noticeOutlineTop, figmaAccent, outlined);
            SetNoticeOutline(_noticeOutlineBottom, figmaAccent, outlined);
            SetNoticeOutline(_noticeOutlineLeft, figmaAccent, outlined);
            SetNoticeOutline(_noticeOutlineRight, figmaAccent, outlined);
            if (_noticeIconText != null)
            {
                _noticeIconText.text = style switch
                {
                    NoticeStyle.Damage => "△",
                    NoticeStyle.Lap => "⚑",
                    _ => string.Empty
                };
                _noticeIconText.color = figmaAccent;
            }
            _noticeText.text = message;
            _noticeText.color = figmaAccent;
            _noticeText.fontSize = large ? 30 : style == NoticeStyle.Item ? 14 : 15;
            _noticeText.alignment = style == NoticeStyle.Boost ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            _noticeText.rectTransform.offsetMin = style == NoticeStyle.Boost ? new Vector2(92f, 2f) : new Vector2(12f, 2f);
            _noticeText.rectTransform.offsetMax = style is NoticeStyle.Damage or NoticeStyle.Lap
                ? new Vector2(-42f, -2f)
                : new Vector2(-12f, -2f);
            _noticeRoot.SetActive(true);
            _noticeHideAt = Time.unscaledTime + Mathf.Max(0.25f, duration);
        }

        private void HideNotice()
        {
            if (_noticeRoot != null && _noticeRoot.activeSelf) _noticeRoot.SetActive(false);
            _noticeHideAt = -1f;
        }

        private static void SetNoticeOutline(RectTransform line, Color color, bool visible)
        {
            if (line == null) return;
            Image image = line.GetComponent<Image>();
            if (image != null) image.color = color;
            line.gameObject.SetActive(visible);
        }

        private RectTransform SpriteImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 size,
            Vector2 position, bool preserveAspect)
        {
            RectTransform rect = PanelRect(name, parent, anchor, anchor, size, position, Color.white);
            Image image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return rect;
        }

        private RectTransform SolidChamferPanel(string name, Transform parent, Vector2 anchor, Vector2 size,
            Vector2 position, Color background)
        {
            RectTransform rect = PanelRect(name, parent, anchor, anchor, size, position, background);
            ApplyChamfer(rect.GetComponent<Image>());
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.74f);
            outline.effectDistance = new Vector2(3f, -3f);
            return rect;
        }

        private Text AnchoredLabel(string text, Transform parent, Vector2 anchor, Vector2 size, Vector2 position,
            int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = PanelRect(text + " text", parent, anchor, anchor, size, position, Color.clear, false);
            return Label(text, rect, fontSize, alignment, color, style);
        }

        private Text Label(string value, Transform parent, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            var textObject = new GameObject("Label");
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 2f);
            rect.offsetMax = new Vector2(-10f, -2f);
            var label = textObject.AddComponent<Text>();
            label.font = _font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.text = value;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void AddTextShadow(Text label, Color color, Vector2 distance)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static GameObject StretchPanel(string name, Transform parent, Color color)
        {
            RectTransform rect = PanelRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
            return rect.gameObject;
        }

        private RectTransform FigmaCardPanel(string name, Transform parent, Vector2 anchor, Vector2 size,
            Vector2 position, Color background, Color outlineColor)
        {
            RectTransform rect = PanelRect(name, parent, anchor, anchor, size, position, background);
            ApplyChamfer(rect.GetComponent<Image>());
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private RectTransform FramedPanel(string name, Transform parent, Vector2 anchor, Vector2 size,
            Vector2 position, Color background, Color accent)
        {
            RectTransform rect = PanelRect(name, parent, anchor, anchor, size, position, background);
            ApplyChamfer(rect.GetComponent<Image>());
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Line.r, Line.g, Line.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform topLine = PanelRect(name + " Accent", rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 4f), new Vector2(0f, -2f), accent);
            topLine.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private void ApplyChamfer(Image image)
        {
            if (image == null || _chamferSprite == null) return;
            image.sprite = _chamferSprite;
            image.type = Image.Type.Sliced;
        }

        private static Sprite CreateChamferSprite(out Texture2D texture)
        {
            const int size = 32;
            const int cut = 7;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuneRush UI Chamfer",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool topLeft = x + (size - 1 - y) < cut;
                    bool topRight = (size - 1 - x) + (size - 1 - y) < cut;
                    bool bottomLeft = x + y < cut;
                    bool bottomRight = (size - 1 - x) + y < cut;
                    pixels[y * size + x] = topLeft || topRight || bottomLeft || bottomRight
                        ? Color.clear
                        : Color.white;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                size, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
            sprite.name = "RuneRush UI Chamfer";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static RectTransform PanelRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 size, Vector2 position, Color color, bool image = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            if (image)
            {
                Image panel = go.AddComponent<Image>();
                panel.color = color;
                panel.raycastTarget = color.a > 0.001f;
            }
            return rect;
        }

        private static Font CreateRuntimeFont()
        {
            Font bundled = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (bundled != null) return bundled;

            string[] preferred = { "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC", "Arial Unicode MS" };
            foreach (string fontName in preferred)
            {
                Font font = Font.CreateDynamicFontFromOSFont(fontName, 32);
                if (font != null) return font;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite LoadUiSprite(string resourcePath, string name)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            try
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                sprite.hideFlags = HideFlags.DontSave;
                return sprite;
            }
            catch (ArgumentException)
            {
                return Resources.Load<Sprite>(resourcePath);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static string ItemName(ItemType item)
        {
            return item switch
            {
                ItemType.Nitro => "疾风药剂",
                ItemType.Shield => "守护结界",
                ItemType.Emp => "寂静符文",
                ItemType.Pulse => "狮鹫之矢",
                ItemType.Mine => "诅咒石",
                ItemType.Repair => "牧师恩典",
                _ => "未知圣物"
            };
        }

        private static string ItemGlyph(ItemType item)
        {
            return item switch
            {
                ItemType.Nitro => "ϟ",
                ItemType.Shield => "✦",
                ItemType.Emp => "⊹",
                ItemType.Pulse => "◒",
                ItemType.Mine => "◎",
                ItemType.Repair => "✚",
                _ => "?"
            };
        }

        private Sprite ItemSprite(ItemType item)
        {
            return item switch
            {
                ItemType.Nitro => _nitroItemSprite,
                ItemType.Shield => _shieldItemSprite,
                ItemType.Emp => _empItemSprite,
                ItemType.Pulse => _pulseItemSprite,
                ItemType.Mine => _mineItemSprite,
                ItemType.Repair => _repairItemSprite,
                _ => null
            };
        }

        private static string ItemDescription(ItemType item)
        {
            return item switch
            {
                ItemType.Nitro => "氮气加速",
                ItemType.Shield => "护盾",
                ItemType.Emp => "EMP",
                ItemType.Pulse => "攻击脉冲",
                ItemType.Mine => "地雷",
                ItemType.Repair => "维护修复",
                _ => string.Empty
            };
        }

        private static Color ItemColor(ItemType item)
        {
            return item switch
            {
                ItemType.Nitro => Healing,
                ItemType.Shield => RuneBlue,
                ItemType.Emp => DriftPurple,
                ItemType.Pulse => Gold,
                ItemType.Mine => DriftPurple,
                ItemType.Repair => Hex("E8DFC9"),
                _ => PanelSoft
            };
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }

}
