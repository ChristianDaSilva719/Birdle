using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class DragLaunchBall : MonoBehaviour
{
    [Header("Launch")]
    public float launchPower = 8f;       // how strongly the pull converts to speed
    public float maxPullDistance = 3f;   // clamp so you can't pull infinitely far
    public float minPullToLaunch = 0.3f; // a tiny pull cancels the shot and doesn't cost a turn

    [Header("Movement feel")]
    public float slowDownDrag = 1.5f;    // higher = ball slows down faster
    public float bounciness = 0.9f;      // 0 = no bounce, 1 = perfect bounce
    public float stopSpeed = 0.1f;       // below this speed the ball comes to a full stop

    [Header("Holes")]
    public float fallTime = 0.3f;        // how long the ball takes to shrink into a hole
    public float respawnPause = 0.2f;    // pause before the ball reappears where the shot started

    [Header("Aim line (optional)")]
    public LineRenderer aimLine;

    private Rigidbody2D rb;
    private Camera cam;
    private bool isDragging;
    private bool launched; // true from release until the ball has stopped
    private bool falling;  // true while the ball is dropping into a hole
    private Vector2 dragStart;
    private Vector2 shotStart; // where the ball was when the current shot was fired
    private Vector3 startScale;
    private Coroutine fallRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
        startScale = transform.localScale;

        if (cam == null)
            Debug.LogError("DragLaunchBall: no camera tagged MainCamera, so the pointer can't be turned into a world position.");

        rb.gravityScale = 0f;
        rb.linearDamping = slowDownDrag; // use rb.drag on Unity 2022 and older
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        PhysicsMaterial2D bounceMat = new PhysicsMaterial2D("BallBounce");
        bounceMat.bounciness = bounciness;
        bounceMat.friction = 0f;
        GetComponent<CircleCollider2D>().sharedMaterial = bounceMat;

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
    }

    bool CanShoot()
    {
        return GameManager.instance == null || GameManager.instance.CanShoot;
    }

    //Called by the GameManager when a level starts or restarts
    public void ResetTo(Vector2 position)
    {
        //If a new level starts while the ball is dropping into a hole, undo the fall
        if (fallRoutine != null)
        {
            StopCoroutine(fallRoutine);
            fallRoutine = null;
        }
        falling = false;
        transform.localScale = startScale;
        rb.bodyType = RigidbodyType2D.Dynamic;

        isDragging = false;
        launched = false;
        if (aimLine != null) aimLine.enabled = false;

        rb.linearVelocity = Vector2.zero; // use rb.velocity on Unity 2022 and older
        rb.position = position;
        transform.position = position;
    }

    //True if the press is on a UI button or panel, so it shouldn't start an aim
    bool PointerOverUI()
    {
        if (EventSystem.current == null) return false;

        // Touches need their own id, the mouse uses the default
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            return EventSystem.current.IsPointerOverGameObject(touchscreen.primaryTouch.touchId.ReadValue());

        return EventSystem.current.IsPointerOverGameObject();
    }

    void CancelDrag()
    {
        isDragging = false;
        if (aimLine != null) aimLine.enabled = false;
    }

    void Update()
    {
        // Pointer.current is whichever of mouse, touch or pen was used last
        Pointer pointer = Pointer.current;
        if (cam == null || pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        Vector2 pointerWorld = cam.ScreenToWorldPoint(screenPos);

        // Start aiming anywhere on the screen
        if (pointer.press.wasPressedThisFrame && CanShoot() && !PointerOverUI())
        {
            isDragging = true;
            dragStart = pointerWorld;
            if (aimLine != null) aimLine.enabled = true;
        }

        if (!isDragging) return;

        // Cancel the aim if the game state changes mid-pull
        if (!CanShoot())
        {
            CancelDrag();
            return;
        }

        // Pull is measured from where the press started, not from the ball
        Vector2 pull = Vector2.ClampMagnitude(pointerWorld - dragStart, maxPullDistance);

        if (aimLine != null)
        {
            aimLine.SetPosition(0, transform.position);
            aimLine.SetPosition(1, (Vector2)transform.position - pull); // opposite of the pull
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            CancelDrag();

            if (pull.magnitude < minPullToLaunch) return; // too small, no shot and no turn used

            shotStart = rb.position; // remembered so the ball can return here after falling in a hole

            // Same result as an impulse, but the velocity is set instantly so the stop check below can't fire early
            rb.linearVelocity = -pull * launchPower / rb.mass;
            launched = true;

            if (GameManager.instance != null) GameManager.instance.OnBallLaunched();
        }
    }

    void FixedUpdate()
    {
        if (falling) return; // the fall animation decides when this shot ends

        // Drag alone only slows the ball asymptotically, so snap to a stop when it's nearly still
        if (rb.linearVelocity.sqrMagnitude < stopSpeed * stopSpeed)
        {
            rb.linearVelocity = Vector2.zero;

            if (launched)
            {
                launched = false;
                if (GameManager.instance != null) GameManager.instance.OnBallStopped();
            }
        }
    }

    //-----Holes-----//

    //Called by a hole when the ball's centre reaches its middle
    public void FallIntoHole(Vector2 holeCentre)
    {
        if (!launched || falling) return;

        fallRoutine = StartCoroutine(FallRoutine(holeCentre));
    }

    IEnumerator FallRoutine(Vector2 holeCentre)
    {
        falling = true;

        // Freeze the ball so physics can't push it around while it drops
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Vector3 from = transform.position;
        Vector3 to = new Vector3(holeCentre.x, holeCentre.y, from.z);

        // Slide to the middle of the hole while shrinking (unscaled time so orange peg slow-mo doesn't drag it out)
        float t = 0f;
        while (t < fallTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fallTime);
            transform.position = Vector3.Lerp(from, to, k);
            transform.localScale = startScale * (1f - k);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(respawnPause);

        // Back to where the shot started
        transform.localScale = startScale;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = shotStart;
        transform.position = new Vector3(shotStart.x, shotStart.y, from.z);

        launched = false;
        falling = false;
        fallRoutine = null;

        if (GameManager.instance != null) GameManager.instance.OnBallFellInHole();
    }
}