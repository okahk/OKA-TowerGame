using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterController : UserData
{
    public Camera detectCamera;
    public float followSpeed = 1600f;
    public float acc = 640f;
    public int answerId = -1;
    private float currectSpeed = 640f;
    public CanvasGroup answerBubble;
    public TMPro.TextMeshProUGUI answerText, playerNameText;
    public Image playerTag;
    public int direction = 0;
    public string key = "";
    public bool IsLocalPlayer = false; 
    private Vector3 localDestination = Vector3.zero;
    public bool isMouseDown = false; 
    public CanvasGroup localPlayer;
    // Network throttling
    private float lastNetworkUpdateTime = 0f;
    public float networkUpdateInterval = 0.1f; // Send updates every 0.1 seconds (10 times per second)
    public CharacterAnimation characterAnimation;
    public RawImage characterUIImage;
    public float textureAnimationFrameRate = 2f;
    private Vector3 smoothVelocity = Vector3.zero;
    private float smoothTime = 0.08f; // tune this to reduce fling; lower = snappier, higher = smoother
    private float maxMoveSpeed => followSpeed * (1 / TowerGameController.Instance.clientMapScale); // maximum units per second
    private static PointerEventData s_pointerEventData;
    private static List<RaycastResult> s_raycastResults = new List<RaycastResult>(8);
    public bool isMoving = false;
    public float distance;

    private Transform cachedTransform;
    private WS_Client.PositionData posData = new WS_Client.PositionData();
    private WS_Client.PositionData destData = new WS_Client.PositionData();

    private void Awake()
    {
        this.cachedTransform = transform;
    }


    void Start()
    {
        if (transform.parent != null)
            localDestination = transform.localPosition;
        else
            localDestination = transform.position;
    }
    public void setLocalPlayer(bool _isLocalPlayer = false)
    {
        this.IsLocalPlayer = _isLocalPlayer;
        SetUI.Set(this.localPlayer, _isLocalPlayer);
    }
    public void setPlayerTag(Sprite tag, string playerName)
    {
        if (this.playerTag != null)
        {
            this.playerTag.sprite = tag;
        }
        if (this.playerNameText != null)
        {
            this.playerNameText.text = playerName;
        }
    }
    public void SetCostumeTextures(CharacterSet characterSet)
    {
        // Initialize image components if not done yet (in case this is called before Start)

        if(this.characterAnimation != null)
        {
            this.characterAnimation.characterSet = characterSet;
            this.characterAnimation.setIdling();
        }
    }


    //Fixed the touch and mouse click conflict with UI Buttons
    private bool IsPointerOverUIButton()
    {
        // Return true if pointer (mouse or any touch) is over UI element that should block movement.
        // Uses EventSystem raycast and considers common UI elements (Graphic raycast targets, Selectable, TMP input, and handlers).
        if (EventSystem.current == null) return false;

        if (s_pointerEventData == null) s_pointerEventData = new PointerEventData(EventSystem.current);

        int touchCount = Input.touchCount;
        // Check all touches first
        for (int i = 0; i < touchCount; ++i)
        {
            Touch t = Input.GetTouch(i);
            s_pointerEventData.pointerId = t.fingerId;
            s_pointerEventData.position = t.position;

            s_raycastResults.Clear();
            EventSystem.current.RaycastAll(s_pointerEventData, s_raycastResults);
            if (IsTopRaycastInteractive(s_raycastResults)) return true;
        }

        // Mouse fallback
        s_pointerEventData.pointerId = -1;
        s_pointerEventData.position = Input.mousePosition;
        s_raycastResults.Clear();
        EventSystem.current.RaycastAll(s_pointerEventData, s_raycastResults);
        if (IsTopRaycastInteractive(s_raycastResults)) return true;

        return false;
    }

    // Only consider the top-most raycast result (closest) interactive by default.
    // Walk up parents from that top result checking for interactive components.
    private bool IsTopRaycastInteractive(List<RaycastResult> results)
    {
        if (results == null || results.Count == 0) return false;

        // Top-most result is results[0]
        var top = results[0].gameObject;
        if (top == null) return false;

        // If the top-most is clearly interactive, block.
        if (IsGameObjectBlockingUI(top)) return true;

        // Otherwise check parents for interactive components (common pattern)
        Transform t = top.transform.parent;
        while (t != null)
        {
            if (IsGameObjectBlockingUI(t.gameObject)) return true;
            t = t.parent;
        }

        // Not interactive � let the click pass through
        return false;
    }


    // Helper: heuristics for deciding if a GameObject should block gameplay input
    private bool IsGameObjectBlockingUI(GameObject go)
    {
        if (go == null) return false;

        // Only treat truly interactive UI as blocking:
        // 1) Any Selectable (Button, Toggle, Slider, Dropdown, ScrollRect, etc.) -> block
        var selectable = go.GetComponent<Selectable>();
        if (selectable != null) return true;

        // 2) Input fields -> block (user typing / focusing)
        var tmpInput = go.GetComponent<TMPro.TMP_InputField>();
        if (tmpInput != null) return true;
        var inputField = go.GetComponent<InputField>();
        if (inputField != null) return true;

        // 3) If this GameObject implements pointer/drag/scroll/submit handlers -> block
        var monos = go.GetComponents<MonoBehaviour>();
        for (int j = 0; j < monos.Length; ++j)
        {
            var m = monos[j];
            if (m is IPointerClickHandler) return true;
            if (m is IBeginDragHandler) return true;
            if (m is IDragHandler) return true;
            if (m is IEndDragHandler) return true;
            if (m is IScrollHandler) return true;
            if (m is ISubmitHandler) return true;
        }

        // Do NOT treat plain Graphics or CanvasGroups as blocking here � that was too aggressive.
        // Plain decorative images/text should not prevent gameplay clicks.

        return false;
    }


    void Update()
    {
        if (CanvasMapPan.Instance.playerRect == null) return;
        if (!this.IsLocalPlayer)
        {
            UpdateAnimation();
            return;
        }

        // Skip input if pointer is over UI
        if (this.IsPointerOverUIButton())
        {
            isMouseDown = false;
            UpdateAnimation();
            return;
        }

        // Cache touch count once per frame
        int touchCount = Input.touchCount;
        Touch? firstTouch = touchCount > 0 ? (Touch?)Input.GetTouch(0) : null;

        // Handle input state
        if (Input.GetMouseButtonDown(0) || (firstTouch.HasValue && firstTouch.Value.phase == TouchPhase.Began))
            isMouseDown = true;

        if (Input.GetMouseButtonUp(0) || (firstTouch.HasValue && firstTouch.Value.phase == TouchPhase.Ended))
            isMouseDown = false;

        if (touchCount == 0 && !Input.GetMouseButton(0))
        {
            localDestination = cachedTransform.parent != null ? cachedTransform.localPosition : cachedTransform.position;
            smoothVelocity = Vector3.zero;
            isMouseDown = false;
        }

        // Movement + network updates
        if (isMouseDown)
        {
            calLocalDestination();

            if (Time.time - lastNetworkUpdateTime >= networkUpdateInterval)
            {
                lastNetworkUpdateTime = Time.time;

                if (WS_Client.Instance != null)
                {
                    try
                    {
                        // Reuse objects to avoid GC allocations
                        posData.x = cachedTransform.localPosition.x;
                        posData.y = cachedTransform.localPosition.y;

                        destData.x = localDestination.x;
                        destData.y = localDestination.y;

                        _ = WS_Client.Instance.UpdateServerPosition(posData, destData);
                    }
                    catch (System.Exception ex)
                    {
                        LogController.Instance.debugError(
                            $"Network update failed for {gameObject.name}: {ex.Message}\n{ex.StackTrace}"
                        );
                    }
                }
            }
        }

        UpdateAnimation();

    }

    void LateUpdate()
    {
        if (!this.IsLocalPlayer)
        {
            this.FollowLocalDestination();
            return;
        }
        else
        {
           this.smoothMoveToLocalPosition();
        }
    }
    public void smoothMoveToLocalPosition()
    {
        Vector3 current = (transform.parent != null) ? transform.localPosition : transform.position;
        Vector3 target = localDestination;

        float dt = Time.deltaTime;
        float maxStep = maxMoveSpeed * dt;

        // Smooth damp towards target. Use maxStep limiter to avoid large per-frame jumps.
        Vector3 next = Vector3.SmoothDamp(current, target, ref smoothVelocity, smoothTime, maxMoveSpeed, dt);

        // clamp per-frame movement to maxStep to avoid fling when target suddenly jumps
        Vector3 delta = next - current;
        if (delta.magnitude > maxStep)
        {
            next = current + delta.normalized * maxStep;
            smoothVelocity = Vector3.zero;
        }

        // Stop threshold: snap to target and clear velocity when very close
        const float stopThreshold = 0.02f;
        if ((target - current).magnitude <= stopThreshold)
        {
            smoothVelocity = Vector3.zero;
            next = target;
        }

        if (transform.parent != null)
        {
            transform.localPosition = new Vector3(next.x, next.y, transform.localPosition.z);
        }
        else
        {
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }

    private void calLocalDestination()
    {
        if (this.detectCamera == null) this.detectCamera = Camera.main;

        // Get input position from touch or mouse
        Vector3 inputPosition;
        if (Input.touchCount > 0)
            inputPosition = Input.GetTouch(0).position;
        else
            inputPosition = Input.mousePosition;

        // Convert screen point to world point on plane of character z
        Ray ray = (this.detectCamera != null) ? this.detectCamera.ScreenPointToRay(inputPosition) : Camera.main.ScreenPointToRay(inputPosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);

            // Character current world position
            Vector3 charWorldPos = (transform.parent != null) ? transform.parent.TransformPoint(transform.localPosition) : transform.position;

            // Vector from character to pointer
            Vector3 delta = worldPoint - charWorldPos;
            float distanceToPointer = delta.magnitude;

            // If pointer is very close to the character, treat as no movement to avoid jitter
            const float clickDeadzoneWorld = 0.18f; // tune this (world units)
            if (distanceToPointer <= clickDeadzoneWorld)
            {
                // Snap destination to current position and clear velocity to avoid tiny SmoothDamp adjustments
                if (transform.parent != null)
                    localDestination = transform.localPosition;
                else
                    localDestination = transform.position;

                smoothVelocity = Vector3.zero;
                this.isMoving = false;
                return;
            }

            // Otherwise set a destination in the direction of the pointer but limit step to avoid overshoot
            Vector3 dir = delta.normalized;

            // Use a reasonable step toward pointer: min(distance, maxMoveSpeed)
            float stepDistance = Mathf.Min(distanceToPointer, maxMoveSpeed);

            Vector3 worldDestination = charWorldPos + dir * stepDistance;

            // store as local if parent exists, otherwise world pos
            if (transform.parent != null)
                localDestination = transform.parent.InverseTransformPoint(worldDestination);
            else
                localDestination = worldDestination;

            this.isMoving = true;
            // keep z consistent
            localDestination.z = transform.localPosition.z;
        }
    }

    public void setLocalDestination(Vector3 destination)
    {
        localDestination = destination;
    }

    private void FollowLocalDestination()
    {
        float distance = Vector3.Distance(transform.localPosition, this.localDestination);

        if (!this.IsLocalPlayer && distance > 500f)
        {
            LogController.Instance.debug("Teleporting player due to large desync: distance=" + distance);
            transform.localPosition = new Vector3(localDestination.x, localDestination.y, transform.localPosition.z);
        }
        else if (distance > 10f)
        {
            var newFollowSpeed = followSpeed * (1 / TowerGameController.Instance.clientMapScale);
            currectSpeed = Mathf.Min(currectSpeed + acc * Time.deltaTime, newFollowSpeed);
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, localDestination, currectSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimation()
    {
        try
        {
            Vector3 movement = localDestination - transform.localPosition;
            this.distance = movement.magnitude;
            // Minimum movement to consider (avoids jitter when clicking nearly on the character)
            const float minMoveThreshold = 5f;

            // Horizontal deadzone to avoid rapid left/right flips
            const float horizontalDeadzone = 0.15f;

            var imageTransform = this.characterUIImage?.transform;

            if (this.distance > minMoveThreshold)
            {
                // Only change facing when horizontal movement is meaningful
                float mx = movement.x;

                if (Mathf.Abs(mx) > horizontalDeadzone)
                {
                    if (mx > 0f)
                    {
                        this.direction = 2; // facing right
                        if (imageTransform != null)
                            imageTransform.localScale = new Vector3(-1f, 1f, 1f);
                    }
                    else
                    {
                        this.direction = 1; // facing left
                        if (imageTransform != null)
                            imageTransform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }
                // If horizontal movement is within deadzone, keep previous facing (do not flip)
            }
            else
            {
                // Considered stopped
                this.direction = 0;
            }

            // Animation: only play walking when movement is meaningful or local player is dragging
            if (this.characterAnimation == null) return;

            const float remoteMoveThreshold = 10f;
            bool remoteShouldWalk = (!IsLocalPlayer && this.distance > remoteMoveThreshold);
            bool localShouldWalk = (IsLocalPlayer && isMouseDown && this.isMoving);
            bool shouldWalk = remoteShouldWalk || localShouldWalk;

            if (shouldWalk)
            {
                this.characterAnimation.PlayWalking(1);
            }
            else
            {
                this.characterAnimation.setIdling();
            }
        }
        catch (System.Exception ex)
        {
            LogController.Instance.debugError($"Error in UpdateAnimation for {gameObject.name}: {ex.Message}");
        }
    }

    public void showAnswerBubble(int show, string _answer = "")
    {
        SetUI.Set(this.answerBubble, show == 1);
        if(this.answerText != null)
        {
            this.answerText.text = _answer;
        }   
        if(show==1) AudioController.Instance?.PlayAudio(9);
        if (show == 0) this.answerId = -1;
    }

    // Add these methods near the bottom of the class (before showAnswerBubble)
    private void StopMovementForCollision()
    {
        // Freeze destination at current position and clear velocity so physics won't push the character
        if (transform.parent != null)
            localDestination = transform.localPosition;
        else
            localDestination = transform.position;

        smoothVelocity = Vector3.zero;
        isMoving = false;

        // ensure animation switches to idle immediately
        if (this.characterAnimation != null)
            this.characterAnimation.setIdling();
    }

    // Called when colliders (non-trigger) intersect - 2D physics
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null) return;

        // If the other collider is a PolygonCollider2D, stop movement
        if (collision.collider.CompareTag("Obstacle"))
        {
            StopMovementForCollision();
        }
    }
}