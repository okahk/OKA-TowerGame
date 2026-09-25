using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Class to hold all costume textures for a single costume
[Serializable]
public class CostumeTextures
{
    public Texture2D standTexture;
    public Texture2D walkTexture;
    public Texture2D jumpTexture;
}

public class TowerGameController : GameBaseController
{
    public static TowerGameController Instance = null;
    public GameObject playerPrefab;
    public GameObject onTopUI;
    public GameObject answerPrefab;
    public GameObject obstaclePrefab;
    public Transform globalParent;
    public CanvasGroup readyUI;
    public CanvasGroup startCountDownClock;
    public TextMeshProUGUI startCountDownText;
    public CanvasGroup readyTeamsUI;
    public GameObject readyBtn, cancelBtn;
    public NumberCounter blueTeamScore;
    public NumberCounter redTeamScore;
    public GameObject disconnectedUI;

    public List<CharacterController> characterControllers = new List<CharacterController>();
    public List<WS_Client.QuestionData> questions = new List<WS_Client.QuestionData>();
    public List<WS_Client.AnswerData> answers = new List<WS_Client.AnswerData>();
    public Camera trackingCamera;
    private int playerID = 0;
    public Text debugText;

    //this variable is only used in "next round order call" right now, change webSocket for start round position
    [SerializeField]
    private Vector3[] startingPos;

    private string costumeDataJson = "";
    public string accountCostumeId = ""; // ID of the costume currently equipped by the account
    public Dictionary<string, CostumeTextures> costumeTexturesById = new Dictionary<string, CostumeTextures>(); // costume_id -> CostumeTextures (stand, walk, jump)
    public Dictionary<string, CostumeData> costumeDataById = new Dictionary<string, CostumeData>(); // costume_id -> CostumeData
    public bool finishLoading = false; // Set to true after costume data and account costume ID are loaded
    private int loadingImagesCount = 0; // Track how many images are currently loading
    private int currentQuestionId = -1;

    // Map WS player key (string) -> CharacterController (ensures one GameObject per ws player)
    private Dictionary<string, CharacterController> playerControllersByKey = new Dictionary<string, CharacterController>();
    private Dictionary<int, string> playerKeysByUid = new Dictionary<int, string>();

    // Map question ID -> GameObject
    private Dictionary<int, GameObject> questionObjectsById = new Dictionary<int, GameObject>();

    // Map answer ID -> GameObject
    private Dictionary<int, GameObject> answerObjectsById = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> obstacleObjectsById = new Dictionary<int, GameObject>();
    private Dictionary<int, int> lastAnswerVisibilityByUid = new Dictionary<int, int>();
    private Dictionary<int, string> lastAnswerTextByUid = new Dictionary<int, string>();
    private HashSet<string> currentKeys = new HashSet<string>();
    private HashSet<string> disconnectedPlayerKeys = new HashSet<string>();
    public CharacterSet[] characterSets;
    public GameObject[] scoreboardControllers;
    public Sprite[] playerTags;
    public GameObject[] teamIcons;
    public TextMeshProUGUI correctAnswerText;
    public bool allPlayersReady = false;

    /// <summary>
    /// minmap
    /// </summary>
    public RectTransform minimapParent;
    public RawImage minimapRawImage;       
    public Texture localPlayerIndicator;
    public Sprite minimapBluePlayerMarker;
    public Sprite minimapOrangePlayerMarker;
    public Sprite minimapBlueOtherMarker;
    public Sprite minimapOrangeOtherMarker;
    public Sprite minimapAnswerMarker;
    public RectTransform minimapMarkersParent;                   // parent under the minimap canvas (can be the RawImage rectTransform)
    public Vector2 minimapWorldBottomLeft = new Vector2(-50f, -50f); // world coords that map to minimap bottom-left
    public Vector2 minimapWorldTopRight = new Vector2(50f, 50f);

    private Dictionary<string, RectTransform> minimapMarkersByKey = new Dictionary<string, RectTransform>();
    private Dictionary<int, RectTransform> minimapAnswerMarkersByKey = new Dictionary<int, RectTransform>();

    // throttle SyncPlayers to reduce per-frame cost (seconds)
    public float syncPlayersInterval = 0.1f;
    private float lastSyncPlayersTime = 0f;

    public float clientMapScale = 1.0f;
    private bool suppressSyncPlayers = false;
    private int[] previous = new int[2] { -1, -1 };

