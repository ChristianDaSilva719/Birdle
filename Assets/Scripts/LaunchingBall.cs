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

    [Header("Aim line (optional)")]
    public LineRenderer aimLine;

    private Rigidbody2D rb;
    private Camera cam;
    private bool isDragging;
    private bool launched; // true from release until the ball has stopped
    private Vector2 dragStart;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

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

            // Same result as an impulse, but the velocity is set instantly so the stop check below can't fire early
            rb.linearVelocity = -pull * launchPower / rb.mass;
            launched = true;

            if (GameManager.instance != null) GameManager.instance.OnBallLaunched();
        }
    }

    void FixedUpdate()
    {
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
}