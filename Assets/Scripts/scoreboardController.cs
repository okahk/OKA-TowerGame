using System.Collections;
using System.Collections.Generic;
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
        this.rootCanvasGroup.alpha = 1f;
    }

    public void resetScoreboard()
    {
        this.key = "";
        this.icon = null;
        this.text = "";
        this.isReady = false;
        this.iconObject.SetActive(false);
        this.rootCanvasGroup.alpha = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        if (key != "" && key != null) {
            WS_Client.PlayerData player = WS_Client.Instance.GameData.players.Find(p => p.player_id == key);
            if (player != null) {
                if (player.status == "ready") {
                    readyObject.SetActive(true);
                    notReadyObject.SetActive(false);
                } else if (player.status == "waiting") {
                    readyObject.SetActive(false);
                    notReadyObject.SetActive(true);
                } else {
                    readyObject.SetActive(false);
                    notReadyObject.SetActive(false);
                }
            }
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
