using System;


public class LoaderConfig : GameSetting
{
    public static LoaderConfig Instance = null;

    protected override void Awake()
    {
        if (Instance == null)
            Instance = this;

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
#if UNITY_EDITOR
        this.LoadGameData();
#endif
    }

    protected override void Update()
    {
        base.Update();


    }

    public void LoadGameData()
    {
        this.apiManager.PostGameSetting(this.GetParseURLParams,
                                        () => StartCoroutine(this.apiManager.postGameSetting(this.LoadQuestions)),
                                        () => StartCoroutine(this.apiManager.postGameAppSetting(this.LoadQuestions)),
                                        this.finishedLoadQuestion
                                        );
    }

    public void getHashValue(string hashValue)
    {
        LogController.Instance.debug("Received hash value from JavaScript: " + hashValue);
    }


    public void LoadQuestions()
    {
        this.InitialGameImages(() =>
        {
            QuestionManager.Instance?.LoadQuestionFile(this.unitKey, () => this.finishedLoadQuestion());
        });
    }

    void finishedLoadQuestion()
    {
        var title = QuestionManager.Instance.questionData.gameTitle;
        if (!string.IsNullOrEmpty(title))
            this.gameSetup.gamePageName = title;

        var pageName = this.gameSetup.gamePageName;
        if (!string.IsNullOrEmpty(pageName))
        {
            ExternalCaller.SetWebPageTitle(pageName);
            LogController.Instance?.debug($"Setup Current GameName: {pageName}");
        }

        var wsClient = WS_Client.Instance;
        if (wsClient != null)
        {
            if (!string.IsNullOrEmpty(this.apiManager.jwt))
                wsClient.jwt = this.apiManager.jwt;

            wsClient.Connect();
        }

        ExternalCaller.HiddenLoadingBar();
        this.changeScene(1);
    } 

    public void SubmitAnswer(int duration, int playerScore, float statePercent, int stateProgress,
                             int correctId, float currentQADuration, string qid, int answerId, string answerText,
                             string correctAnswerText, float currentQAscore, float currentQAPercent, Action onCompleted = null)
    {
        /*        string jsonPayload = $"[{{\"payloads\":{playloads}," +
        $"\"role\":{{\"uid\":{uid}}}," +
        $"\"state\":{{\"duration\":{stateDuration},\"score\":{stateScore},\"percent\":{statePercent},\"progress\":{stateProgress}}}," +
        $"\"currentQuestion\":{{\"correct\":{correct},\"duration\":{currentQADuration},\"qid\":\"{currentqid}\",\"answer\":{answerId},\"answerText\":\"{answerText}\",\"correctAnswerText\":\"{correctAnswerText}\",\"score\":{currentQAscore},\"percent\":{currentQAPercent}}}}}]";*/

        var answer = this.apiManager.answer;
        answer.state.duration = duration;
        answer.state.score = playerScore;
        answer.state.percent = statePercent;
        answer.state.progress = stateProgress;

        answer.currentQA.correctId = correctId;
        answer.currentQA.duration = currentQADuration;
        answer.currentQA.qid = qid;
        answer.currentQA.answerId = answerId;
        answer.currentQA.answerText = answerText;
        answer.currentQA.correctAnswerText = correctAnswerText;
        answer.currentQA.score = currentQAscore;
        answer.currentQA.percent = currentQAPercent;


        StartCoroutine(this.apiManager.SubmitAnswer(onCompleted));
    }

    public void closeLoginErrorBox()
    {
        this.apiManager.resetLoginErrorBox();
    }

    public void exitPage(bool useExitApi = false, string state = "", Action<bool> leavePageWithValue = null, Action leavePageWithoutValue = null)
    {
        bool isLogined = this.apiManager.IsLogined;
        if (isLogined)
        {
   
            if (useExitApi)
            {
                LogController.Instance?.debug($"{state}, called exit api.");
                // StartCoroutine(this.apiManager.ExitGameRecord(() =>
                // {
                    leavePageWithValue?.Invoke(true);
                    leavePageWithoutValue?.Invoke();
                // }));
            }
            else
            {
                leavePageWithValue?.Invoke(true);
                leavePageWithoutValue?.Invoke();
            }
        }
        else
        {
            leavePageWithValue?.Invoke(false);
            leavePageWithoutValue?.Invoke();
            LogController.Instance?.debug($"{state}.");
        }
    }

    public void restartGameAPI(Action onCompleted = null)
    {
        bool isLogined = this.apiManager.IsLogined;
        if (isLogined)
        {
            LogController.Instance?.debug("Called restart game api and renew payload.");
            StartCoroutine(this.apiManager.restartGameAPI(onCompleted));
        }
    }

    public void submitEndGameSummary()
    {
        bool isLogined = this.apiManager.IsLogined;
        if (isLogined)
        {
            LogController.Instance?.debug("Called end game summary api.");
            StartCoroutine(this.apiManager.ExitGameRecord(null));
        }
        else
        {
            LogController.Instance?.debug("Not logged in, skip end game summary api.");
        }
    }

    public void QuitGame()
    {
        this.exitPage(true, "Quit Game", null);
    }

    private void OnApplicationQuit()
    {
        this.QuitGame();
    }
}

