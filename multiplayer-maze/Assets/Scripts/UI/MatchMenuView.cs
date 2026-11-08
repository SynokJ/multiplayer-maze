using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// View: the menu (name, create, join by code, room list) and the room screen (lobby, countdown,
/// HUD, result) as a uGUI canvas built in code. It scales to any screen, follows portrait and
/// landscape, and respects the phone's safe area. It never touches game data: every tap is raised
/// as an event and the controller decides.
/// </summary>
public class MatchMenuView : MonoBehaviour
{
    private const float _CARD_MAX_WIDTH = 920.0f;
    private const float _CARD_SIDE_MARGIN = 24.0f;
    private const int _MIN_PLAYERS = 1;

    public event Action<string, int> CreateRequested;
    public event Action<string, string> JoinCodeRequested;
    public event Action<string, string> JoinRoomRequested;
    public event Action RefreshRequested;
    public event Action LeaveRequested;
    public event Action<bool> ReadyRequested;
    public event Action RestartRequested;
    public event Action LocalHostRequested;
    public event Action LocalJoinRequested;

    private CanvasScaler _scaler;
    private RectTransform _safeArea;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;

    // Menu screen.
    private GameObject _menu;
    private RectTransform _menuCard;
    private CanvasGroup _menuGroup;
    private InputField _nameField;
    private InputField _codeField;
    private Text _playersText;
    private Text _statusText;
    private RectTransform _roomList;
    private int _maxPlayers = 4;

    // Room screen.
    private GameObject _room;
    private RectTransform _roomCard;
    private Text _roundText;
    private Text _hudText;
    private Text _countdownText;
    private Text _bannerText;
    private Text _hintText;
    private RectTransform _playersBox;
    private Button _readyButton;
    private Button _restartButton;
    private readonly List<Text> _playerRows = new List<Text>();
    private bool _myReady;

    public string PlayerName
    {
        get => _nameField.text;
        set => _nameField.text = value;
    }

    public bool IsRoomShown => _room.activeSelf;

    private void Awake()
    {
        EnsureEventSystem();
        BuildCanvas();
        BuildMenu();
        BuildRoom();
        ShowMenu();
        FitToScreen(true);
    }

    private void Update()
    {
        FitToScreen();
    }

    public void ShowMenu()
    {
        _menu.SetActive(true);
        _room.SetActive(false);
    }

    public void ShowRoom()
    {
        _menu.SetActive(false);
        _room.SetActive(true);
    }

    public void SetBusy(bool busy) => _menuGroup.interactable = !busy;

    public void SetStatus(string text, bool isError = false)
    {
        _statusText.text = text ?? string.Empty;
        _statusText.color = isError ? UiKit.Danger : UiKit.TextMuted;
    }

    public void SetRooms(IReadOnlyList<RoomInfo> rooms)
    {
        for (int i = _roomList.childCount - 1; i >= 0; --i)
            Destroy(_roomList.GetChild(i).gameObject);

        if (rooms.Count == 0)
        {
            Text empty = UiKit.Label(_roomList, "No open rooms yet. Create one!", UiKit.BODY, UiKit.TextMuted, TextAnchor.MiddleCenter);
            UiKit.Size(empty, height: 90.0f);
            return;
        }

        foreach (RoomInfo room in rooms)
        {
            RectTransform row = UiKit.Row(_roomList, "Room " + room.Id, 100.0f);

            string title = string.IsNullOrEmpty(room.Name) ? room.Id : room.Name;
            Text name = UiKit.Label(row, title + "  <color=#A8B5D1>" + room.Id + "</color>", 34);
            UiKit.Size(name, flexWidth: 1.0f);

            Text count = UiKit.Label(row, room.Players + "/" + room.MaxPlayers, 34, UiKit.TextMain, TextAnchor.MiddleCenter);
            UiKit.Size(count, width: 90.0f);

            string id = room.Id;
            Button join = UiKit.Button(row, "Join", UiKit.Primary, 90.0f, () => JoinRoomRequested?.Invoke(id, PlayerName), 34);
            UiKit.Size(join, width: 170.0f);
            join.interactable = room.Players < room.MaxPlayers;
        }
    }

