using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class scoreboardController : MonoBehaviour
{
    public string key;
    private Texture2D icon;
    private string text;
    public bool isReady = false;

    [SerializeField]
    private GameObject rootObject;
    [SerializeField]
    private GameObject readyObject;
    [SerializeField]
    private GameObject notReadyObject;
    [SerializeField]
    private GameObject iconObject;
    [SerializeField]
    private GameObject textObject;
    // Start is called before the first frame update

    private Image iconImage;
    private TextMeshProUGUI textComponent;
    private CanvasGroup rootCanvasGroup;

    [SerializeField]
    private CanvasGroup localPlayerIndicator;
    [SerializeField]
    private CanvasGroup wifiDisconnectedIndicator;

    private bool isWifiDisconnected;

    void Awake()
    {
        this.iconImage = this.iconObject.GetComponent<Image>();
        this.textComponent = this.textObject.GetComponent<TextMeshProUGUI>();
        
        // Get or add CanvasGroup to rootObject for visibility control
        this.rootCanvasGroup = this.rootObject.GetComponent<CanvasGroup>();
        if (this.rootCanvasGroup == null)
        {
            this.rootCanvasGroup = this.rootObject.AddComponent<CanvasGroup>();
        }
        resetScoreboard();
        DontDestroyOnLoad(this.gameObject);
    }
    
    public void setScoreboard(string key, Texture2D icon, string text = "")
    {
        Debug.Log("setScoreboard: key=" + key + ", icon=" + icon + ", text=" + text);
        this.key = key;
        this.icon = icon;
        this.text = text;
        this.isReady = false;
        this.iconImage.sprite = this.icon != null ? SetUI.ConvertTextureToSprite(this.icon) : null;
        this.iconObject.SetActive(this.icon != null);
        this.textComponent.text = this.text;
        this.isWifiDisconnected = false;
        SetIndicatorAlpha(this.wifiDisconnectedIndicator, 0f);
        this.rootCanvasGroup.alpha = 1f;
    }

    public void setDisconnected(bool disconnected)
    {
        this.isWifiDisconnected = disconnected;
        SetIndicatorAlpha(this.wifiDisconnectedIndicator, disconnected ? 1f : 0f);
    }

    public void resetScoreboard()
    {
        this.key = "";
        this.icon = null;
        this.text = "";
        this.isReady = false;
        this.isWifiDisconnected = false;
        this.iconObject.SetActive(false);
        this.rootCanvasGroup.alpha = 0f;
        SetIndicatorAlpha(this.localPlayerIndicator, 0f);
        SetIndicatorAlpha(this.wifiDisconnectedIndicator, 0f);
    }

    // Update is called once per frame
    void Update()
    {
        if (string.IsNullOrEmpty(key)) return;

        var client = WS_Client.Instance;
        var players = client != null && client.GameData != null ? client.GameData.players : null;
        var player = players != null ? players.Find(p => p != null && p.player_id == key) : null;

        SetIndicatorAlpha(localPlayerIndicator,
            player != null && client.public_UserInfo != null && player.uid == client.public_UserInfo.uid ? 1f : 0f);
        SetIndicatorAlpha(wifiDisconnectedIndicator, isWifiDisconnected ? 1f : 0f);

        if (player == null) return;

        if (player.status == "ready")
        {
            readyObject.SetActive(true);
            notReadyObject.SetActive(false);
        }
        else if (player.status == "waiting")
        {
            readyObject.SetActive(false);
            notReadyObject.SetActive(true);
        }
        else
        {
            readyObject.SetActive(false);
            notReadyObject.SetActive(false);
        }

        if (string.Equals(player.status, "disconnected", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(player.status, "offline", System.StringComparison.OrdinalIgnoreCase))
        {
            SetIndicatorAlpha(wifiDisconnectedIndicator, 1f);
        }
    }

    private void SetIndicatorAlpha(CanvasGroup indicator, float alpha)
    {
        if (indicator != null)
        {
            indicator.alpha = alpha;
            indicator.interactable = false;
            indicator.blocksRaycasts = false;
        }
    }

    private float lastLogTime = 0f;
    private void debugLogPerSecond(string message, string type = "debug")
    {
        if (Time.time - lastLogTime >= 2f)
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
