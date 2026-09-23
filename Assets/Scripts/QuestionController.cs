using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestionController : MonoBehaviour
{
    public static QuestionController Instance = null;
    public List<WS_Client.QuestionData> questionData = new List<WS_Client.QuestionData>();
    public WS_Client.QuestionData currentQuestion;
    public QuestionType questiontype = QuestionType.None;
    public string[] correctAnswerParts;
    public string displayQuestion = "";
    public string fullSentence = "";
    public int correctAnswerId;
    public string correctAnswer;
    public string[] answersChoics;
    public CanvasGroup[] questionBgs;
    public RawImage questionImage;
    public AudioClip audioClip = null;
    public CanvasGroup audioPlayBtn = null;
    private AspectRatioFitter aspecRatioFitter = null;
    public TextMeshProUGUI questionText;
    public Texture qaTexture;

    private void Awake()
    {
        if(Instance == null) Instance = this;
    }

    public void nextQuestion(bool _playAudio = false)
    {
        LogController.Instance?.debug("next question");
        this.GetQuestionAnswer(_playAudio);
    }

    public void GetQuestionAnswer(bool _playAudio = false)
    {
        if (LoaderConfig.Instance == null || QuestionManager.Instance == null)
            return;

        try
        {
            var client = WS_Client.Instance;
            if (client == null || client.GameData == null || client.GameData.questions == null)
                return;

            this.questionData = client.GameData.questions;
            int questionCount = this.questionData.Count;
            LogController.Instance?.debug("Loaded questions:" + questionCount);
            if (questionCount == 0)
            {
                return;
            }

            int round = Mathf.Clamp(client.GameData.round, 1, questionCount);
            this.currentQuestion = this.questionData[round - 1];
            string mediaUrl = "";
            LogController.Instance.debug("updateQuestionUI: round = " + round + " - question = " + this.currentQuestion.content + " - questionMedia = " + this.currentQuestion.media);

            switch (this.currentQuestion.questionType)
            {
                case "text":
                case "Text":
                    this.questiontype = QuestionType.Text;
                    SetUI.SetGroup(this.questionBgs, 2, 0f);
                    this.questionText = this.questionBgs[2].GetComponentInChildren<TextMeshProUGUI>();
                    if (this.questionText != null && this.questionText.gameObject.tag != "Ignore")
                    {
                        switch (LoaderConfig.Instance.gameSetup.qa_font_alignment)
                        {
                            case 1:
                                this.questionText.alignment = TextAlignmentOptions.Left;
                                break;
                            case 2:
                                this.questionText.alignment = TextAlignmentOptions.Center;
                                break;
                            case 3:
                                this.questionText.alignment = TextAlignmentOptions.Right;
                                break;
                            default:
                                this.questionText.alignment = TextAlignmentOptions.Left;
                                break;
                        }
                        this.questionText.text = this.currentQuestion.content;
                    }
                    break;
                case "picture":
                case "Picture":
                    this.questiontype = QuestionType.Picture;
                    SetUI.SetGroup(this.questionBgs, 0, 0f);
                    this.questionImage = this.questionBgs[0].GetComponentInChildren<RawImage>();
                    this.questionText = this.questionBgs[0].GetComponentInChildren<TextMeshProUGUI>();
                    if (this.questionText != null && this.questionText.gameObject.tag != "Ignore") this.questionText.text = this.currentQuestion.content;

                    var imageUrl = this.currentQuestion.media[0];


                    mediaUrl = !string.IsNullOrEmpty(imageUrl) ?
                                      APIConstant.blobServerRelativePath + imageUrl : "";

                    Debug.Log("mediaUrl:" + mediaUrl);

                    var loadImage = QuestionManager.Instance.loadImage;
                    this.qaTexture = null;
                    StartCoroutine(loadImage.Load("", mediaUrl, tex => {
                        this.qaTexture = tex;

                        if (this.questionImage != null && this.qaTexture != null)
                        {
                            this.questionImage.enabled = true;
                            this.aspecRatioFitter = this.questionImage.GetComponent<AspectRatioFitter>();
                            this.questionImage.texture = this.qaTexture;

                            var parentRectTransform = this.questionImage.transform.parent.GetComponent<RectTransform>();
                            var parentWidth = parentRectTransform.sizeDelta.x;
                            if (this.qaTexture.width > this.qaTexture.height)
                            {
                                this.questionImage.GetComponent<RectTransform>().sizeDelta = new Vector2(parentWidth, 300f);
                            }
                            else
                            {
                                this.questionImage.GetComponent<RectTransform>().sizeDelta = new Vector2(parentWidth, 430f);
                            }
                            this.aspecRatioFitter.aspectRatio = (float)this.qaTexture.width / (float)this.qaTexture.height;
                        }
                    }));
                    break;
                case "audio":
                case "Audio":
                    this.questiontype = QuestionType.Audio;
                    SetUI.SetGroup(this.questionBgs, 1, 0f);
                    this.questionText = this.questionBgs[1].GetComponentInChildren<TextMeshProUGUI>();
                    if (this.questionText != null && this.questionText.gameObject.tag != "Ignore") this.questionText.text = this.currentQuestion.content;
                    this.audioPlayBtn = this.questionBgs[1].GetComponentInChildren<CanvasGroup>();
                    if (this.audioPlayBtn != null)
                    {
                        SetUI.Set(this.audioPlayBtn, true, 0f);
                    }

                    var audioUrl = this.currentQuestion.media[0];
                    mediaUrl = !string.IsNullOrEmpty(audioUrl) ? 
                        APIConstant.blobServerRelativePath + audioUrl : "";

                    var loadAudio = QuestionManager.Instance.loadAudio;
                    StartCoroutine(loadAudio.Load("", mediaUrl, audio => {
                        this.audioClip = audio;
                        this.playAudio(_playAudio);
                    }));
                    break;
                case "fillInBlank":
                case "FillInBlank":
                    this.questiontype = QuestionType.FillInBlank;
                    SetUI.SetGroup(this.questionBgs, 3, 0f);
                    this.questionText = this.questionBgs[3].GetComponentInChildren<TextMeshProUGUI>();
                    if (this.questionText != null && this.questionText.gameObject.tag != "Ignore")
                    {
                        switch (LoaderConfig.Instance.gameSetup.qa_font_alignment)
                        {
                            case 1:
                                this.questionText.alignment = TextAlignmentOptions.Left;
                                break;
                            case 2:
                                this.questionText.alignment = TextAlignmentOptions.Center;
                                break;
                            case 3:
                                this.questionText.alignment = TextAlignmentOptions.Right;
                                break;
                            default:
                                this.questionText.alignment = TextAlignmentOptions.Left;
                                break;
                        }
                        this.questionText.text = this.currentQuestion.content;
                    }
                    this.audioPlayBtn = this.questionBgs[3].GetComponentInChildren<CanvasGroup>();
                    if (this.audioPlayBtn != null)
                    {
                        SetUI.Set(this.audioPlayBtn, true, 0f);
                        this.audioPlayBtn.GetComponentInChildren<Button>()?.gameObject.SetActive(this.audioClip != null);
                    }
                    this.playAudio(_playAudio);
                    break;
            }

        }
        catch (Exception e)
        {
            LogController.Instance?.debugError(e.Message);
        }
    }

    public AudioClip currentAudioClip
    {
        get
        {
            return this.audioClip;
        }
    }

    public void playAudio(bool _playAudio)
    {
        if (this.audioPlayBtn != null && this.currentAudioClip != null)
        {
            var audio = this.audioPlayBtn.GetComponentInChildren<AudioSource>();
            if (audio != null && _playAudio)
            {
                audio.Stop();
                audio.clip = this.currentAudioClip;
                audio.Play();
            }
        }
    }

    
}
