using UnityEngine;

//Draws the ball as a lit, rolling sphere. Add this to the same object as DragLaunchBall
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BallVisual : MonoBehaviour
{
    [Header("Shader")]
    public Shader ballShader; //Drag RollingBall in here, a shader that's only found by name can be left out of a build

    [Header("Sprite on top")]
    public bool keepSpriteOnTop = true;   //Your sprite stays drawn over the rolling sphere and doesn't roll itself
    public SpriteRenderer overlaySprite;  //Optional: which sprite to use. Leave empty to use the first one on the ball

    [Header("Look")]
    public Color baseColour = new Color(0.96f, 0.96f, 0.98f, 1f);
    public Color panelColour = new Color(0.62f, 0.70f, 0.88f, 1f);
    public Color capColour = new Color(0.14f, 0.17f, 0.32f, 1f);
    [Range(2, 12)] public int panels = 6;             //Keep this even so the colours alternate evenly
    [Range(0f, 1f)] public float highlightStrength = 0.6f; //Turn this down if your sprite already has a shine
    [Range(0f, 1f)] public float shadowStrength = 0.35f;
    public int sortingOrderOffset = 1;                //1 = drawn just above the old flat ball, so it sits on top of pegs

    [Header("Rolling")]
    public float rollMultiplier = 1f;                 //1 = rolls exactly as fast as it travels, higher = spins faster

    const float Extent = 1.5f; //How much bigger than the ball the drawing area is, the extra room is for the shadow
    static readonly int RotId = Shader.PropertyToID("_BallRot");

    private Rigidbody2D rb;
    private CircleCollider2D circle;
    private Material mat;
    private Quaternion orientation = Quaternion.identity; //Which way the ball is turned
    private bool ready;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        circle = GetComponent<CircleCollider2D>();
        BuildVisual();
    }

    void BuildVisual()
    {
        Shader shader = ballShader != null ? ballShader : Shader.Find("Custom/RollingBall");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("BallVisual: the RollingBall shader wasn't found, so the flat ball is kept. Drag the shader into the Ball Shader slot.");
            return;
        }

        //Find the ball's sprite and remember how it was sorted
        int layerId = 0;
        int order = 0;
        SpriteRenderer[] flat = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer top = overlaySprite != null ? overlaySprite : (flat.Length > 0 ? flat[0] : null);
        if (top != null)
        {
            layerId = top.sortingLayerID;
            order = top.sortingOrder;
        }

        //Hide the flat sprites, except the one that should stay on top of the sphere
        foreach (SpriteRenderer sr in flat)
        {
            if (keepSpriteOnTop && sr == top) continue;
            sr.enabled = false;
        }

        mat = new Material(shader);
        mat.SetColor("_BaseColour", baseColour);
        mat.SetColor("_PanelColour", panelColour);
        mat.SetColor("_CapColour", capColour);
        mat.SetFloat("_Panels", panels);
        mat.SetFloat("_SpecStrength", highlightStrength);
        mat.SetFloat("_ShadowStrength", shadowStrength);
        mat.SetFloat("_Extent", Extent);
        mat.SetMatrix(RotId, Matrix4x4.identity);

        //A flat square that the shader draws the whole ball on, it follows the ball and shrinks with it
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BallVisual";
        quad.layer = gameObject.layer;
        Destroy(quad.GetComponent<Collider>());

        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = circle.offset;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one * (circle.radius * 2f * Extent);

        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingLayerID = layerId;
        mr.sortingOrder = order + sortingOrderOffset;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        //Your sprite goes one step above the sphere, so it's drawn over it
        if (keepSpriteOnTop && top != null)
        {
            top.enabled = true;
            top.sortingLayerID = layerId;
            top.sortingOrder = mr.sortingOrder + 1;
        }

        ready = true;
    }

    void Update()
    {
        if (!ready || Time.deltaTime <= 0f) return; //Nothing to do before it's built or while paused

        Vector2 v = rb.linearVelocity; // use rb.velocity on Unity 2022 and older
        float speed = v.magnitude;
        if (speed < 0.001f) return;

        //Radius in world units (it shrinks while the ball drops into a hole)
        float radius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        if (radius <= 0.0001f) return;

        //Rolling across a surface behind the ball: the turn axis is sideways to the direction of travel
        Vector3 axis = new Vector3(v.y, -v.x, 0f) / speed;
        float degrees = speed * Time.deltaTime / radius * Mathf.Rad2Deg * rollMultiplier;

        orientation = Quaternion.AngleAxis(degrees, axis) * orientation;
        orientation.Normalize();

        //The shader needs the opposite turn, to find which point of the pattern is facing the camera
        mat.SetMatrix(RotId, Matrix4x4.Rotate(Quaternion.Inverse(orientation)));
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}