    protected override void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        //DontDestroyOnLoad(this.gameObject);
    }

    // Start is called before the first frame update
    protected override void Start()
    {
        base.Start();
        this.GetComponent<AudioControl>().starBGMStatusAutoTrue();
        var client = WS_Client.Instance;
        // Subscribe to the order changed event
        if (client != null)
        {
            client.OnStartCountDownChanged += HandleStartCountDownChanged;
            client.OnOrderChanged += HandleOrderChanged;

            // Check if there's a pending order that arrived before we subscribed
            if (!string.IsNullOrEmpty(client.pendingOrder))
            {
                HandleOrderChanged(client.pendingOrder);
                client.pendingOrder = ""; // Clear after processing
            }
        }

        if (client?.GameData?.teamScore != null && client.GameData.teamScore.Count >= 2)
        {
            previous[0] = client.GameData.teamScore[0];
            previous[1] = client.GameData.teamScore[1];
        }

        if (this.minimapParent == null)
        {
            this.minimapParent = this.minimapRawImage.rectTransform.parent as RectTransform;
        }

        var minimapScaler = this.minimapParent.GetComponent<CanvasScaler>();
        if (minimapScaler != null)
        {
            var referenceResolution = minimapScaler.referenceResolution;
            if (this.minimapParent.sizeDelta.x != referenceResolution.x)
            {
                float scaleFactor = Mathf.Clamp(referenceResolution.x / this.minimapParent.sizeDelta.x, 0.8f, 1.5f);
                //Debug.Log(scaleFactor);

                float scaledPosX = this.minimapRawImage.rectTransform.localPosition.x * scaleFactor;
                float scaledPosY = this.minimapRawImage.rectTransform.localPosition.y;

                //Debug.Log(this.minimapRawImage.rectTransform.localPosition);
                this.minimapRawImage.rectTransform.localPosition = new Vector3(scaledPosX, scaledPosY, 0f);
            }
        }

        // Wait for starwishApiCaller to be ready before fetching costume data
        StartCoroutine(WaitForStarwishApi());

        /*// change ready button to WS_Client's ready button
        if (WS_Client.Instance.GameData.players.Find(p => p.uid == WS_Client.Instance.public_UserInfo.uid).status != "playing") {
            readyButton.SetActive(true);
        }*/
    }

    private IEnumerator WaitForStarwishApi()
    {
        while (LoaderConfig.Instance == null || LoaderConfig.Instance.apiManager == null)
        {
            yield return null;
        }

        // Fetch both costume data and account costume ID in parallel
        // Task task1 = fetchCostumeData();
        Task task2 = fetchAccountCostumeId();
        
        // Wait for both tasks to complete
        yield return new WaitUntil(() => task2.IsCompleted);
        
        finishLoading = true;
    }

    public void showTeamGetScore(int teamId)
    {
        for (int i = 0; i < this.teamIcons.Length; i++)
        {
            if (i == teamId)
            {
                if (this.teamIcons[i] != null)
                {
                    this.teamIcons[i].SetActive(true);
                }
            }
            else
            {
                if (this.teamIcons[i] != null)
                {
                    this.teamIcons[i].SetActive(false);
                }
            }
        }
    }

    protected async Task fetchAccountCostumeId()
    {
        try
        {
            if (LoaderConfig.Instance == null)
            {
                LogController.Instance.debug("fetchAccountCostumeId: LoaderConfig.Instance is null. Aborting fetch.");
                return;
            }

            var api = LoaderConfig.Instance.apiManager;
            if (api == null)
            {
                LogController.Instance.debug("fetchAccountCostumeId: LoaderConfig.Instance.apiManager is null. Aborting fetch.");
                return;
            }

            string jsonResponse = null;
            try
            {
                jsonResponse = await api.getCurrentAccount();
            }
            catch (Exception apiEx)
            {
                LogController.Instance.debugError($"getCurrentAccount() threw: {apiEx.Message}\n{apiEx.StackTrace}");
                return;
            }

            LogController.Instance.debug("jsonResponse: " + (jsonResponse ?? "null"));

            if (string.IsNullOrEmpty(jsonResponse))
            {
                LogController.Instance.debug("fetchAccountCostumeId: api returned empty response.");
                return;
            }

            try
            {
                StarwishPartyAccountResponse accountResponse = JsonUtility.FromJson<StarwishPartyAccountResponse>(jsonResponse);

                if (accountResponse != null &&
                    accountResponse.data != null &&
                    accountResponse.data.equipped_costume_data != null &&
                    !string.IsNullOrEmpty(accountResponse.data.equipped_costume_data.costume_id))
                {
                    accountCostumeId = accountResponse.data.equipped_costume_data.costume_id;
                    LogController.Instance.debug($"fetchAccountCostumeId: accountCostumeId = {accountCostumeId}");
                }
                else
                {
                    LogController.Instance.debug("fetchAccountCostumeId: No equipped costume found in account response");
                }
            }
            catch (Exception parseEx)
            {
                LogController.Instance.debugError($"Failed to parse account data JSON: {parseEx.Message}\n{parseEx.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            LogController.Instance.debugError($"Failed to fetch account costume ID: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private Vector2 WorldToMinimapAnchoredPosition(Vector2 worldPos)
    {
        if (minimapRawImage == null || minimapMarkersParent == null) return Vector2.zero;

        RectTransform mapRT = minimapRawImage.rectTransform;

        // world bounds
        float minX = minimapWorldBottomLeft.x;
        float maxX = minimapWorldTopRight.x;
        float minY = minimapWorldBottomLeft.y;
        float maxY = minimapWorldTopRight.y;

        if (Mathf.Approximately(maxX, minX) || Mathf.Approximately(maxY, minY))
        {
            LogController.Instance.debug("Minimap world bounds invalid (min == max). Check minimapWorldBottomLeft/topRight.");
            return Vector2.zero;
        }

        // compute inner texture rect (local coords of mapRT)
        Rect inner = GetRawImageInnerRect(mapRT, minimapRawImage);

        // world extents
        float worldW = maxX - minX;
        float worldH = maxY - minY;
        if (Mathf.Approximately(worldW, 0f) || Mathf.Approximately(worldH, 0f))
        {
            LogController.Instance.debug("Invalid world size for minimap mapping.");
            return Vector2.zero;
        }

        // Map world position to inner rect by linear scaling:
        // localX = inner.xMin + (worldPos.x - minX) * (inner.width / worldW)
        // localY = inner.yMin + (worldPos.y - minY) * (inner.height / worldH)
        float scaleX = inner.width / worldW;
        float scaleY = inner.height / worldH;

        float localX = inner.xMin + (worldPos.x - minX) * scaleX;
        float localY = inner.yMin + (worldPos.y - minY) * scaleY;

        // clamp into inner rect to avoid corners when out-of-bounds
        localX = Mathf.Clamp(localX, inner.xMin, inner.xMax);
        localY = Mathf.Clamp(localY, inner.yMin, inner.yMax);

        Vector2 localPointInMapRT = new Vector2(localX, localY);

        // If markers are direct children of the minimap RectTransform, return the local point directly.
        if (minimapMarkersParent == mapRT)
        {
            return localPointInMapRT;
        }

        // Otherwise convert to minimapMarkersParent local coordinates
        Canvas mapCanvas = minimapRawImage.canvas;
        Camera canvasCam = mapCanvas != null ? mapCanvas.worldCamera : null;

        Vector3 worldPoint = mapRT.TransformPoint(localPointInMapRT);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCam, worldPoint);

        Vector2 anchoredPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(minimapMarkersParent, screenPoint, canvasCam, out anchoredPos);

#if UNITY_EDITOR
        if (debugText != null)
        {
            debugText.text = $"World({worldPos.x:F1},{worldPos.y:F1}) -> local({localX:F2},{localY:F2}) inner({inner.xMin:F1},{inner.yMin:F1},{inner.width:F1},{inner.height:F1})";
        }
#endif

        return anchoredPos;
    }

    private Rect GetRawImageInnerRect(RectTransform rt, RawImage rawImage)
    {
        Rect r = rt.rect;

        if (rawImage == null || rawImage.texture == null)
            return r;

        // If texture has no size, return full rect
        float texW = rawImage.texture.width;
        float texH = rawImage.texture.height;
        if (texW <= 0f || texH <= 0f) return r;

        float texAspect = texW / texH;
        float rectAspect = r.width / r.height;

        Rect inner = r;

        if (rectAspect > texAspect)
        {
            // letterbox horizontally — texture height fits, width is smaller
            float innerW = r.height * texAspect;
            inner.x = r.x + (r.width - innerW) * 0.5f;
            inner.width = innerW;
        }
        else
        {
            // letterbox vertically — texture width fits, height is smaller
            float innerH = r.width / texAspect;
            inner.y = r.y + (r.height - innerH) * 0.5f;
            inner.height = innerH;
        }

        return inner;
    }

    // Add this handler method inside TowerGameController
    private void HandleStartCountDownChanged(int newCountDown)
    {
        // Use existing method to update UI; it reads WS_Client.Instance.startCountDown internally
        this.controlReadyCountDown(newCountDown);
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (WS_Client.Instance != null)
        {
            WS_Client.Instance.OnStartCountDownChanged -= HandleStartCountDownChanged;
            WS_Client.Instance.OnOrderChanged -= HandleOrderChanged;
        }
    }

    // This method will be called whenever the order changes
    private void HandleOrderChanged(string newOrder)
    {
        LogController.Instance.debug($"Order changed to: {newOrder}");

        // Handle different order types
        switch (newOrder)
        {
            case "addPlayer":
                if (WS_Client.Instance.pendingAddedUid > 0)
                {
                    MarkPlayerConnected(WS_Client.Instance.pendingAddedUid);
                    WS_Client.Instance.pendingAddedUid = -1;
                }
            break;
            case "removePlayer":
                if (WS_Client.Instance.pendingRemovedUid > 0)
                {
                    MarkPlayerDisconnected(WS_Client.Instance.pendingRemovedUid);
                    WS_Client.Instance.pendingRemovedUid = -1;
                }
            break;
            case "reconnectPlayer":
                StartCoroutine(updateScoreUI());
                showReadyUI(false);
                checkAnswerVisibility();

                var client = WS_Client.Instance;
                if (client == null || client.GameData == null) break;

                int localUid = client.public_UserInfo != null ? client.public_UserInfo.uid : -1;

                if (client.pendingReconnectUid != -1)
                {
                    if (client.pendingReconnectUid == localUid)
                    {
                        StartCoroutine(updateQuestionUI(true));
                        SetUI.Set(this.TopUILayer, true, 0f);
                    }
                    client.pendingReconnectUid = -1;
                }
                break;
            case "startGame":
                showReadyUI(false);
                //ReassignPlayersForStart();
                StartGame.Instance.startGameSequence();
                StartCoroutine(updateQuestionUI(false));
                resetStartingPos();
                break;
            case "endGame":
                this.EndGame();
                break;
            case "resetGame":
                showReadyUI(true);
                onTopUI.GetComponent<CanvasGroup>().alpha = 0;
                resetStartingPos();
                // Add your logic here
                break;
            case "nextRound":
                StartCoroutine(updateQuestionUI(true));
                SetUI.Set(this.TopUILayer, true, 0f);
                resetStartingPos();
                break;
            case "getAnswer":
                checkAnswerVisibility();
                break;
            case "submitCorrectAnswer":
                submitCorrectAnswerHandler();
                break;
            case "submitWrongAnswer":
                submitWrongAnswerHandler();
                break;
            case "disconnected":
                disconnectedUI.SetActive(true);
                break;
            case "reconnected":
                // Start background sync: wait for server SyncRoomData then refresh local game state
                StartCoroutine(HandleReconnectedSync());
                break;
            case "localCancelReady":
                this.allPlayersReady = false;
                break;
            default:
                break;
        }
    }


    private IEnumerator HandleReconnectedSync()
    {
        // Keep UI visible while we try to sync (optional)
        // disconnectedUI.SetActive(true);

        var client = WS_Client.Instance;
        float timeout = 5f;
        float elapsed = 0f;
        // Wait until GameData is populated or timeout
        while ((client == null || client.GameData == null) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
            client = WS_Client.Instance;
        }

        if (client == null || client.GameData == null)
        {
            LogController.Instance.debug("HandleReconnectedSync: GameData not available after reconnect.");
            // keep disconnected UI or show alternative message
            disconnectedUI.SetActive(true);
            yield break;
        }

        try
        {
            // Hide disconnected UI now that we have data
            disconnectedUI.SetActive(false);

            // Recreate / resync players and UI from authoritative GameData
            // This will create missing player controllers and set positions
            resetStartingPos(true);

            // Immediately sync UI pieces: scores, question, answers, minimap, answer visibility
            StartCoroutine(updateScoreUI());
            StartCoroutine(updateQuestionUI(true));
            checkAnswerVisibility();

            // Force an immediate players sync (updates destinations, minimap markers, answer bubbles)
            SyncPlayers();

            LogController.Instance.debug("HandleReconnectedSync: sync completed.");
        }
        catch (Exception ex)
        {
            LogController.Instance.debugError($"HandleReconnectedSync error: {ex.Message}\n{ex.StackTrace}");
            // If anything went wrong, show disconnected UI so user knows state is not clean
            disconnectedUI.SetActive(true);
        }
    }

    private void showReadyUI(bool show) {
        SetUI.Set(this.readyUI, show);
        SetUI.Set(this.readyTeamsUI, show);
        this.readyBtn?.SetActive(show);
        this.cancelBtn?.SetActive(!show);

        SetUI.SetScale(this.startCountDownClock, false);
        if (this.startCountDownText != null)
        {
            this.startCountDownText.text = "";
            this.startCountDownText.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }
    }

    // Replace existing controlReadyCountDown() with this implementation
    public void controlReadyCountDown(int countDown)
    {
        //Debug.Log($"controlReadyCountDown called with countDown={countDown}");  
        // Defensive checks
        if (this.readyUI == null || this.startCountDownClock == null)
            return;

        // If ready UI is hidden, always hide the clock
        if (this.readyUI.alpha == 0)
        {
            this.allPlayersReady = false;
            if (this.startCountDownText != null)
            {
                this.startCountDownText.text = "";
                this.startCountDownText.ForceMeshUpdate(); // ensure TMP redraw
                Canvas.ForceUpdateCanvases();
            }
            return;
        }

        var client = WS_Client.Instance;
        // Determine whether any player (including local) is currently "ready"
        this.allPlayersReady = true;
        try
        {
            var players = client.GameData?.players;
            if (players != null)
            {
                foreach (var p in players)
                {
                    if (p == null) continue;
                    if (!string.IsNullOrEmpty(p.status) && !p.status.Equals("ready", StringComparison.OrdinalIgnoreCase))
                    {
                        this.allPlayersReady = false;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogController.Instance?.debugError($"controlReadyCountDown: failed to inspect player statuses: {ex.Message}");
            this.allPlayersReady = false;
        }

        int playersCount = client?.GameData?.players?.Count ?? 0;
        bool validMatchSize = (playersCount == 2 || playersCount == 4 || playersCount == 6);

        // Show clock only when countdown is active AND at least one player is ready.
        if (countDown > -1 && this.allPlayersReady && validMatchSize)
        {
            if (this.startCountDownText != null)
            {
                this.startCountDownText.text = countDown.ToString();
                this.startCountDownText.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
            }
            this.controlStartCountDown(true);
        }
        else
        {
            // Hide and clear text when nobody is ready or countdown not active
            if (this.startCountDownText != null)
            {
                this.startCountDownText.text = "";
                this.startCountDownText.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
            }
        }
    }

    public void controlStartCountDown(bool status)
    {
        if (status)
        {
            SetUI.SetScale(this.startCountDownClock, true);
        }
        else
        {
            SetUI.SetScale(this.startCountDownClock, false);
        }
    }

    public void hideDisconnectedUI()
    {
        disconnectedUI.SetActive(false);
    }

    public void EndGame()
    {
        LogController.Instance.debug("Game is ended");
        if (WS_Client.Instance != null)
        {
            WS_Client.Instance.pendingReconnectRoomId = "";
        }
        StartCoroutine(updateScoreUI(()=>
        {
            var client = WS_Client.Instance;
            if (client == null || client.GameData == null || client.GameData.players == null)
            {
                Debug.LogError("GameData or players is null in endGame");
                return;
            }

            int[] teamScores = new int[2] { 0, 0 };
            if (client.GameData.teamScore != null && client.GameData.teamScore.Count >= 2)
            {
                // Defensive copy / bounds check
                teamScores[0] = client.GameData.teamScore.Count > 0 ? client.GameData.teamScore[0] : 0;
                teamScores[1] = client.GameData.teamScore.Count > 1 ? client.GameData.teamScore[1] : 0;
            }
            else
            {
                // Fallback: sum player scores by team using player list order (legacy)
                var players = client.GameData.players;
                if (players != null)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        var p = players[i];
                        if (p == null) continue;
                        // ensure we don't throw on uninitialized score
                        int s = p.score;
                        teamScores[i % 2] += s;
                    }
                }
            }
            this.endGamePage.updateFinalScore(0, teamScores[0]);
            this.endGamePage.updateFinalScore(1, teamScores[1]);
            onTopUI.GetComponent<CanvasGroup>().alpha = 0;

            try
            {
                int localUid = client.public_UserInfo != null ? client.public_UserInfo.uid : -1;
                int localIndex = -1;
                for (int i = 0; i < client.GameData.players.Count; i++)
                {
                    var p = client.GameData.players[i];
                    if (p != null && p.uid == localUid)
                    {
                        localIndex = i;
                        break;
                    }
                }

                if (localIndex != -1)
                {
                    int localTeam = localIndex % 2;
                    int otherTeam = 1 - localTeam;
                    bool localTeamWins = teamScores[localTeam] > teamScores[otherTeam];

                    if (localTeamWins)
                    {
                        // Show end game page with success true for winner
                        this.endGamePage.setStatus(true, true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogController.Instance.debugError($"EndGame: failed to compute local team or set end page: {ex.Message}\n{ex.StackTrace}");
            }

            base.endGame();
        }));
        //showReadyUI(false);
    }

    private IEnumerator updateScoreUI(Action completed=null)
    {
        var client = WS_Client.Instance;
        while (client.GameData == null) {
            yield return new WaitForSeconds(0.1f);
        }
        if(this.blueTeamScore != null) this.blueTeamScore.Value = client.GameData.teamScore[0];
        if(this.redTeamScore != null) this.redTeamScore.Value = client.GameData.teamScore[1];

        completed?.Invoke();
    }

    private IEnumerator updateQuestionUI(bool _autoPlayAudio = false)
    {
        while (WS_Client.Instance == null ||
               WS_Client.Instance.GameData == null ||
               WS_Client.Instance.GameData.questions == null ||
               WS_Client.Instance.GameData.questions.Count == 0) {
            yield return new WaitForSeconds(0.1f);
        }

        int round = Math.Clamp(WS_Client.Instance.GameData.round, 1, WS_Client.Instance.GameData.questions.Count);
        if(_autoPlayAudio) RoundTitle.Instance?.ShowRoundTitle(round - 1);
        QuestionController.Instance?.nextQuestion(_autoPlayAudio);
        while (round == currentQuestionId) {
            round = WS_Client.Instance.GameData.round;
            yield return new WaitForSeconds(0.1f);
        }
        if (round > 0) {
            currentQuestionId = round;
        }
    }


    private void SyncPlayers()
    {
        try
        {
            if (suppressSyncPlayers) return;
            var client = WS_Client.Instance;
            if (client == null || client.GameData == null || client.GameData.teamScore == null) return;
            var players = client.GameData.players;
            if (players == null) return;
            
            // Clear currentKeys so we rebuild it from the authoritative GameData
            currentKeys.Clear();

            // Get local player's uid (if available)
            int localUid = -1;
            if (WS_Client.Instance.public_UserInfo != null)
            {
                localUid = WS_Client.Instance.public_UserInfo.uid;
            }

            // Track minimap presence while iterating players (reuse currentKeys)
            foreach (var player in players)
            {
                string key = !string.IsNullOrEmpty(player.player_id) ? player.player_id : player.uid.ToString();

                if(!string.IsNullOrEmpty(player.answerContent)) this.correctAnswerText.text = player.answerContent;

                bool isLocal = (player.uid == localUid);
                if (!playerControllersByKey.ContainsKey(key))
                {
                    Vector3 location = player.position != null && player.position.Length >= 2
                        ? new Vector3(player.position[0], player.position[1], 0f)
                        : Vector3.zero;

                    LogController.Instance.debug("CreatePlayerFromData 1: " + location + " - " + key + " - " + isLocal);
                    this.CreatePlayerFromData(player, location, key, isLocal);
                }

                // mark as present for this cycle (used for player removal and minimap cleanup)
                currentKeys.Add(key);

                // update non-local players' destination
                if (!isLocal)
                {
                    if (playerControllersByKey.TryGetValue(key, out var cc))
                    {
                        if (cc != null)
                        {
                            Vector2 otherPlayerPos = Vector3.zero;
                            if (player.position != null && player.position.Length >= 2)
                            {
                                //otherPlayerPos = MapServerToLocal();
                                otherPlayerPos = new Vector2(player.position[0], player.position[1]);
                                cc.setLocalDestination(otherPlayerPos);
                            }
                        }
                    }
                }

                // --- Minimap marker update (merged here to avoid a second players pass) ---
                if (minimapRawImage != null && minimapMarkersParent != null)
                {
                    // Determine world position for marker (prefer authoritative server position)
                    Vector2 worldPos = Vector2.zero;
                    if (player.position != null && player.position.Length >= 2)
                    {
                        worldPos = new Vector2(player.position[0], player.position[1]);
                    }
                    else if (playerControllersByKey.TryGetValue(key, out var fallbackCc) && fallbackCc != null)
                    {
                        Vector3 t = fallbackCc.transform.position; // use world-space position
                        worldPos = new Vector2(t.x, t.y);
                    }

                    Vector2 anchoredPos = WorldToMinimapAnchoredPosition(worldPos);

                    if (!minimapMarkersByKey.TryGetValue(key, out var marker) || marker == null)
                    {
                        // Determine team first (needed to select correct sprite)
                        int team = -1;
                        if (!string.IsNullOrEmpty(player.player_id))
                        {
                            // parse "playerN" safely
                            string idDigits = player.player_id.StartsWith("player", StringComparison.OrdinalIgnoreCase)
                                ? player.player_id.Substring(6)
                                : player.player_id;
                            if (int.TryParse(idDigits, out int parsedIndex))
                            {
                                team = Mathf.Max(0, (parsedIndex - 1) % 2);
                            }
                        }
                        if (team == -1)
                        {
                            team = (player.uid % 2 == 0) ? 0 : 1;
                        }

                        // Determine if local player
                        bool isLocalPlayer = false;
                        if (playerControllersByKey.TryGetValue(key, out var cc) && cc != null)
                        {
                            isLocalPlayer = cc.IsLocalPlayer;
                        }

                        // Select appropriate sprite based on team and local player status
                        Sprite spriteToUse = null;
                        if (team == 0) // Blue team
                        {
                            spriteToUse = minimapBluePlayerMarker;
                        }
                        else // Red team
                        {
                            spriteToUse = minimapOrangePlayerMarker;
                        }

                        // Create new GameObject with Image component as child of minimapRawImage.rectTransform
                        RectTransform parentRT = minimapRawImage.rectTransform;
                        GameObject markerObj = new GameObject($"MinimapMarker_{key}");
                        markerObj.transform.SetParent(parentRT, false);
                    
                        RectTransform instance = markerObj.AddComponent<RectTransform>();
                        Image markerImage = markerObj.AddComponent<Image>();
                        markerImage.sprite = spriteToUse;
                        markerImage.raycastTarget = false; // Disable raycasting for performance

                        // Ensure marker uses centered anchors/pivot and neutral scale so anchoredPosition behaves predictably
                        instance.anchorMin = new Vector2(0.5f, 0.5f);
                        instance.anchorMax = new Vector2(0.5f, 0.5f);
                        instance.pivot = new Vector2(0.5f, 0.5f);
                        instance.localScale = Vector3.one;

                            // Set size (adjust these values based on your sprite size preferences)
                            RectTransform subIcon = new GameObject("Icon").AddComponent<RectTransform>();
                            subIcon.SetParent(instance, false);
                            subIcon.anchorMin = new Vector2(0.5f, 0.5f);
                            subIcon.anchorMax = new Vector2(0.5f, 0.5f);
                            subIcon.pivot = new Vector2(0.5f, 0.5f);
                            subIcon.localScale = Vector3.one;
                            subIcon.anchoredPosition = Vector2.zero;
                            subIcon.sizeDelta = new Vector2(12f, 12f);
                            RawImage iconImage = subIcon.gameObject.AddComponent<RawImage>();
                            iconImage.raycastTarget = false;

                            if (isLocalPlayer)
                            {
                                RectTransform subIconIndicator = new GameObject("Indicator").AddComponent<RectTransform>();
                                subIconIndicator.SetParent(subIcon, false);
                                subIconIndicator.anchorMin = new Vector2(0.5f, 0.5f);
                                subIconIndicator.anchorMax = new Vector2(0.5f, 0.5f);
                                subIconIndicator.pivot = new Vector2(0.5f, 0.5f);
                                subIconIndicator.localScale = Vector3.one * 0.4f;
                                subIconIndicator.anchoredPosition = new Vector3(0f, 60f, 0f);
                                subIconIndicator.sizeDelta = new Vector2(localPlayerIndicator.width, localPlayerIndicator.height);
                                RawImage subIconIndicatorImage = subIconIndicator.gameObject.AddComponent<RawImage>();
                                subIconIndicatorImage.raycastTarget = false;
                                subIconIndicatorImage.texture = localPlayerIndicator;
                            }

                            Texture iconTex = null;
                            if (!string.IsNullOrEmpty(player.costume_id) && int.TryParse(player.costume_id, out int costumeId))
                            {
                                int csIndex = costumeId - 1;
                                if (characterSets != null && csIndex >= 0 && csIndex < characterSets.Length && characterSets[csIndex] != null)
                                {
                                    // characterSets[].defaultIcon is expected to be a Texture2D or Texture
                                    iconTex = characterSets[csIndex].defaultIcon;
                                    if (iconTex == null && characterSets[csIndex].defaultIcon != null)
                                    {
                                        // fallback if defaultIcon is a Sprite
                                        iconImage.texture = iconTex;
                                    }
                                }
                            }

                            if (iconTex != null)
                            {
                                iconImage.texture = iconTex;
                                // optional: preserve aspect by adjusting size (keeps icon readable)
                                float aspect = (iconTex.width > 0 && iconTex.height > 0) ? (float)iconTex.width / iconTex.height : 1f;
                                if (aspect >= 1f)
                                    subIcon.sizeDelta = new Vector2(75f, 75f / aspect);
                                else
                                    subIcon.sizeDelta = new Vector2(75f * aspect, 75f);
                            }
                            else
                            {
                                // No icon — hide child to avoid empty visuals
                                iconImage.enabled = false;
                            }
                        minimapMarkersByKey[key] = instance;
                        marker = instance;
                    }

                    // Update marker position using the function that returns local map coords when markers are children
                    if (marker != null)
                    {
                        marker.anchoredPosition = anchoredPos;
                    }
                }
            }

            int team0 = client.GameData.teamScore.Count > 0 ? client.GameData.teamScore[0] : 0;
            int team1 = client.GameData.teamScore.Count > 1 ? client.GameData.teamScore[1] : 0;

            if (previous[0] == -1 && previous[1] == -1)
            {
                previous[0] = team0;
                previous[1] = team1;
                return;
            }

            if (team0 > previous[0])
            {
                this.showTeamGetScore(0);
            }
            else if (team1 > previous[1])
            {
                this.showTeamGetScore(1);
            }

            previous[0] = team0;
            previous[1] = team1;

            // Remove controllers for players who left (keys not present in currentKeys)
            // Collect keys to remove to avoid modifying dictionary during iteration
            var toRemove = new List<string>();
        foreach (var kv in playerControllersByKey)
        {
            if (!currentKeys.Contains(kv.Key))
            {
                toRemove.Add(kv.Key);
            }
        }

        foreach (var key in toRemove)
        {
            RemovePlayer(key, true);
        }

        // Remove minimap markers for players that are gone (cleanup)
        if (minimapMarkersByKey != null && minimapMarkersByKey.Count > 0)
        {
            var markersToRemove = new List<string>();
            foreach (var kv in minimapMarkersByKey)
            {
                if (!currentKeys.Contains(kv.Key))
                {
                    if (kv.Value != null) GameObject.Destroy(kv.Value.gameObject);
                    markersToRemove.Add(kv.Key);
                }
            }
            foreach (var k in markersToRemove) minimapMarkersByKey.Remove(k);
        }
        }
        catch (System.Exception ex)
        {
            LogController.Instance.debugError($"Error in SyncPlayers: {ex.Message}\n{ex.StackTrace}");
        }
    }


    // Update is called once per frame
    void Update()
    {
        try
        {
            // If no data, nothing to do
            if (finishLoading && Time.time - lastSyncPlayersTime >= syncPlayersInterval)
            {
                lastSyncPlayersTime = Time.time;
                SyncPlayers();

                if(!this.allPlayersReady)
                {
                    this.controlStartCountDown(false);
                }
            }   

            if (Input.GetKeyDown(KeyCode.P))
            {
                // printCostumeData(); // for DEV printCostumeData
                printGameData(); // for DEV printCostumeData
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error in Update: {ex.Message}\n{ex.StackTrace}");
        }
    }

    void printGameData() {
        // Debug.Log($"=== Game Data ===");
        // Debug.Log($"Players: {WS_Client.Instance.GameData.players.Count}");
        // Debug.Log($"Questions: {WS_Client.Instance.GameData.questions.Count}");
        // Debug.Log($"Answers: {WS_Client.Instance.GameData.answers.Count}");
        // Debug.Log($"Obstacles: {WS_Client.Instance.GameData.obstacles.Count}");
        // Debug.Log($"=== End Game Data ===");
        foreach (WS_Client.PlayerData player in WS_Client.Instance.GameData.players) {
            LogController.Instance.debug($"player: {player.uid} - {player.costume_id}");
        }
    }

    void printCostumeData()
    {
        Debug.Log($"=== Costume Data ({costumeDataById.Count} costumes loaded) ===");
        
        foreach (var kvp in costumeDataById)
        {
            string costumeId = kvp.Key;
            CostumeData costume = kvp.Value;
            
            bool isEquipped = costumeId == accountCostumeId;
            // Check if textures are loaded
            if (costumeTexturesById.ContainsKey(costumeId))
            {
                CostumeTextures textures = costumeTexturesById[costumeId];
                Debug.Log($"  Textures loaded: Stand={textures.standTexture != null}, Walk={textures.walkTexture != null}, Jump={textures.jumpTexture != null}");
            }
            else
            {
                Debug.Log($"  Textures: Not loaded yet");
            }
        }
        
        Debug.Log("=== End Costume Data ===");
    }

    private void FixedUpdate()
{
    try
    {
        // If no data, nothing to do
        if (WS_Client.Instance == null || WS_Client.Instance.GameData == null) return;
        var players = WS_Client.Instance.GameData.players;
        if (players == null || WS_Client.Instance.public_UserInfo == null) return;

        foreach (WS_Client.PlayerData player in WS_Client.Instance.GameData.players)
        {
            if (player == null || player.uid == WS_Client.Instance.public_UserInfo.uid)
            {
                continue;
            }
            CharacterController characterController = characterControllers != null ? characterControllers.Find(c => c.UserId == player.uid) : null;
            if (characterController != null)
            {
                int answerVisible = player.isAnswerVisible != 0 ? 1 : 0;
                if (characterController.answerBubble != null &&
                    (!lastAnswerVisibilityByUid.TryGetValue(player.uid, out int lastVisibility) || lastVisibility != answerVisible))
                {
                    SetUI.Set(characterController.answerBubble, answerVisible != 0);
                    lastAnswerVisibilityByUid[player.uid] = answerVisible;
                }

                string answerContent = "";
                if (player.answer_id != 0 && WS_Client.Instance.GameData.answers != null)
                {
                    var answer = WS_Client.Instance.GameData.answers.Find(a => a.id == player.answer_id);
                    if (answer != null)
                    {
                        answerContent = answer.content;
                    }
                    else
                    {
                        LogController.Instance.debug($"Answer with id {player.answer_id} not found for player {player.uid}");
                    }
                }

                if (characterController.answerText != null &&
                    (!lastAnswerTextByUid.TryGetValue(player.uid, out string lastAnswerText) || lastAnswerText != answerContent))
                {
                    characterController.answerText.text = answerContent;
                    lastAnswerTextByUid[player.uid] = answerContent;
                }
            }
        }
    }
    catch (Exception ex)
    {
        LogController.Instance.debugError($"Error in FixedUpdate: {ex.Message}\n{ex.StackTrace}");
    }

    // Process answers
    if (WS_Client.Instance.GameData.answers != null)
    {
        var currentAnswerIds = new HashSet<int>();

        // Collect answers that need to be removed locally because server says they're on a player
        var toRemoveLocal = new List<int>();

        foreach (var answer in WS_Client.Instance.GameData.answers)
        {
            if (answer == null) continue;
            currentAnswerIds.Add(answer.id);

            // If server marks answer as on a player, ensure no world object exists
            if (answer.isOnPlayer == 1)
            {
                // If we have a local GameObject for this answer, remove it
                if (answerObjectsById.ContainsKey(answer.id))
                {
                    toRemoveLocal.Add(answer.id);
                }

                // Also ensure minimap marker is removed (RemoveAnswerObject handles both)
                continue;
            }

            // answer.isOnPlayer == 0 => create or update world object
            if (!answerObjectsById.ContainsKey(answer.id))
            {
                // Create answer at position from data
                Vector3 answerPos = Vector3.zero;
                if (answer.position != null && answer.position.Length >= 2)
                {
                    answerPos = new Vector3(answer.position[0], answer.position[1], 0f);
                }
                CreateAnswerObject(answer, answerPos);
            }
            else
            {
                // Update answer position if it exists and still free
                var answerObj = answerObjectsById[answer.id];
                if (answerObj != null && answer.position != null && answer.position.Length >= 2)
                {
                    Vector2 uiPosition = new Vector2(answer.position[0], answer.position[1]);
                    RectTransform rectTransform = answerObj.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        rectTransform.anchoredPosition = uiPosition;
                    }
                    else
                    {
                        answerObj.transform.position = new Vector3(uiPosition.x, uiPosition.y, 0f);
                    }
                }
            }

            // --- Minimap marker for answers ---
            if (minimapRawImage != null && minimapAnswerMarker != null && answer.position != null && answer.position.Length >= 2)
            {
                Vector2 worldPos = new Vector2(answer.position[0], answer.position[1]);
                Vector2 anchoredPos = WorldToMinimapAnchoredPosition(worldPos);

                if (!minimapAnswerMarkersByKey.TryGetValue(answer.id, out var answerMarker) || answerMarker == null)
                {
                    // Create new answer marker
                    RectTransform parentRT = minimapRawImage.rectTransform;
                    GameObject markerObj = new GameObject($"MinimapAnswerMarker_{answer.id}");
                    markerObj.transform.SetParent(parentRT, false);

                    RectTransform instance = markerObj.AddComponent<RectTransform>();
                    Image markerImage = markerObj.AddComponent<Image>();
                    markerImage.sprite = minimapAnswerMarker;
                    markerImage.raycastTarget = false;

                    // Centered anchors/pivot
                    instance.anchorMin = new Vector2(0.5f, 0.5f);
                    instance.anchorMax = new Vector2(0.5f, 0.5f);
                    instance.pivot = new Vector2(0.5f, 0.5f);
                    instance.localScale = Vector3.one;
                    instance.sizeDelta = new Vector2(40f, 40f); // Slightly smaller than player markers

                    minimapAnswerMarkersByKey[answer.id] = instance;
                    answerMarker = instance;
                }

                // Update marker position
                if (answerMarker != null)
                {
                    answerMarker.anchoredPosition = anchoredPos;
                }
            }
        }

        // Remove local world objects for answers server says are taken
        foreach (var id in toRemoveLocal)
        {
            RemoveAnswerObject(id);
        }

        // Remove answers that no longer exist on server (cleanup local objects)
        var answersToRemove = new List<int>();
        foreach (var kv in answerObjectsById)
        {
            if (!currentAnswerIds.Contains(kv.Key))
            {
                answersToRemove.Add(kv.Key);
            }
        }

        // Now remove them safely
        foreach (var answerId in answersToRemove)
        {
            RemoveAnswerObject(answerId);
            // Also remove minimap marker for this answer (RemoveAnswerObject handles marker)
        }
    }
}

    private void resetStartingPos(bool preserveDisconnectedPlayers = false)
    {
        suppressSyncPlayers = true;
        try
        {
            // 1) Remove all player controllers (safe snapshot of keys)
            var keys = new List<string>(playerControllersByKey.Keys);
            foreach (var k in keys)
            {
                RemovePlayer(k, preserveDisconnectedPlayers && IsGameStarted() && !IsPlayerInGameData(k));
            }

            // Ensure dictionaries/lists are empty
            playerControllersByKey.Clear();

            // 2) Destroy any leftover character GameObjects and clear list
            for (int i = characterControllers.Count - 1; i >= 0; i--)
            {
                var cc = characterControllers[i];
                if (cc != null && cc.gameObject != null)
                {
                    GameObject.Destroy(cc.gameObject);
                }
                characterControllers.RemoveAt(i);
            }

            // 3) Remove all minimap markers created for players
            if (minimapMarkersByKey != null)
            {
                foreach (var kv in minimapMarkersByKey)
                {
                    if (kv.Value != null) GameObject.Destroy(kv.Value.gameObject);
                }
                minimapMarkersByKey.Clear();
            }

            // 4) Optionally keep answers/obstacles; do not change unless desired.
            // (If you want to clear answers/obstacles too, do similar destroy+clear here.)

            // 5) Recreate controllers from authoritative GameData.players (maintain server ordering)
            var client = WS_Client.Instance;
            if (client == null || client.GameData == null || client.GameData.players == null)
                return;

            var players = client.GameData.players;
            int localUid = client.public_UserInfo != null ? client.public_UserInfo.uid : -1;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;

                string key = !string.IsNullOrEmpty(p.player_id) ? p.player_id : p.uid.ToString();
                bool isLocal = (p.uid == localUid);

                Vector3 startPos = Vector3.zero;
                if (p.position != null && p.position.Length >= 2)
                    startPos = new Vector3(p.position[0], p.position[1], 0f);

                // Use existing factory to create controller and register into dictionaries
                CreatePlayerFromData(p, startPos, key, isLocal);

                // Ensure created controller correct identity (defensive)
                if (playerControllersByKey.TryGetValue(key, out var created) && created != null)
                {
                    disconnectedPlayerKeys.Remove(key);
                    created.UserId = p.uid;
                    created.UserName = p.ename ?? ("Player_" + p.uid);
                    created.detectCamera = this.trackingCamera;
                    created.gameObject.tag = isLocal ? "MainPlayer" : "Untagged";
                    created.setLocalPlayer(isLocal);

                    // For remote players set destination from server
                    if (!isLocal && p.position != null && p.position.Length >= 2)
                    {
                        created.setLocalDestination(new Vector3(p.position[0], p.position[1], 0f));
                    }
                    else
                    {
                        // ensure local player's destination initialized
                        created.setLocalDestination(created.transform.localPosition);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogController.Instance.debugError($"resetStartingPos error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // allow SyncPlayers to resume
            suppressSyncPlayers = false;
        }
    }

    private void CreatePlayerFromData(WS_Client.PlayerData player, Vector3 startPos, string key, bool isLocal = false)
    {
        try
        {
            if (player == null)
            {
                LogController.Instance.debugError("Attempted to create player from null PlayerData");
                return;
            }
            
            // Instantiate without parent, set world position, then attach to parent preserving world pos
            var characterController = GameObject.Instantiate(this.playerPrefab, this.globalParent).GetComponent<CharacterController>();
            
            // Find the scoreboardController with matching uid
            scoreboardController matchingScoreboard = null;

            if (string.IsNullOrEmpty(player.player_id))
            {
                LogController.Instance.debugError($"Player {player.uid} has null or empty player_id");
                return;
            }

            string playerNumber = player.player_id.StartsWith("player", StringComparison.OrdinalIgnoreCase)
                ? player.player_id.Substring(6)
                : player.player_id;

            if (int.TryParse(playerNumber, out int playerIndex) &&
                this.scoreboardControllers != null &&
                playerIndex > 0 && playerIndex <= this.scoreboardControllers.Length)
            {
                GameObject scoreboardObject = this.scoreboardControllers[playerIndex - 1];
                scoreboardController scoreboard = scoreboardObject != null
                    ? scoreboardObject.GetComponent<scoreboardController>()
                    : null;

                if (scoreboard != null && (string.IsNullOrEmpty(scoreboard.key) || scoreboard.key == key))
                {
                    matchingScoreboard = scoreboard;
                    matchingScoreboard.key = key;
                }
            }
        
        if (characterController == null)
        {
            LogController.Instance.debugError("playerPrefab missing CharacterController component");
            GameObject.Destroy(characterController.gameObject);
            return;
        }

            int uid = player.uid;
            characterController.detectCamera = this.trackingCamera;
            characterController.gameObject.name = "Player_" + uid;
            characterController.UserName = "Player_" + uid;
            characterController.UserId = uid;

            if (!string.IsNullOrEmpty(player.answerContent))
            {
                characterController.answerId = player.answer_id;
                characterController.showAnswerBubble(1, player.answerContent ?? "");
            }

            if (isLocal) characterController.gameObject.tag = "MainPlayer";
        this.characterControllers.Add(characterController);

        // set world-space start position
        characterController.transform.localPosition = startPos;
        characterController.transform.localScale = Vector3.one * (1f /this.clientMapScale);

        // mark local player for client-side control
        characterController.setLocalPlayer(isLocal);
        if (this.playerTags != null && this.playerTags.Length >= 2)
        {
            int teamTagIndex = (playerIndex % 2 == 0) ? 1 : 0;
            characterController.setPlayerTag(this.playerTags[teamTagIndex], player.ename);
        }
        else
        {
            LogController.Instance.debugError("TowerGameController: playerTags must contain at least two team sprites.");
            characterController.setPlayerTag(null, player.ename);
        }
        if (isLocal)
        {
            LogController.Instance.debug($"Local player created for uid={uid}");
        }

        playerControllersByKey[key] = characterController;
            playerKeysByUid[uid] = key;
        characterController.key = key;

        Texture2D scoreboardIcon = null;

        // Safely parse and access costume with bounds checking
        if (!string.IsNullOrEmpty(player.costume_id) && int.TryParse(player.costume_id, out int costumeId))
        {
            int arrayIndex = costumeId - 1;
            if (this.characterSets != null &&
                arrayIndex >= 0 && arrayIndex < this.characterSets.Length &&
                this.characterSets[arrayIndex] != null)
            {
                var characterSet = this.characterSets[arrayIndex];
                if (characterSet.walkingAnimationTextures != null && characterSet.walkingAnimationTextures.Length > 0)
                {
                    characterController.SetCostumeTextures(characterSet);
                }

                scoreboardIcon = characterSet.defaultIcon as Texture2D;
            }
            else
            {
                int costumeCount = this.characterSets != null ? this.characterSets.Length : 0;
                LogController.Instance.debugError($"Invalid costume_id {player.costume_id} for player {player.uid}. Valid range: 1-{costumeCount}");
            }
        }
        else
        {
            LogController.Instance.debug($"Player {player.uid} has invalid or empty costume_id: {player.costume_id}");
        }

        // Scoreboard visibility must not depend on costume parsing succeeding.
        if (matchingScoreboard != null)
        {
            disconnectedPlayerKeys.Remove(key);
            matchingScoreboard.setScoreboard(key, scoreboardIcon, player.ename);
        }

            // keep an incremental id for legacy naming if needed
            this.playerID = Mathf.Max(this.playerID, uid + 1);
            LogController.Instance.debug($"Created player GameObject for uid={uid} at {startPos} (isLocal={isLocal})");
        }
        catch (Exception ex)
        {
            LogController.Instance.debugError($"Error creating player from data: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void RemovePlayer(string key, bool showDisconnected)
    {
        if (playerControllersByKey.TryGetValue(key, out var cc))
        {
            if (cc != null)
            {
                // Find matching scoreboard
                GameObject scoreboardObj = Array.Find(this.scoreboardControllers, obj => obj.GetComponent<scoreboardController>().key == cc.key);
                if (scoreboardObj != null) {
                    LogController.Instance.debug("RemovePlayer: scoreboardObj=" + scoreboardObj.name + cc.key);
                    scoreboardController matchingScoreboard = scoreboardObj.GetComponent<scoreboardController>();
                    if (matchingScoreboard != null) {
                        if (IsGameStarted() && (showDisconnected || disconnectedPlayerKeys.Contains(key)))
                        {
                            matchingScoreboard.setDisconnected(true);
                        }
                        else
                        {
                            matchingScoreboard.resetScoreboard();
                        }
                    }
                }
                this.characterControllers.Remove(cc);
                GameObject.Destroy(cc.gameObject);
                LogController.Instance.debug($"[TowerGameController] Removed player GameObject for key={key}");
            }
            playerControllersByKey.Remove(key);
        }

        this.RemovePlayerMarker(key);

    #if !UNITY_WEBGL && !UNITY_IOS
    GC.Collect();
    #endif
    }

    private void MarkPlayerDisconnected(int uid)
    {
        var player = WS_Client.Instance.GameData != null && WS_Client.Instance.GameData.players != null
            ? WS_Client.Instance.GameData.players.Find(candidate => candidate != null && candidate.uid == uid)
            : null;
        var character = characterControllers != null
            ? characterControllers.Find(candidate => candidate != null && candidate.UserId == uid)
            : null;
        var key = player != null ? player.player_id : character != null ? character.key :
            (playerKeysByUid.TryGetValue(uid, out var knownKey) ? knownKey : null);
        if (string.IsNullOrEmpty(key)) return;

        if (!IsGameStarted())
        {
            disconnectedPlayerKeys.Remove(key);
            var waitingScoreboard = Array.Find(scoreboardControllers, obj =>
                obj != null && obj.GetComponent<scoreboardController>() != null &&
                obj.GetComponent<scoreboardController>().key == key);
            if (waitingScoreboard != null)
            {
                waitingScoreboard.GetComponent<scoreboardController>().resetScoreboard();
            }
            return;
        }

        disconnectedPlayerKeys.Add(key);
        var scoreboardObject = Array.Find(scoreboardControllers, obj =>
            obj != null && obj.GetComponent<scoreboardController>() != null &&
            obj.GetComponent<scoreboardController>().key == key);
        if (scoreboardObject != null)
        {
            scoreboardObject.GetComponent<scoreboardController>().setDisconnected(true);
        }
    }

    private void MarkPlayerConnected(int uid)
    {
        var player = WS_Client.Instance.GameData != null && WS_Client.Instance.GameData.players != null
            ? WS_Client.Instance.GameData.players.Find(candidate => candidate != null && candidate.uid == uid)
            : null;
        var character = characterControllers != null
            ? characterControllers.Find(candidate => candidate != null && candidate.UserId == uid)
            : null;
        var key = player != null ? player.player_id : character != null ? character.key :
            (playerKeysByUid.TryGetValue(uid, out var knownKey) ? knownKey : null);
        if (string.IsNullOrEmpty(key)) return;

        disconnectedPlayerKeys.Remove(key);
        var scoreboardObject = Array.Find(scoreboardControllers, obj =>
            obj != null && obj.GetComponent<scoreboardController>() != null &&
            obj.GetComponent<scoreboardController>().key == key);
        if (scoreboardObject != null)
        {
            scoreboardObject.GetComponent<scoreboardController>().setDisconnected(false);
        }
    }

    private bool IsPlayerInGameData(string key)
    {
        var players = WS_Client.Instance.GameData != null ? WS_Client.Instance.GameData.players : null;
        return players != null && players.Exists(player => player != null && player.player_id == key);
    }

    private bool IsGameStarted()
    {
        return WS_Client.Instance.GameData != null &&
            string.Equals(WS_Client.Instance.GameData.status, "playing", StringComparison.OrdinalIgnoreCase);
    }

    private void RemovePlayerMarker(string key)
    {
        if (minimapMarkersByKey.TryGetValue(key, out var marker))
        {
            if (marker != null)
            {
                GameObject.Destroy(marker.gameObject);
            }
            minimapMarkersByKey.Remove(key);
            LogController.Instance.debug($"[TowerGameController] Removed minimap marker for key={key}");
        }
    }

    private void CreateAnswerObject(WS_Client.AnswerData answer, Vector3 position)
    {
        if (answerPrefab == null)
        {
            LogController.Instance.debugError("answerPrefab is not assigned!");
            return;
        }

        var answerObj = GameObject.Instantiate(answerPrefab, this.globalParent);
        answerObj.name = "Answer_" + answer.id;

        // Use RectTransform for UI positioning

        Vector3 local = new Vector2(position.x, position.y);
        RectTransform rectTransform = answerObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(local.x, local.y);
            rectTransform.localScale = Vector3.one * (1f / this.clientMapScale);
        }
        else
        {
            answerObj.transform.position = local;
        }

        // Set text on TextMeshProUGUI component in child
        TextMeshProUGUI textComponent = answerObj.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = answer.content;
        }

        AnswerTrigger answerTrigger = answerObj.GetComponent<AnswerTrigger>();
        if (answerTrigger == null)
        {
            answerTrigger = answerObj.AddComponent<AnswerTrigger>();
        }
        answerTrigger.answerId = answer.id;
        answerTrigger.content = answer.content;

        answerObj.gameObject.SetActive(true);

        // Store the answer data
        answerObjectsById[answer.id] = answerObj;
        answers.Add(answer);
    }

    public void RemoveAnswerObject(int id)
    {
        if (answerObjectsById.TryGetValue(id, out var answerObj))
        {
            if (answerObj != null)
            {
                GameObject.Destroy(answerObj);
            }
            answerObjectsById.Remove(id);

            // Remove from list
            answers.RemoveAll(a => a.id == id);
        }

        // Also remove minimap marker if present
        if (minimapAnswerMarkersByKey.TryGetValue(id, out var answerMarker))
        {
            if (answerMarker != null)
            {
                Destroy(answerMarker.gameObject);
            }
            minimapAnswerMarkersByKey.Remove(id);
        }
    }

    public void OnAnswerObjectTrigger(int answerId)
    {
        // Find and update the answer in GameData
        var client = WS_Client.Instance;
        if (client != null && client.GameData?.answers != null)
        {
            WS_Client.AnswerData answer = client.GameData.answers.Find(a => a.id == answerId);
            if (answer != null)
            {
                answer.isOnPlayer = 1;
                LogController.Instance.debug($"Set answer {answerId} isOnPlayer to 1");

                _= WS_Client.Instance.updateAnswerOnPlayer(answerId);
            }
            else
            {
                Debug.LogWarning($"Answer {answerId} not found in GameData.answers");
            }
        }
    }

    public void checkAnswerVisibility()
    {
        foreach (GameObject answerObj in answerObjectsById.Values) {
            AnswerTrigger answerTrigger = answerObj.GetComponent<AnswerTrigger>();
            if (answerTrigger != null)
            {
                answerTrigger.checkAnswerVisibility();
            }
        }
    }

    private void CreateObstacleObject(WS_Client.ObstacleData obstacle, Vector3 position)
    {
        if (obstaclePrefab == null)
        {
            Debug.LogError("obstaclePrefab is not assigned!");
            return;
        }

        var obstacleObj = GameObject.Instantiate(obstaclePrefab, this.globalParent);
        obstacleObj.name = "Obstacle_" + obstacle.id;

        // 使用与answer完全相同的坐标转换逻辑
        Vector2 uiPosition = new Vector2(position.x, position.y);

        // 使用RectTransform进行UI定位 - 与answer相同
        RectTransform rectTransform = obstacleObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = uiPosition;
        }
        else
        {
            // 回退到世界坐标（如果不是UI元素）
            obstacleObj.transform.localPosition = new Vector3(uiPosition.x, uiPosition.y, 0f);
        }

        obstacleObj.gameObject.SetActive(true);

        // 存储障碍物数据 - 与answer相同的模式
        obstacleObjectsById[obstacle.id] = obstacleObj;
    }
    private void RemoveObstacleObject(int id)
    {
        if (obstacleObjectsById.TryGetValue(id, out var obstacleObj))
        {
            if (obstacleObj != null)
            {
                GameObject.Destroy(obstacleObj);
            }
            obstacleObjectsById.Remove(id);
        }
    }

    private IEnumerator HideYouWinAfterDelay(float delay)
    {
        AudioController.Instance?.PlayAudio(1);
        yield return new WaitForSeconds(delay);
        this.setGetScorePopup(false);
    }

    private IEnumerator HideYouLoseAfterDelay(float delay)
    {
        AudioController.Instance?.PlayAudio(2);
        yield return new WaitForSeconds(delay);
        this.setWrongPopup(false);
    }

    private void submitCorrectAnswerHandler()
    {
        this.setGetScorePopup(true);
        StartCoroutine(updateScoreUI());
        StartCoroutine(HideYouWinAfterDelay(3f));
    }

    private void submitWrongAnswerHandler()
    {
        try
        {
            var client = WS_Client.Instance;
            if (client == null || client.GameData == null || client.GameData.players == null)
            {
                LogController.Instance.debug("submitWrongAnswerHandler: missing GameData");
                return;
            }

            // Determine local UID safely
            int localUid = client.public_UserInfo != null ? client.public_UserInfo.uid : -1;

            // Determine whether the local player submitted a wrong answer.
            bool localSubmittedWrong = false;
            if (localUid != -1)
            {
                var localPlayer = client.GameData.players.Find(p => p.uid == localUid);
                if (localPlayer != null && localPlayer.answer_id != 0 && client.GameData.answers != null)
                {
                    var answer = client.GameData.answers.Find(a => a.id == localPlayer.answer_id);
                    if (answer != null)
                    {
                        // server's AnswerData.isCorrect == 0 -> wrong
                        localSubmittedWrong = (answer.isCorrect == 0);
                    }
                }
            }

            // Hide answer bubbles for players who shouldn't show them (same as before)
            foreach (WS_Client.PlayerData player in client.GameData.players)
            {
                if (player.isAnswerVisible == 0)
                {
                    CharacterController characterController = characterControllers.Find(c => c.UserId == player.uid);
                    if (characterController != null)
                    {
                        characterController.showAnswerBubble(0, "");
                    }
                }
            }

            // Show wrong popup only to the submitting (local) player
            if (localSubmittedWrong)
            {
                this.setWrongPopup(true);
                StartCoroutine(HideYouLoseAfterDelay(3f));
            }
            else
            {
                // ensure popup hidden for others
                this.setWrongPopup(false);
            }
        }
        catch (Exception ex)
        {
            LogController.Instance.debugError($"Error in submitWrongAnswerHandler: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void loadRoomList() {
        WS_Client.Instance.JoinGameRoom(0);
        LoaderConfig.Instance?.changeScene(1);
    }

    public void quitGame() {
        #if UNITY_EDITOR
            // In Unity Editor, stop Play mode
            UnityEditor.EditorApplication.isPlaying = false;
        #elif UNITY_WEBGL
            // For WebGL builds, use JavaScript to close the browser tab
            Application.ExternalEval("window.close();");
        #else
            // For standalone builds, quit the application
            Application.Quit();
        #endif
    }

    public void ready(bool ready) {
        if (ready) {
            WS_Client.Instance.ready();
        } else {
            WS_Client.Instance.cancelReady();
        }
    }

}