    // Connected but the room state has not arrived yet.
    public void RenderConnecting()
    {
        _roundText.text = string.Empty;
        _hudText.text = "Connecting...";
        _countdownText.text = string.Empty;
        _roomCard.gameObject.SetActive(true);
        _bannerText.text = "Joining the room";
        _bannerText.color = UiKit.TextMain;
        _hintText.text = string.Empty;
        _readyButton.gameObject.SetActive(false);
        _restartButton.gameObject.SetActive(false);

        foreach (Text row in _playerRows)
            row.gameObject.SetActive(false);
    }

    public void Render(RoomSnapshot s)
    {
        _roundText.text = "Round " + (s.Round + 1);

        bool playing = s.Phase == GamePhase.Playing;
        bool ended = s.Phase == GamePhase.Won || s.Phase == GamePhase.Lost;

        _hudText.text = playing ? $"Time {s.TimeLeft}s    Items {s.Items}/{s.ItemsRequired}" : $"Items needed: {s.ItemsRequired}";
        _countdownText.text = s.Phase == GamePhase.Countdown ? Mathf.Max(1, s.Countdown).ToString() : string.Empty;

        // While playing only the top bar stays, so the whole screen is free for the joystick.
        _roomCard.gameObject.SetActive(!playing);

        _bannerText.text = Banner(s);
        _bannerText.color = s.Phase == GamePhase.Won ? UiKit.Good : s.Phase == GamePhase.Lost ? UiKit.Danger : UiKit.TextMain;
        _hintText.text = Hint(s);

        _myReady = s.MyReady;
        bool canReady = s.Phase == GamePhase.Lobby || s.Phase == GamePhase.Countdown;
        _readyButton.gameObject.SetActive(canReady);
        UiKit.SetButtonText(_readyButton, s.MyReady ? "Not ready" : "Ready");

        _restartButton.gameObject.SetActive(ended && s.IsOwner);

        RenderPlayers(s);
    }

    private static string Banner(RoomSnapshot s)
    {
        switch (s.Phase)
        {
            case GamePhase.Won: return "You escaped!";
            case GamePhase.Lost: return s.Reason == FinishReason.TimeUp ? "Time is up" : "Everyone was caught";
            case GamePhase.Countdown: return "Get ready";
            default: return "Lobby";
        }
    }

    private static string Hint(RoomSnapshot s)
    {
        switch (s.Phase)
        {
            case GamePhase.Lobby: return "The round starts when everyone is ready.";
            case GamePhase.Countdown: return "Starting soon. Tap Not ready to wait.";
            case GamePhase.Won:
            case GamePhase.Lost: return s.IsOwner ? string.Empty : "Waiting for the host to start another round.";
            default: return string.Empty;
        }
    }

    private void RenderPlayers(RoomSnapshot s)
    {
        while (_playerRows.Count < s.Players.Count)
        {
            Text row = UiKit.Label(_playersBox, string.Empty, 36);
            UiKit.Size(row, height: 64.0f);
            _playerRows.Add(row);
        }

        for (int i = 0; i < _playerRows.Count; ++i)
        {
            bool used = i < s.Players.Count;
            _playerRows[i].gameObject.SetActive(used);
            if (!used)
                continue;

            RoomSnapshot.PlayerLine p = s.Players[i];
            string tags = (p.IsOwner ? " (host)" : string.Empty) + (p.IsMe ? " (you)" : string.Empty);
            string state = p.Ready ? "  <color=#4DC780>READY</color>" : string.Empty;
            _playerRows[i].text = $"{p.Name}{tags}{state}";
        }
    }

    // ---- building ----

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var system = new GameObject("EventSystem", typeof(EventSystem));
        system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("MenuCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        _scaler = canvasObject.AddComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        _safeArea = UiKit.Rect(canvasObject.transform, "SafeArea");
        FitToScreen(true);
    }

