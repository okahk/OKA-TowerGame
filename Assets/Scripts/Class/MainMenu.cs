using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance = null;
    public CanvasGroup gameStartPanel;
    public float instructionPanelStartPosY = 200f;
    public AudioOnOff audioOnOffPanel;
    public CanvasGroup loadingScreenCanvas;
    public RectTransform progressBar;
    public TextMeshProUGUI loadingText;
    private void Awake()
    {
        if(Instance == null) 
            Instance = this;

        Time.timeScale = 1f;
        LoaderConfig.Instance?.InitialGameSetup();
        SetUI.Set(this.loadingScreenCanvas, false);
    }
    private void Start()
    {
        AudioController.Instance?.changeBGMStatus(false);
        if (!LoaderConfig.Instance.skipAudioPanel)
        {
            this.audioOnOffPanel.Init(true);
            SetUI.SetMove(this.gameStartPanel, false, new Vector2(0f, this.instructionPanelStartPosY), 0f);
            InstructionSlideShow.Instance?.ShowInstructionPopup(true);
            LoaderConfig.Instance.skipAudioPanel = true;
        }
        else
        {
            this.audioOnOffPanel.Init(false);
            InstructionSlideShow.Instance?.ShowInstructionPopup(false);
            SetUI.SetMove(this.gameStartPanel, true, Vector2.zero, 0.5f);
        }

        ExternalCaller.SaveHistoryBackTarget();
    }

    public void MusicOnbutton()
    {
        this.audioOnOffPanel.set(true);
        this.audioOnOffPanel.setPanel(false);
        SetUI.SetMove(this.gameStartPanel, true, Vector2.zero, 0.5f);
    }
    public void MusicOffbutton()
    {
        this.audioOnOffPanel.set(false);
        this.audioOnOffPanel.setPanel(false);
        SetUI.SetMove(this.gameStartPanel, true, Vector2.zero, 0.5f);
    }

    public void StartGame()
    {
        AudioController.Instance?.PlayAudio(0);
        SetUI.SetMove(this.gameStartPanel, false, new Vector2(0f, this.instructionPanelStartPosY), 0.5f, ()=> this.gameStart());
    }

    public void playAudioClick()
    {
        AudioController.Instance?.PlayAudio(0);
    }

    public void gameStart()
    {
        LogController.Instance?.debug("Start Game.");
        StartCoroutine(LoadSceneAsync(2));
    }

    public void BackToWebpage()
    {
        AudioController.Instance?.PlayAudio(0);
        LoaderConfig.Instance?.exitPage(false, "Leave Game", ExternalCaller.BackToHomeUrlPage, null);
    }


    private IEnumerator LoadSceneAsync(int sceneId)
    {
        LogController.Instance?.debug($"Starting to load scene {sceneId}...");
        // Wait one frame to ensure UI is rendered
        SetUI.Set(this.loadingScreenCanvas, true);
        yield return null;

        var previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        // Start loading the scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneId);
        asyncLoad.allowSceneActivation = false;
        // Minimum loading time to ensure visibility
        float minimumLoadTime = 0.5f;
        float startTime = Time.time;

        float displayedProgress = 0f;
        const float progressSmoothSpeed = 0.8f; // units per second, tune for desired smoothness
        bool targetReached = false;

        // Update loading progress
        while (!asyncLoad.isDone)
        {
            // Engine-reported progress: 0..0.9 while loading, then 0.9 when ready to activate
            float engineProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // If engine reports ready (>=0.9) consider target = 1.0
            float targetProgress = asyncLoad.progress >= 0.9f ? 1f : engineProgress;

            // Smoothly move displayedProgress toward targetProgress
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, progressSmoothSpeed * Time.deltaTime);

            // Update progress bar if it exists
            if (this.progressBar != null)
            {
                float barWidth = this.progressBar.rect.width;
                float startX = -barWidth;
                float updatedX = Mathf.Lerp(startX, 0f, displayedProgress);
                this.progressBar.anchoredPosition = new Vector2(updatedX, this.progressBar.anchoredPosition.y);
            }

            if (loadingText != null)
            {
                loadingText.text = $"{(displayedProgress * 100f):0}%";
            }


            // Scene is ready to activate
            if (asyncLoad.progress >= 0.9f)
            {
                // Ensure minimum loading time has passed
                float elapsedTime = Time.time - startTime;
                if (elapsedTime < minimumLoadTime)
                {
                    // still wait but keep updating UI; do not block animator
                    yield return null;
                    continue;
                }

                // Wait until displayed progress visually reaches 1.0
                if (displayedProgress < 0.999f)
                {
                    // let the smoothing finish
                    yield return null;
                    continue;
                }
                // brief pause so user sees 100%
                if (!targetReached)
                {
                    targetReached = true;
                    yield return new WaitForSeconds(0.15f);
                }

                Application.backgroundLoadingPriority = previousPriority;
                LogController.Instance?.debug($"Scene {sceneId} loaded successfully!");
                asyncLoad.allowSceneActivation = true;
                break;
            }

            yield return null;
        }
        // Fallback: restore priority
        Application.backgroundLoadingPriority = previousPriority;
    }
}
