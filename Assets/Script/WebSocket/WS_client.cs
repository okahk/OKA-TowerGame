using NativeWebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

public class WS_Client : MonoBehaviour
{
    private static WS_Client _instance;
    private static bool isQuitting = false;
    public static WS_Client Instance
    {
        get
        {
            // 如果实例不存在，尝试在场景中查找
            if (isQuitting) return null;
            if (_instance == null)
            {
                _instance = FindObjectOfType<WS_Client>();
                // 如果场景中也没有，就创建一个新的GameObject并挂载此组件
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(WS_Client).Name);
                    _instance = singletonObject.AddComponent<WS_Client>();
                }
            }
            return _instance;
        }
    }
    public WebSocket websocket;
    private string channelId = "towerGame";
    //uid = 543717
    // public string jwt = "eyJ0eXAiOiJqd3QiLCJhbGciOiJIUzI1NiJ9.eyJsb2dfZW5hYmxlZCI6IjEiLCJ0b2tlbiI6IjU0MzcxNy05ZjY3MjcwZDk1Zjc5NjEzMTMwNzU0MGEyNjUyMDdmN2Q0YWM5ZDU2OTM3OTBiMmNhNjhlNTQ5YzI5NjBkZmM5IiwiZXhwaXJlcyI6MTc2MjIyNDQ0NywicmVuZXdfZW5hYmxlZCI6MSwidGltZSI6IjIwMjUtMTAtMjggMDI6NDc6MjcgR01UIiwidWlkIjoiNTQzNzE3IiwidXNlcl9yb2xlIjoiMiIsInNjaG9vbF9pZCI6IjI3MiIsImlwIjoiMTY5LjI1NC4xMjkuNiIsInZlcnNpb24iOiIyLjguMzYiLCJkZXZpY2UiOiJ3aW5kb3dzIn0.tDcwbbY0OxfSCrrAMcneyvji2u5M7k5M8Moz7JQHiUU";
    public string jwt = "eyJ0eXAiOiJqd3QiLCJhbGciOiJIUzI1NiJ9.eyJsb2dfZW5hYmxlZCI6IjEiLCJ0b2tlbiI6IjUxMS00MzY0ZTlmYmE3NzA2M2Q4MjdjZWY0NjMzMGYwMjlhZmU2ZTIyNWZhOTk1MGMzMTRiMzRkNjAyNjY5NGUzYWIwIiwiZXhwaXJlcyI6MTc2MzYxMDE0NywidGltZSI6IjIwMjUtMTAtMjEgMTE6NDI6MjciLCJ1aWQiOiI1MTEiLCJ1c2VyX3JvbGUiOiIzIiwic2Nob29sX2lkIjoiMjcyIiwiaXAiOiIxNjkuMjU0LjEyOS40IiwidmVyc2lvbiI6IjIuOC4zNiIsImRldmljZSI6Im1hYyJ9.LT8f4UNEB3nnW6BY2FMPQXZVMUzQ-6NyCJT08gqSx1s";
    private string roomId = "";
    private string player_id = "";
    public string pendingReconnectRoomId = "";
    public string pendingOrder = "";
    public int pendingReconnectUid = -1;

    // New: flag set when server informs same-account connection (another device)
    // Consumers (UI/controllers) can read this to block actions or show UI.
    public bool IsLoggedInElsewhere = false;

    // const string WEBSHOCKET_URL = "wss://ws.openknowledge.hk:8084";//dev : "wss://ws.openknowledge.hk:8084";  // prod : "wss://ws.openknowledge.hk";
    public string localhostUrl = "ws://localhost:8000/";
    public string localhostUrl_copy = "ws://localhost:8000/";
    public string developmentUrl = "wss://ws.openknowledge.hk:8084";
    public string uatUrl = "wss://ws.openknowledge.hk:8082";
    public string productionUrl = "wss://ws.openknowledge.hk";
    const string WS_API_BASE_URL = "https://ws.openknowledge.hk/api/towerGame";//"https://ws.openknowledge.hk:8084/api/metaverse";//"https://ws.openknowledge.hk/api/metaverse";
    public const int TIMEOUT_TIMELIMIT = 15;
    // flag set by WS thread when "test" message received
    public static volatile bool testReceived = false;
    public static volatile bool gameDataReceived = false;
    private bool isJoining = false;
    private float lastJoinTime = 0f;
    private const float JOIN_COOLDOWN = 1f; // 1秒冷却时间
    public UserInfo userInfo;
    private bool isSendingPosition = false;
    
    // Disconnection timeout tracking
    private float disconnectionStartTime = -1f;
    private const float DISCONNECTION_TIMEOUT = 5f; // 5 seconds
    private bool hasCalledDisconnected = false;
    
    // Reconnection tracking
    private float lastReconnectAttemptTime = 0f;
    private const float RECONNECT_COOLDOWN = 5f; // 5 seconds between reconnect attempts

    // Event system for order changes
    public delegate void OrderChangedHandler(string newOrder);
    public event OrderChangedHandler OnOrderChanged;
    private string overrideWebsocketBaseUrl = null;
    private static string cachedDomainName;

    // Event invoked when server startCountDown changes
    public event Action<int> OnStartCountDownChanged;
    public event Action<bool> CancelStartCountDown;
    public bool isAllPlayersReady;
    public CanvasGroup duplicatedLoginBox;


    // 新增公共属性，作为访问私有字段的受控接口
    public UserInfo public_UserInfo
    {
        get { return userInfo; }
        set { userInfo = value; }
    }
    // 私有静态字段，用于实际存储数据
    public RoomGameData _gameData;

    // 公共静态属性，供其他类访问
    public RoomGameData GameData
    {
        get { return _gameData; }
        set { _gameData = value; }
    }

    public static List<RoomInfo> _roomList;

    public List<RoomInfo> RoomList
    {
        get { return _roomList; }
        set { _roomList = value; }
    }

    [Serializable]
    public class UserInfo
    {
        public int uid;
        public string cname;
        public string ename;
        public int gender;
        public int user_role;
        public int school_id;
        public string classno;
        public string nickname;
        public string wsId;
        public string channelId;
        public string[] roomIds;
    }
    [Serializable]
    private class MessageContent
    {
        public string action;
        public string position;
        public string destination;
        public int answer_id;
        public int timer;
    }

    [Serializable]
    private class OutMessage
    {
        public string messageType;
        public MessageContent content;
        public string roomId;
    }

    [System.Serializable]
    public class WebSocketMessage
    {
        public string fromWsId;
        public int fromUid;
        public string messageType;
        public string roomId;
        // 可以根据需要添加其他字段
        public string data;
        public ServerMessageContent content;
        public long time;
    }

    public class PositionData
    {
        public float x;
        public float y;
    }

    [System.Serializable]
    public class ServerMessageContent
    {
        public RoomGameData roomGameData;
        // 如果需要，你也可以定义members字段
        // public List<RoomMember> members;
        public string roomId;
        public UserInfo userInfo;
        public UserInfo added;
        public string message;
        public List<RoomInfo> roomList;

        public string order; // "addPlayer" , "reconnectPlayer , "removePlayer" , "startGame" , "endGame" , "resetGame" , "nextRound" , "getAnswer" , "submitCorrectAnswer" , "submitWrongAnswer"
    }

    [System.Serializable]
    public class RoomInfo
    {
        public string roomId;
        public int roomMembers;
        public int roomPlayers;
        public string roomStatus;
    }

    [System.Serializable]
    public class RoomGameData
    {
        public List<PlayerData> players;
        public List<QuestionData> questions;
        public List<AnswerData> answers;
        public List<ObstacleData> obstacles;
        public List<int> teamScore; // [0,0] 
        public string status; // waiting / playing
        public int gameTimer; // 0 - 180
        public int round; // 1 - 10
        public int startCountDown;
    }

    [System.Serializable]
    public class PlayerData
    {
        public string player_id;
        public string ename;
        public string cname;
        public int uid;
        public string status;
        public float[] position;
        public string costume_id;
        // public int dir;
        public float[] destination;
        public int isAnswerVisible;
        public string answerContent;
        public int answer_id;
        public int score;
    }

    [System.Serializable]
    public class QuestionData
    {
        public int id;
        public string content;
        public float[] position;
        public int score;
        public string questionType; // "text" , "picture" , "audio" , "fillInBlank"
        public string[] media;
    }

    [System.Serializable]
    public class AnswerData
    {
        public int id;
        public string content;
        // public string question_id;
        public float[] position;
        public int isOnPlayer;
        // public int isSubmitted;
        public int isCorrect;
    }
    [System.Serializable]
    public class ObstacleData
    {
        public int id;
        public float[] position;
    }

    // Dummy data for testing (based on expected server format)
    private List<QuestionData> dummyQuestions = new List<QuestionData>
    {
        new QuestionData { id = 1, content = "Question 1:XXXXX" },
        new QuestionData { id = 2, content = "Question 2:XXXXX" },
        new QuestionData { id = 3, content = "Question 3:XXXXX" },
        new QuestionData { id = 4, content = "Question 4:XXXXX" }
    };

    private List<AnswerData> dummyAnswers = new List<AnswerData>
    {
        new AnswerData { id = 1, content = "Answer 1", position = new float[] { -1f, 0f }, isOnPlayer = 0},
        new AnswerData { id = 2, content = "Answer 2", position = new float[] { 0f, 1f }, isOnPlayer = 0},
        new AnswerData { id = 3, content = "Answer 3", position = new float[] { 1f, 0f }, isOnPlayer = 0},
        new AnswerData { id = 4, content = "Answer 4", position = new float[] { 0f, -1f }, isOnPlayer = 0}
    };

    // 如果需要处理members，可以定义此类
    [System.Serializable]
    public class RoomMember
    {
        public int uid;
        public string cname;
        public string ename;
        public int gender;
        public int user_role;
        public int school_id;
        public string classno;
        public string nickname;
        public string wsId;
        public string channelId;
        public string[] roomIds;
    }



    public static string GetCurrentDomainName
    {
        get
        {
            if (!string.IsNullOrEmpty(cachedDomainName)) return cachedDomainName;
#if UNITY_EDITOR
            cachedDomainName = "dev";
#else
            string absoluteUrl = Application.absoluteURL;
            Uri url = new Uri(absoluteUrl);
            cachedDomainName = url.Host;
#endif
            return cachedDomainName;
        }
    }

    public string GetCurrentUrl
    {
        set
        {
            overrideWebsocketBaseUrl = value;
        }
        get
        {
            if (!string.IsNullOrEmpty(overrideWebsocketBaseUrl))
                return overrideWebsocketBaseUrl;

#if UNITY_EDITOR
            return localhostUrl;
#else
        string currentDomain = GetCurrentDomainName.ToLower();

        // localhost to localhost websocket
        if (currentDomain == "localhost")
        {
            return localhostUrl;
        }

        // 环境检测逻辑：如果域名以"dev"开头，则使用开发服务器
        if (currentDomain.StartsWith("dev"))
        {
            return developmentUrl;
        }
        else if(currentDomain.StartsWith("uat"))
        {
            return uatUrl;
        }
        else
        {
            return productionUrl;
        }
#endif
        }
    }

    void Start()
    {

        // GameData.questions = dummyQuestions;
        // GameData.answers = dummyAnswers;
    }

    public void Connect()
    {
        this.Connect(()=>
        {
            try
            {
                // Defensive checks: ensure GameData, players and user info exist before accessing
                if (this.GameData?.players == null || this.public_UserInfo == null)
                {
                    Debug.LogWarning("WS_Client: GameData.players or public_UserInfo is null, skipping ready button activation.");
                    return;
                }

                var me = this.GameData.players.Find(p => p.uid == this.public_UserInfo.uid);
                if (me == null)
                {
                    Debug.LogWarning($"WS_Client: local player (uid={this.public_UserInfo.uid}) not found in GameData.players.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WS_Client: exception in onConnect callback: {ex.Message}");
            }
        });
    }

    private void Awake()
    {
        // 确保单例在场景切换时不被销毁，且实例唯一
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject); // 可选：如需跨场景保持连接
        }

        // 在这里初始化WebSocket连接
        // InitializeWebSocket();
    }

    public async void Connect(Action onConnectCompleted = null)
    {
        if (isQuitting || this == null)
        {
            Debug.Log("WS_Client.Connect skipped because object is destroyed or application is quitting.");
            return;
        }

        Debug.Log("Connect: " + GetCurrentUrl);
        // Cancel any existing repeating invokes to prevent duplicates
        try { CancelInvoke("SendTest"); } catch { }
        try { CancelInvoke("ConstantSyncData"); } catch { }

        // Close existing websocket if it exists
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            Debug.Log("关闭现有WebSocket连接...");
            await websocket.Close();
            websocket = null;
        }
        
        // var baseUrl = WEBSHOCKET_URL; // "wss://ws.openknowledge.hk"
        // // *********************************************
        // var baseUrl = "ws://localhost:8000/"; // comment when build and deploy
        // *********************************************
        var baseUrl = GetCurrentUrl;

        var query = "?channelId=" + Uri.EscapeDataString(channelId) + "&jwt=" + Uri.EscapeDataString(jwt);

        var fullUrl = baseUrl + query;
        websocket = new WebSocket(fullUrl);
        Debug.Log("WebSocket URL: " + fullUrl);

        websocket.OnOpen += OnWebSocketOpen;

        websocket.OnError += (e) =>
        {
            Debug.Log("Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
            try
            {
                disconnected();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Error invoking disconnected handler: " + ex.Message);
            }
        };

        websocket.OnMessage += (bytes) =>
        {
            try
            {
                // 将字节数组转换为字符串
                var jsonString = System.Text.Encoding.UTF8.GetString(bytes);

                // 将JSON字符串反序列化为对象
                WebSocketMessage message = JsonUtility.FromJson<WebSocketMessage>(jsonString);

                string receivedOrder = message.content?.order;
                Debug.Log("Received order: " + receivedOrder + " | messageType: " + message.messageType + " | roomId: " + message.roomId);
                if (receivedOrder == "resetGame" || receivedOrder == "endGame" ||
                    message.messageType == "resetGame" || message.messageType == "endGame")
                {
                    pendingReconnectRoomId = "";
                }

                // 现在可以安全地访问messageType属性
                switch (message.messageType)
                {
                    case "roomInfo":
                        Debug.Log("roomInfo : " + jsonString);
                        roomId = message.roomId;
                        if (!string.IsNullOrEmpty(roomId) && roomId != "lobby")
                        {
                            pendingReconnectRoomId = roomId;
                        }
                        break;
                    case "listGameRoom":
                        Debug.Log("listGameRoom : " + jsonString);
                        RoomList = message.content.roomList;
                        break;
                    case "roomFull":
                        Debug.Log("roomFull : " + jsonString);
                        break;
                    case "SyncRoomData":
                        debugLogPerSecond("OnMessage! " + jsonString);
                        //Debug.Log(jsonString);
                        this.GameData = message.content.roomGameData;
                        this.OnStartCountDownChanged?.Invoke(this.GameData.startCountDown);
                        //Debug.Log($"SyncRoomData: ready button startCountDown = {this.GameData.startCountDown}");
                        if (!string.IsNullOrEmpty(message.content.order))
                        {
                            //Debug.LogWarning("Order received: " + message.content.order);
                            Debug.LogWarning("OnMessage! " + jsonString);                  
                            // Store order for scenes that haven't loaded yet
                            pendingOrder = message.content.order;

                            if (message.content.order == "reconnectPlayer")
                            {
                                int parsedUid = -1;

                                // 1) Prefer the explicit 'added' object (server sample shows content.added.uid)
                                if (message.content.added != null && message.content.added.uid > 0)
                                {
                                    parsedUid = message.content.added.uid;
                                    Debug.Log($"SyncRoomData: parsed reconnect uid from content.added: {parsedUid}");
                                }
                                // 2) Fallback to content.userInfo (older messages)
                                else if (message.content.userInfo != null && message.content.userInfo.uid > 0)
                                {
                                    parsedUid = message.content.userInfo.uid;
                                    Debug.Log($"SyncRoomData: parsed reconnect uid from content.userInfo: {parsedUid}");
                                }
                                else
                                {
                                    // 3) Last resort: extract from raw JSON (handles "uid":123 or "uid":"123")
                                    try
                                    {
                                        var m = Regex.Match(jsonString, "\"added\"\\s*:\\s*\\{[^}]*\"uid\"\\s*:\\s*\"?(\\d+)\"?", RegexOptions.Singleline);
                                        if (m.Success && int.TryParse(m.Groups[1].Value, out int uidFromJson))
                                        {
                                            parsedUid = uidFromJson;
                                            Debug.Log($"SyncRoomData: extracted reconnect uid from raw JSON: {parsedUid}");
                                        }
                                        else
                                        {
                                            Debug.LogWarning("SyncRoomData: could not extract reconnect uid from raw JSON.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogWarning($"SyncRoomData: regex extraction failed: {ex.Message}");
                                    }
                                }

                                // Assign only when parsedUid is valid
                                if (parsedUid > 0)
                                {
                                    pendingReconnectUid = parsedUid;
                                    //Debug.LogError($"Received reconnectPlayer order for UID {pendingReconnectUid}");
                                }
                                else
                                {
                                    pendingReconnectUid = -1;
                                   // Debug.LogWarning("Received reconnectPlayer order but UID is invalid (set to -1).");
                                }
                            }

                            // Fire the event to notify subscribers
                            //Debug.Log("Order received WS_Client: " + message.content.order);
                            OnOrderChanged?.Invoke(message.content.order);
                        }
      
                        gameDataReceived = true;
                        break;
                    case "ready":
                        this.userInfo = message.content.userInfo;
                        //this.GameData = message.content.roomGameData;
                        break;
                    case "cancelReady":
                        this.userInfo = message.content.userInfo;
                        this.CancelStartCountDown?.Invoke(false);
                        //this.GameData = message.content.roomGameData;
                        break;
                    case "inPlayingRoom":
                        Debug.LogWarning("inPlayingRoom : " + jsonString);
                        pendingReconnectRoomId = message.content.roomId;
                        roomId = pendingReconnectRoomId;
                        break;
                    case "connectionClose":
                        // Server can send this when the same account connects from another device:
                        // member.sendMsg({ fromWsId: member.userInfo.wsId, messageType: 'connectionClose', content: { message: `same account connected to server` } });
                        Debug.Log("connectionClose : " + jsonString);
                        try
                        {
                            // Defensive checks and simple pattern match for server's text
                            if (message.content != null && !string.IsNullOrEmpty(message.content.message))
                            {
                                string txt = message.content.message.ToLowerInvariant();
                                Debug.Log("connectionClose message content: " + txt);
                                if (txt.Contains("same account") || txt.Contains("same account connected") || txt.Contains("connected to server"))
                                {
                                    IsLoggedInElsewhere = true;
                                    pendingOrder = "connectionClose";
                                    if(this.duplicatedLoginBox != null)
                                    {
                                        this.duplicatedLoginBox.alpha = 1;
                                        this.duplicatedLoginBox.interactable = true;
                                        this.duplicatedLoginBox.blocksRaycasts = true;
                                    }

                                    try
                                    {
                                        OnOrderChanged?.Invoke("connectionClose");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.Log("OnOrderChanged invoke failed: " + ex.Message);
                                    }

                                    // Optionally close local socket to match server intent.
                                    // Fire-and-forget close so we don't block message processing.
                                    //try { _ = websocket?.Close(); } catch { }
                                }
                                else
                                {
                                    Debug.Log("connectionClose received (unrecognized text): " + message.content.message);
                                }
                            }
                            else
                            {
                                Debug.Log("connectionClose received with empty content.message");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("Error handling connectionClose: " + ex.Message);
                        }
                        break;
                    case "test":
                        testReceived = true;                        
                        break;
                    default:
                        Debug.Log("Unhandled messageType: " + message.messageType);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error processing message: " + e.Message);
            }
        };

        // // waiting for messages\
        await websocket.Connect();

        try
        {
            OnOrderChanged?.Invoke("reconnected");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to invoke reconnected order: " + ex.Message);
        }

        if (!isQuitting && this != null && gameObject != null)
        {
            try
            {
                InvokeRepeating("SendTest", 0.0f, 5f);
                InvokeRepeating("ConstantSyncData", 0.0f, 0.1f);
            }
            catch (MissingReferenceException)
            {
                // object destroyed between connect and invoke scheduling
                Debug.LogWarning("WS_Client destroyed before InvokeRepeating could be scheduled.");
            }
        }


        onConnectCompleted?.Invoke();
    }

    public void BackToLogin()
    {
        try
        {
            Debug.Log(GetCurrentDomainName);
#if !UNITY_EDITOR
            string login_url = $"https://{GetCurrentDomainName}/login";
            string javascript = $@"
                    window.location.replace('{login_url}');
                ";
            Application.ExternalEval(javascript);
            return;
#else
            Debug.Log("Exit editor: Close the Editor to change another jwt account");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning("BackToLogin failed: " + ex.Message);
        }
    }



    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        // Only dispatch messages if websocket exists and is valid
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
        if (GetCurrentDomainName.StartsWith("dev"))
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                _ = updateAnswerOnPlayer(1); // 玩家拾取答案
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                _ = submitAnswer(4); // 玩家提交答案
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                _ = ready(); // 玩家準備
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                _ = cancelReady(); // 取消準備
            }
            if (Input.GetKeyDown(KeyCode.T))
            {
                _ = setTimer(3);
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                _ = resetGame(); // for DEV 重置遊戲
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                _ = nextRound(); // for DEV 下一回合
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                _ = startGame(); // for DEV 開始遊戲
            }
            if (Input.GetKeyDown(KeyCode.J))
            {
                JoinGameRoom(1); // for DEV JoinRoom
            }
        }
        
        // Auto-reconnect logic - only try to reconnect if we're in the game scene (not lobby)
        bool needsReconnect = false;
        
        // Check if websocket is null or not in Open state
        if (websocket == null)
        {
            needsReconnect = true;
        }
        else if (websocket.State != WebSocketState.Open && websocket.State != WebSocketState.Connecting)
        {
            needsReconnect = true;
        }
        
        if (needsReconnect)
        {
            // Check if enough time has passed since last reconnect attempt
            if (Time.time - lastReconnectAttemptTime >= RECONNECT_COOLDOWN)
            {
                Debug.Log($"WebSocket needs reconnection, attempting to connect...");
                lastReconnectAttemptTime = Time.time;
                
                try
                {
                    // Stop any existing repeating invokes before reconnecting
                    CancelInvoke("SendTest");
                    CancelInvoke("ConstantSyncData");
                    
                    // Reconnect
                    Connect();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error during WebSocket reconnection: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    public bool IsAllPlayersReady
    {
        get
        {
            if (GameData?.players == null || GameData.players.Count == 0)
            {
                this.isAllPlayersReady = false;
                return false;
            }

            this.isAllPlayersReady = GameData.players.All(p => p.status == "ready");
            return this.isAllPlayersReady;
        }
    }

    public void printGameData()
    {
        // if (GameData.players != null) {
        //     foreach (var player in GameData.players) {
        //         Debug.Log($"printGameData Player: {player.uid} - {player.costume_id}");
        //     }
        // }
        // if (GameData.answers != null) {
        //     foreach (var answer in GameData.answers) {
        //         Debug.Log($"Answer: {answer.id} - {answer.content} - {answer.isOnPlayer}");
        //     }
        // }
        // Debug.Log($"========================");
    }

    public void JoinGameRoom(int roomId = 1)
    {
        Debug.Log("JoinGameRoom: " + roomId);
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.Log(websocket);
            Debug.LogWarning("WebSocket not connected! Cannot join room.");
            return;
        }
        if (!isJoining)
        {
            lastJoinTime = Time.time;
            isJoining = true;
            _ = JoinRoomAsync(roomId);
        }
    }

    // 连接打开后
    private async void OnWebSocketOpen()
    {
        Debug.Log("WebSocket连接成功！");
        
        // Reset reconnection timer on successful connection
        lastReconnectAttemptTime = 0f;
        
        try
        {
            // await JoinRoom(); // 调用一次 JoinRoom
            await ListGameRoom();

            // Rejoin the room held before the socket disconnected. Only use the
            // lobby when there is no active game room to restore.
            string roomToRestore = !string.IsNullOrEmpty(pendingReconnectRoomId)
                ? pendingReconnectRoomId
                : roomId;

            if (!string.IsNullOrEmpty(roomToRestore) && roomToRestore != "lobby")
            {
                await JoinRoom(roomToRestore);
            }
            else
            {
                JoinGameRoom(0);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to join room: {ex.Message}");
        }

    }

    public async Task ListGameRoom()
    {
        var msg = new OutMessage
        {
            messageType = "listGameRoom",
            content = new MessageContent { action = "listGameRoom" }
        };

        string jsonString = JsonUtility.ToJson(msg);

        await websocket.SendText(jsonString);
    }

    private async Task JoinRoomAsync(int roomId = 1)
    {
        try
        {
            // 这里替换为你的实际加入房间逻辑
            await JoinRoom(roomId);
            // Debug.Log("Room joined successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Join room failed: {e.Message}");
        }
        finally
        {
            isJoining = false;
        }
    }

    public async Task JoinRoom(int roomId = 1)
    {
        string roomIdString = roomId == 0 ? "lobby" : "room" + roomId.ToString();
        await JoinRoom(roomIdString);
    }

    private async Task JoinRoom(string roomIdString)
    {
        var msg = new OutMessage
        {
            messageType = "joinRoom",
            content = new MessageContent { action = "joinRoom" },
            roomId = roomIdString
        };

        string jsonString = JsonUtility.ToJson(msg);
        await websocket.SendText(jsonString);
    }

    async void SendTest()
    {
        // Check if websocket is valid and open before sending
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            var msg = new OutMessage
            {
                messageType = "handleMessage",
                content = new MessageContent { action = "test" }
            };

            string jsonString = JsonUtility.ToJson(msg);
            await websocket.SendText(jsonString);
        }
        catch (System.ObjectDisposedException)
        {
            Debug.LogWarning("WebSocket disposed, cannot send test message");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error sending test message: {e.Message}");
        }
    }

    void disconnected()
    {
        pendingOrder = "disconnected";
        Debug.LogWarning("WebSocket disconnected, invoking OnOrderChanged with 'disconnected'");
        OnOrderChanged?.Invoke("disconnected");
    }

    async void ConstantSyncData()
    {
        if (isSendingPosition) return;
        
        try
        {
            // 检查WebSocket连接状态
            if (websocket?.State != WebSocketState.Open)
            {
                // Debug.Log("WebSocket未连接，无法发送位置同步数据！");
                return;
            }

            // 检查是否已加入房间
            if (string.IsNullOrEmpty(roomId) || roomId == "lobby")
            {
                // Debug.Log("未加入有效房间，跳过位置同步");
                return;
            }

            // 1. 从本地GameData中获取当前玩家的数据
            if (GameData?.players == null || userInfo == null)
            {
                // Debug.Log("GameData或players列表为空，无法同步位置");
                return;
            }

            // 获取当前玩家的UID
            int currentPlayerUid = this.userInfo.uid;

            // 在本地数据中查找当前玩家
            PlayerData myPlayer = GameData.players.FirstOrDefault(p => p.uid == currentPlayerUid);

            if (myPlayer == null)
            {
                return;
            }
            
            // Validate position and destination arrays
            if (myPlayer.position == null || myPlayer.position.Length < 2 ||
                myPlayer.destination == null || myPlayer.destination.Length < 2)
            {
                Debug.LogWarning($"Player {currentPlayerUid} has invalid position or destination data");
                return;
            }

            // 2. 准备位置数据
            PositionData positionData = new PositionData
            {
                x = myPlayer.position[0],
                y = myPlayer.position[1],
            };

            PositionData destinationData = new PositionData
            {
                x = myPlayer.destination[0],
                y = myPlayer.destination[1]
            };

            // 3. 发送位置更新到服务器
            await UpdateServerPosition(positionData, destinationData);

        }
        catch (System.ObjectDisposedException)
        {
            Debug.Log("WebSocket已关闭，停止位置同步");
            isSendingPosition = false;
        }
        catch (System.Exception e)
        {
            Debug.Log($"位置同步失败: {e.Message}");
            isSendingPosition = false;
        }
        finally
        {
            isSendingPosition = false;
        }
    }

    public async Task UpdateServerPosition(PositionData position, PositionData destination)
    {
        isSendingPosition = true;
        if (websocket?.State == WebSocketState.Open)
        {
            try
            {
                var msg = new OutMessage
                {
                    messageType = "UpdateServerPosition",
                    content = new MessageContent
                    {
                        action = "UpdateServerPosition",
                        // 将坐标转换为类似 "[x, y, dir]" 的字符串格式
                        position = $"[{position.x}, {position.y}]",
                        destination = $"[{destination.x}, {destination.y}]"
                    }
                };

                string jsonString = JsonUtility.ToJson(msg);
                await websocket.SendText(jsonString);
            }
            catch (System.ObjectDisposedException)
            {
                Debug.LogWarning("WebSocket已关闭，无法发送位置更新");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"发送位置更新失败: {e.Message}");
            }
        }
        else
        {
            Debug.Log("WebSocket未连接！");
                        
            // Start tracking disconnection time
            if (disconnectionStartTime < 0)
            {
                disconnectionStartTime = Time.time;
                hasCalledDisconnected = false;
            }
            
            // Check if disconnected for more than 5 seconds
            float disconnectedDuration = Time.time - disconnectionStartTime;
            if (disconnectedDuration >= DISCONNECTION_TIMEOUT && !hasCalledDisconnected)
            {
                Debug.LogWarning($"WebSocket断开连接超过 {DISCONNECTION_TIMEOUT} 秒，调用 disconnected()");
                hasCalledDisconnected = true;
                disconnected();
            }
            
            return;
        }
        
        // Reset disconnection tracking when connection is restored
        if (disconnectionStartTime >= 0)
        {
            disconnectionStartTime = -1f;
            hasCalledDisconnected = false;
        }
    }

    public void UpdatePlayerPositionInGameData(int playerUid, float[] newPosition, float[] newDestination = null)
    {
        // 确保 GameData 和玩家列表不为空
        if (GameData?.players == null)
        {
            Debug.Log("尝试更新玩家位置时，GameData 或 players 为 null。");
            return;
        }

        // 查找指定UID的玩家
        var playerToUpdate = GameData.players.FirstOrDefault(p => p.uid == playerUid);
        if (playerToUpdate != null)
        {
            // 更新玩家位置
            playerToUpdate.position = newPosition; // 例如 [1.5f, 2.3f]

            // 如果提供了新目的地，则一并更新
            if (newDestination != null)
            {
                playerToUpdate.destination = newDestination;
            }

            Debug.Log($"已更新本地玩家数据: UID {playerUid} 位置 -> [{newPosition[0]}, {newPosition[1]}]");
        }
        else
        {
            Debug.Log($"在 GameData 中未找到 UID 为 {playerUid} 的玩家。");
        }
    }

    public async Task updateAnswerOnPlayer(int answer_id)
    {
        isSendingPosition = true;
        if (websocket?.State == WebSocketState.Open)
        {
            var msg = new OutMessage
            {
                messageType = "UpdateAnswerOnPlayer",
                content = new MessageContent
                {
                    action = "UpdateAnswerOnPlayer",
                    answer_id = answer_id
                }
            };

            string jsonString = JsonUtility.ToJson(msg);
            await websocket.SendText(jsonString);
            Debug.Log($"玩家取得答案: {jsonString}");
        }
        else
        {
            Debug.LogWarning("WebSocket未连接！");
        }
    }

    public async Task submitAnswer(int answer_id)
    {
        isSendingPosition = true;
        if (websocket?.State == WebSocketState.Open)
        {
            var msg = new OutMessage
            {
                messageType = "submitAnswer",
                content = new MessageContent
                {
                    action = "submitAnswer",
                    answer_id = answer_id
                }
            };

            string jsonString = JsonUtility.ToJson(msg);
            await websocket.SendText(jsonString);
        }
        else
        {
            Debug.LogWarning("WebSocket未连接！");
        }
    }

    public async Task setTimer(int timer)
    {
        isSendingPosition = true;
        if (websocket?.State == WebSocketState.Open)
        {
            var msg = new OutMessage
            {
                messageType = "setTimer",
                content = new MessageContent
                {
                    action = "setTimer",
                    timer = timer
                }
            };

            string jsonString = JsonUtility.ToJson(msg);
            await websocket.SendText(jsonString);
        }
        else
        {
            Debug.LogWarning("WebSocket未连接！");
        }
    }

    public async Task sendAction(string action)
    {
        isSendingPosition = true;
        if (websocket?.State == WebSocketState.Open)
        {
            var msg = new OutMessage
            {
                messageType = action,
                content = new MessageContent
                {
                    action = action
                }
            };

            string jsonString = JsonUtility.ToJson(msg);
            await websocket.SendText(jsonString);
            Debug.Log($"sendAction: {jsonString}");
        }
        else
        {
            Debug.LogWarning("WebSocket未连接！");
        }
    }
    public Task ready()
    {
        _ = sendAction("ready");
        this.SetLocalReady(true);
        return Task.CompletedTask;
    }
    public Task cancelReady()
    {
        _ = sendAction("cancelReady");
        this.SetLocalReady(false);
        return Task.CompletedTask;
    }
    public Task startGame()
    {
        _ = sendAction("startGame");
        return Task.CompletedTask;
    }

    public Task nextRound()
    {
        _ = sendAction("nextRound");
        return Task.CompletedTask;
    }
    public Task resetGame()
    {
        pendingReconnectRoomId = "";
        _ = sendAction("resetGame");
        return Task.CompletedTask;
    }

    private void SetLocalReady(bool ready)
    {
        try
        {
            if (GameData?.players == null || public_UserInfo == null) return;

            var localPlayer = GameData.players.Find(p => p.uid == public_UserInfo.uid);
            if (localPlayer != null)
            {
                localPlayer.status = ready ? "ready" : "waiting";
            }

            // Notify UI listeners immediately
            try { OnOrderChanged?.Invoke(ready ? "localReady" : "localCancelReady"); } catch { }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SetLocalReady failed: {ex.Message}");
        }
    }

    async void SendWebSocketMessage()
    {
        if (websocket.State == WebSocketState.Open)
        {
            // Sending bytes
            await websocket.Send(new byte[] { 10, 20, 30 });

            // Sending plain text
            await websocket.SendText("plain text message");
        }
    }

    private async void OnDestroy()
    {
        isQuitting = true;
        // Cancel repeated invokes
        try { CancelInvoke("SendTest"); } catch { }
        try { CancelInvoke("ConstantSyncData"); } catch { }

        if (websocket != null)
        {
            try
            {
                // unsubscribe the handler we added
                websocket.OnOpen -= OnWebSocketOpen;
                // Other handlers added with lambdas cannot be unsubscribed here normally.
            }
            catch { }

            // Close socket safely
            try
            {
                if (websocket.State == WebSocketState.Open)
                {
                    await websocket.Close();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing websocket in OnDestroy: {e.Message}");
            }

            websocket = null;
        }

        // Clear singleton if this was the instance
        if (_instance == this)
        {
            _instance = null;
        }

        Debug.Log("WS_Client destroyed, invokes canceled, and WebSocket closed.");
    }

    private async void OnApplicationQuit()
    {
        isQuitting = true;
        try
        {
            CancelInvoke("SendTest");
            CancelInvoke("ConstantSyncData");
        }
        catch { }

        if (websocket != null)
        {
            try { await websocket.Close(); }
            catch { }
        }
    }

    public void setReady(bool state) {
        // string newStatus = state ? "ready" : "waiting";
        // if (GameData != null && GameData.players != null) {
        //     PlayerData player = GameData.players.FirstOrDefault(p => p.uid == public_UserInfo.uid);
        //     if (player != null) {
        //         Debug.Log("setReady: " + newStatus);
        //         player.status = newStatus;
        _ = sendAction("ready");
        //     }
        // }
    }

    private float lastLogTime = 0f;
    private void debugLogPerSecond(string message, string type = "debug")
    {
        if (Time.time - lastLogTime >= 5f)
        {
            switch (type)
            {
                case "debug":
                    Debug.Log(message);
                    break;
                case "warning":
                    Debug.LogWarning(message);
                    break;
                case "error":
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
            lastLogTime = Time.time;
        }
    }
}