    private void BuildMenu()
    {
        _menu = UiKit.Rect(_safeArea, "Menu").gameObject;
        UiKit.Stretch((RectTransform)_menu.transform);
        _menuGroup = _menu.AddComponent<CanvasGroup>();

        var dim = _menu.AddComponent<Image>();
        dim.color = UiKit.Dim;

        _menuCard = UiKit.Panel(_menu.transform, "Card", UiKit.Card);
        UiKit.Column(_menuCard, 18.0f, 32);

        Text title = UiKit.Label(_menuCard, "MAZE", 84, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Size(title, height: 120.0f);

        RectTransform nameRow = UiKit.Row(_menuCard, "NameRow", 96.0f);
        UiKit.Size(UiKit.Label(nameRow, "Name", UiKit.BODY, UiKit.TextMuted), width: 170.0f);
        _nameField = UiKit.Input(nameRow, "Your name", MatchValidation.MAX_NAME_LENGTH);
        _nameField.onValidateInput = (text, index, c) => MatchValidation.IsValidName(c.ToString()) ? c : '\0';
        UiKit.Size(_nameField, flexWidth: 1.0f);

        RectTransform createRow = UiKit.Row(_menuCard, "CreateRow", UiKit.BUTTON_HEIGHT);
        UiKit.Size(UiKit.Label(createRow, "Players", UiKit.BODY, UiKit.TextMuted), width: 170.0f);
        UiKit.Size(UiKit.Button(createRow, "-", UiKit.Secondary, 96.0f, () => ChangeMaxPlayers(-1), 52), width: 96.0f);
        _playersText = UiKit.Label(createRow, _maxPlayers.ToString(), 46, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Size(_playersText, width: 80.0f);
        UiKit.Size(UiKit.Button(createRow, "+", UiKit.Secondary, 96.0f, () => ChangeMaxPlayers(1), 52), width: 96.0f);
        UiKit.Size(UiKit.Button(createRow, "Create room", UiKit.Primary, UiKit.BUTTON_HEIGHT,
            () => CreateRequested?.Invoke(PlayerName, _maxPlayers)), flexWidth: 1.0f);

        RectTransform codeRow = UiKit.Row(_menuCard, "CodeRow", UiKit.BUTTON_HEIGHT);
        _codeField = UiKit.Input(codeRow, "Room code", MatchValidation.ROOM_ID_LENGTH);
        _codeField.onValidateInput = (text, index, c) => IsCodeChar(c) ? char.ToUpperInvariant(c) : ' ';
        UiKit.Size(_codeField, flexWidth: 1.0f);
        UiKit.Size(UiKit.Button(codeRow, "Join by code", UiKit.Secondary, UiKit.BUTTON_HEIGHT,
            () => JoinCodeRequested?.Invoke(_codeField.text, PlayerName)), width: 360.0f);

        RectTransform listHeader = UiKit.Row(_menuCard, "ListHeader", 80.0f);
        UiKit.Size(UiKit.Label(listHeader, "Open rooms", UiKit.BODY, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold), flexWidth: 1.0f);
        UiKit.Size(UiKit.Button(listHeader, "Refresh", UiKit.Secondary, 80.0f, () => RefreshRequested?.Invoke(), 32), width: 220.0f);

        ScrollRect scroll = UiKit.Scroll(_menuCard, out _roomList);
        UiKit.Size(scroll, height: 300.0f, flexHeight: 1.0f);

        _statusText = UiKit.Label(_menuCard, string.Empty, 34, UiKit.TextMuted, TextAnchor.MiddleCenter);
        UiKit.Size(_statusText, height: 90.0f);

#if UNITY_EDITOR
        RectTransform dev = UiKit.Row(_menuCard, "Dev", 80.0f);
        UiKit.Size(UiKit.Button(dev, "Editor: start host", UiKit.Secondary, 80.0f, () => LocalHostRequested?.Invoke(), 30), flexWidth: 1.0f);
        UiKit.Size(UiKit.Button(dev, "Editor: join host", UiKit.Secondary, 80.0f, () => LocalJoinRequested?.Invoke(), 30), flexWidth: 1.0f);
#endif
    }

    private void BuildRoom()
    {
        _room = UiKit.Rect(_safeArea, "Room").gameObject;
        UiKit.Stretch((RectTransform)_room.transform);

        // Top bar: always visible, small, away from the thumb.
        RectTransform bar = UiKit.Panel(_room.transform, "TopBar", new Color(0.05f, 0.06f, 0.10f, 0.75f));
        bar.anchorMin = new Vector2(0.0f, 1.0f);
        bar.anchorMax = new Vector2(1.0f, 1.0f);
        bar.pivot = new Vector2(0.5f, 1.0f);
        bar.sizeDelta = new Vector2(-24.0f, 120.0f);
        bar.anchoredPosition = new Vector2(0.0f, -12.0f);

        var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.padding = new RectOffset(28, 16, 12, 12);
        barLayout.spacing = 16.0f;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = false;
        barLayout.childForceExpandHeight = true;

        _roundText = UiKit.Label(bar, string.Empty, 34, UiKit.TextMuted);
        UiKit.Size(_roundText, width: 190.0f);
        _hudText = UiKit.Label(bar, string.Empty, 36, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
        UiKit.Size(_hudText, flexWidth: 1.0f);
        UiKit.Size(UiKit.Button(bar, "Leave", UiKit.Danger, 96.0f, () => LeaveRequested?.Invoke(), 32), width: 190.0f);

        // Big countdown number in the middle; it does not catch taps.
        _countdownText = UiKit.Label(_room.transform, string.Empty, 300, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Stretch(_countdownText.rectTransform);

        // Lobby / result card.
        _roomCard = UiKit.Panel(_room.transform, "Card", UiKit.Card);
        UiKit.Column(_roomCard, 18.0f, 32);
        _roomCard.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _bannerText = UiKit.Label(_roomCard, string.Empty, 64, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Size(_bannerText, height: 100.0f);

        _playersBox = UiKit.Rect(_roomCard, "Players");
        UiKit.Column(_playersBox, 4.0f, 8);

        _hintText = UiKit.Label(_roomCard, string.Empty, 32, UiKit.TextMuted, TextAnchor.MiddleCenter);
        UiKit.Size(_hintText, height: 70.0f);

        _readyButton = UiKit.Button(_roomCard, "Ready", UiKit.Primary, UiKit.BUTTON_HEIGHT, () => ReadyRequested?.Invoke(!_myReady));
        _restartButton = UiKit.Button(_roomCard, "Play again", UiKit.Good, UiKit.BUTTON_HEIGHT, () => RestartRequested?.Invoke());
    }

    private static bool IsCodeChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

    private void ChangeMaxPlayers(int delta)
    {
        _maxPlayers = Mathf.Clamp(_maxPlayers + delta, _MIN_PLAYERS, GameRules.MAX_PLAYERS_LIMIT);
        _playersText.text = _maxPlayers.ToString();
    }

    // Portrait phones and landscape desktops need different reference sizes, or text is tiny or huge.
    private void FitToScreen(bool force = false)
    {
        var screen = new Vector2Int(Screen.width, Screen.height);
        Rect safe = Screen.safeArea;
        if (!force && screen == _lastScreen && safe == _lastSafeArea)
            return;

        _lastScreen = screen;
        _lastSafeArea = safe;

        bool portrait = screen.y >= screen.x;
        _scaler.referenceResolution = portrait ? new Vector2(1080.0f, 1920.0f) : new Vector2(1920.0f, 1080.0f);
        _scaler.matchWidthOrHeight = portrait ? 0.0f : 1.0f;

        _safeArea.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, screen.x), safe.yMin / Mathf.Max(1, screen.y));
        _safeArea.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, screen.x), safe.yMax / Mathf.Max(1, screen.y));
        _safeArea.offsetMin = Vector2.zero;
        _safeArea.offsetMax = Vector2.zero;

        // Cards are centred and no wider than a comfortable column.
        float scale = portrait ? screen.x / 1080.0f : screen.y / 1080.0f;
        float width = Mathf.Min(_CARD_MAX_WIDTH, safe.width / Mathf.Max(0.01f, scale) - _CARD_SIDE_MARGIN * 2.0f);

        if (_menuCard != null)
        {
            _menuCard.anchorMin = new Vector2(0.5f, 0.0f);
            _menuCard.anchorMax = new Vector2(0.5f, 1.0f);
            _menuCard.pivot = new Vector2(0.5f, 0.5f);
            _menuCard.sizeDelta = new Vector2(width, -_CARD_SIDE_MARGIN * 2.0f);
            _menuCard.anchoredPosition = Vector2.zero;
        }

        if (_roomCard != null)
        {
            _roomCard.anchorMin = new Vector2(0.5f, 0.5f);
            _roomCard.anchorMax = new Vector2(0.5f, 0.5f);
            _roomCard.pivot = new Vector2(0.5f, 0.5f);
            _roomCard.sizeDelta = new Vector2(width, 0.0f);
            _roomCard.anchoredPosition = Vector2.zero;
        }
    }
}
