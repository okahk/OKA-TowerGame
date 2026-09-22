using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CharacterAnimation : MonoBehaviour
{
    public Material[] playerMats;
    public CharacterSet characterSet;
    public float frameRate = 10f; // Frame rate for the animation
    public ParticleSystem runEffect;
    public RawImage boardImage;
    public RawImage characterImage;
    public int currentFrame = 0;
    private Coroutine walkingCoroutine;
    private AspectRatioFitter aspectRatioFitter;
    private bool isWalking;

    void Start()
    {
        if (this.characterImage == null) this.characterImage = GetComponent<RawImage>();
        if (this.characterImage != null)
            this.aspectRatioFitter = this.characterImage.GetComponent<AspectRatioFitter>();
        //this.characterImage.material = this.playerMats[this.characterSet.playerNumber];
        if (this.GetComponent<ShiningEffect>() != null)
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
        // If the walking coroutine is already running, do nothing
        if (this.walkingCoroutine != null) return;

        this.isWalking = true;
        this.setParticleLayer(layerOrder);

        // Start the walking animation coroutine
        this.walkingCoroutine = StartCoroutine(this.walkingAnimation());
    }
    private IEnumerator walkingAnimation()
    {
        int currentFrame = 0;

        while (true) // Loop indefinitely while walking
        {
            if (this.characterImage != null)
            {
                this.characterImage.texture = this.characterSet.walkingAnimationTextures[currentFrame];
                //this.characterImage.material.SetTexture("_NewTex_1", this.characterSet.walkingAnimationTextures[currentFrame]);
            }

            currentFrame = (currentFrame + 1) % this.characterSet.walkingAnimationTextures.Length;
            if(this.runEffect != null && !this.runEffect.isPlaying) { 
                this.runEffect.Play();
            }
            yield return new WaitForSeconds(1f / this.frameRate); // Wait for the frame duration
        }
    }

    // Call this method to switch animation sets
    public void setIdling()
    {
        Texture idleTexture = this.characterSet != null ? this.characterSet.idlingTexture : null;

        // Stop the walking coroutine if it's running
        if (this.walkingCoroutine != null)
        {
            if (this.runEffect != null) this.runEffect.Stop();
            StopCoroutine(this.walkingCoroutine);
            this.walkingCoroutine = null; // Clear the reference
        }
        this.isWalking = false;

        if (this.characterImage != null && idleTexture != null) {
            if (this.aspectRatioFitter == null)
            {
                this.aspectRatioFitter = this.characterImage.gameObject.AddComponent<AspectRatioFitter>();
                this.aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }

            float aspectRatio = (float)idleTexture.width / idleTexture.height;
            if (!Mathf.Approximately(this.aspectRatioFitter.aspectRatio, aspectRatio))
                this.aspectRatioFitter.aspectRatio = aspectRatio;

            if (this.characterImage.texture != idleTexture)
            {
                this.characterImage.material.SetTexture("_NewTex_1", idleTexture);
                this.characterImage.texture = idleTexture;
            }
        }
    }
}
