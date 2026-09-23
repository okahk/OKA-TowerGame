using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CharacterAnimation : MonoBehaviour
{
    public Material[] playerMats;
    public CharacterSet characterSet;
    public float frameRate = 5f; // Frame rate for the animation
    public ParticleSystem runEffect;
    public RawImage boardImage;
    public RawImage characterImage;
    public int currentFrame = 0;
    private AspectRatioFitter aspectRatioFitter;
    private bool isWalking;
    private float walkingFrameTimer;

    private void EnsureVisualReferences()
    {
        if (this.characterImage == null)
            this.characterImage = GetComponent<RawImage>();

        if (this.characterImage != null && this.aspectRatioFitter == null)
            this.aspectRatioFitter = this.characterImage.GetComponent<AspectRatioFitter>();
    }

    void Start()
    {
        this.EnsureVisualReferences();
        //this.characterImage.material = this.playerMats[this.characterSet.playerNumber];
        if (this.GetComponent<ShiningEffect>() != null && this.characterImage != null)
        {
            this.GetComponent<ShiningEffect>().material = this.characterImage.material;
        }
        this.setIdling();
        if (this.boardImage != null)
        {
            this.boardImage.texture = characterSet.boardTexture;
        }
    }

    void setParticleLayer(int layerOrder)
    {
        if (this.runEffect != null)
        {
            this.runEffect.GetComponent<ParticleSystemRenderer>().sortingOrder = layerOrder;
        }
    }

    public void PlayWalking(int layerOrder)
    {
        this.EnsureVisualReferences();

        if (this.characterSet == null ||
            this.characterSet.walkingAnimationTextures == null ||
            this.characterSet.walkingAnimationTextures.Length == 0)
        {
            return;
        }

        if (this.isWalking) return;

        this.isWalking = true;
        this.currentFrame = 0;
        this.walkingFrameTimer = 0f;
        this.setParticleLayer(layerOrder);
        this.SetWalkingTexture();
        if (this.runEffect != null && !this.runEffect.isPlaying)
        {
                this.runEffect.Play();
        }
    }

    private void Update()
    {
        if (!this.isWalking || this.characterSet == null ||
            this.characterSet.walkingAnimationTextures == null ||
            this.characterSet.walkingAnimationTextures.Length == 0)
        {
            return;
        }

        float safeFrameRate = Mathf.Max(0.01f, this.frameRate);
        float frameDuration = 1f / safeFrameRate;
        this.walkingFrameTimer += Time.deltaTime;

        // Catch up after a slow frame without changing the animation speed.
        while (this.walkingFrameTimer >= frameDuration)
        {
            this.walkingFrameTimer -= frameDuration;
            this.currentFrame = (this.currentFrame + 1) % this.characterSet.walkingAnimationTextures.Length;
            this.SetWalkingTexture();
        }

        if (this.runEffect != null && !this.runEffect.isPlaying)
        {
            this.runEffect.Play();
        }
    }

    private void SetWalkingTexture()
    {
        if (this.characterImage == null || this.characterSet == null ||
            this.characterSet.walkingAnimationTextures == null ||
            this.characterSet.walkingAnimationTextures.Length == 0)
        {
            return;
        }

        Texture walkingTexture = this.characterSet.walkingAnimationTextures[this.currentFrame];
        if (walkingTexture != null)
        {
            this.characterImage.texture = walkingTexture;
        }
    }

    // Call this method to switch animation sets
    public void setIdling()
    {
        this.EnsureVisualReferences();
        Texture idleTexture = this.characterSet != null ? this.characterSet.idlingTexture : null;

        if (this.isWalking)
        {
            if (this.runEffect != null) this.runEffect.Stop();
        }
        this.isWalking = false;
        this.walkingFrameTimer = 0f;

        if (this.characterImage != null && idleTexture != null) {
            float aspectRatio = (float)idleTexture.width / idleTexture.height;
            if (this.aspectRatioFitter != null && !Mathf.Approximately(this.aspectRatioFitter.aspectRatio, aspectRatio))
                this.aspectRatioFitter.aspectRatio = aspectRatio;

            if (this.characterImage.texture != idleTexture)
            {
                if (this.characterImage.material != null)
                    this.characterImage.material.SetTexture("_NewTex_1", idleTexture);
                this.characterImage.texture = idleTexture;
            }
        }
    }
}
