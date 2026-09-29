using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class Timer : MonoBehaviour
{
    public float gameDuration = 180f; 
    public float currentTime = 0f;
    public TextMeshProUGUI timer = null;
    bool isSoundPlay=false;
    [SerializeField]
    private Color32 pinkColor = Color.yellow;
    public bool endGame = false;
    public UnityEvent finishedEvent;
    private AudioSource lastTenDingDing = null;
    private Tween timerScaleTween = null;
    private Color originalTimerColor;
    private Vector3 originalTimerScale;

    private void Start()
    {
        this.lastTenDingDing = GetComponent<AudioSource>();
        if (this.timer != null)
        {
            this.originalTimerColor = this.timer.color;
            this.originalTimerScale = this.timer.transform.localScale;
        }
        this.Init();
    }

    private void Update()
    {
        if(this.timer == null)
            return;

        if (!this.endGame)
        {
            if(this.currentTime > 0f)
            {
                if(this.currentTime < 10f)
                {
                    if(isSoundPlay== false)
                    {
                        isSoundPlay = true;

                        this.lastTenDingDing.Play();
                        this.lastTenDingDing.loop = true;

                        if (this.timer.color == Color.white && this.timerScaleTween == null) {
                            this.timerScaleTween = this.timer.transform.DOScale(0.8f, 0.5f).SetLoops(-1, LoopType.Yoyo);
                            this.timer.color = this.pinkColor;
                        }
                    }
                    
                }
                this.currentTime -= Time.deltaTime;
                this.UpdateTimerText();
            }
            else
            {
                if (this.timerScaleTween != null && this.timerScaleTween.IsActive()) this.timerScaleTween.Kill();
                this.currentTime = 0f;
                this.UpdateTimerText();
                this.endGame = true;
                this.lastTenDingDing.Stop();
                if (this.finishedEvent != null) this.finishedEvent.Invoke();
            }
        }
        else
        {
            if (isSoundPlay)
            {
                this.lastTenDingDing.Stop();
                isSoundPlay = false;
            }
        }

    }

    public void Init()
    {
        this.endGame = false;

        if (LoaderConfig.Instance != null && LoaderConfig.Instance.GameTime > 0f)
            this.gameDuration = LoaderConfig.Instance.GameTime;

        this.currentTime = this.gameDuration;
        this.UpdateTimerText();
    }

    public void SyncFromServer(float remainingTime)
    {
        this.currentTime = Mathf.Max(0f, remainingTime);
        this.endGame = this.currentTime <= 0f;
        this.isSoundPlay = false;

        if (this.lastTenDingDing != null)
        {
            this.lastTenDingDing.Stop();
            this.lastTenDingDing.loop = false;
        }

        if (this.timerScaleTween != null)
        {
            if (this.timerScaleTween.IsActive()) this.timerScaleTween.Kill();
            this.timerScaleTween = null;
        }

        if (this.timer != null)
        {
            this.timer.color = this.originalTimerColor;
            this.timer.transform.localScale = this.originalTimerScale;
        }

        this.UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        if(WS_Client.Instance != null && WS_Client.Instance.GameData != null)
        {
            this.currentTime = (float)WS_Client.Instance.GameData.gameTimer;
        }

        int minutes = Mathf.FloorToInt(this.currentTime / 60f);
        int seconds = Mathf.FloorToInt(this.currentTime % 60f);
        this.timer.text = $"{minutes:D2}:{seconds:D2}";
    }

}
