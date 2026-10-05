using Unity.VisualScripting;
using UnityEngine;

public class DragLaunchBall : MonoBehaviour
{
    [Header("Launch")]
    public float launchPower = 8f;     
    public float maxPullDistance = 3f; 

    [Header("Movement")]
    public float slowDownDrag = 1.5f;  
    public float stopSpeed = 0.1f;     

    [Header("Aim line")]
    public LineRenderer aimLine;       

    private Rigidbody2D rb;
    private Camera cam;
    private bool isDragging;
    private Vector2 dragStart;
    private bool canMove;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        rb.linearDamping = slowDownDrag;

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
    }

    void Update()
    {
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        if (canMove == true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                dragStart = mouseWorld;
                if (aimLine != null) aimLine.enabled = true;
            }
        }

        if (isDragging)
        {
            Vector2 pull = Vector2.ClampMagnitude(mouseWorld - dragStart, maxPullDistance);

            if (aimLine != null)
            {
                aimLine.SetPosition(0, transform.position);
                aimLine.SetPosition(1, (Vector2)transform.position - pull);
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                canMove = false;
                if (aimLine != null) aimLine.enabled = false;
                

                rb.linearVelocity = Vector2.zero; 
                rb.AddForce(-pull * launchPower, ForceMode2D.Impulse);
            }
        }
    }

    void FixedUpdate()
    {
        if (rb.linearVelocity.magnitude < stopSpeed)
        {
            rb.linearVelocity = Vector2.zero;
            canMove = true;
        }  
    }

}