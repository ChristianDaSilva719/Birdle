using UnityEngine;
using System.Collections;

public enum PegType
{
    PEG_BASIC,
    PEG_REQUIRED,
    PEG_POWERUP,
    PEG_SPECIAL
}

public class PegBehaviour : MonoBehaviour
{
    [Header("Peg Settings")]
    public PegType Peg; //The Type of peg (set by the LevelGenerator when the peg spawns)
    private bool BeenHit = false;
    private int ScoreAmount;

    [Header("Peg Effects")]
    public Color PegColour;
    public Color DisabledColour;
    [SerializeField] SpriteRenderer PegBoarderSR;
    [SerializeField] SpriteRenderer InnerPegSR;

    [Header("Hit Feedback")]
    [SerializeField] float hitPopScale = 1.35f; //How big the peg gets at the peak of the pop
    [SerializeField] float hitPopTime = 0.25f;  //Total time to grow and shrink back

    private Vector3 borderBaseScale;
    private Vector3 innerBaseScale;

    private bool slowMoRequested = false;

    private void Awake()
    {
        borderBaseScale = PegBoarderSR.transform.localScale;
        innerBaseScale = InnerPegSR.transform.localScale;
        ApplyType();
    }

    //Called by the LevelGenerator right after the peg is spawned
    public void SetType(PegType type)
    {
        Peg = type;
        ApplyType();
    }

    void ApplyType()
    {
        switch (Peg)
        {
            case PegType.PEG_BASIC:
                ScoreAmount = 25;
                PegColour = new Color32(0x5D, 0xFF, 0xF6, 255);
                DisabledColour = new Color32(0x2C, 0x83, 0x9C, 255);
                break;
            case PegType.PEG_REQUIRED:
                ScoreAmount = 50;
                PegColour = new Color32(0xFF, 0x9F, 0x28, 255);
                DisabledColour = new Color32(0x9F, 0x49, 0x2C, 255);
                break;
            case PegType.PEG_POWERUP:
                ScoreAmount = 75;
                PegColour = new Color32(0x52, 0xFF, 0x40, 255);
                DisabledColour = new Color32(0x20, 0x90, 0x01, 255);
                break;
            case PegType.PEG_SPECIAL:
                ScoreAmount = 100; //Special keeps whatever colours are set in the inspector
                break;
        }

        PegBoarderSR.color = new Color(PegColour.r + 0.5f, PegColour.g + 0.5f, PegColour.b + 0.5f);
        InnerPegSR.color = PegColour;
    }

    //-----Slow Motion-----//
    //Pegs only ask for slow-mo, the GameManager is the only thing that touches Time.timeScale
    void SetSlowMo(bool on)
    {
        if (slowMoRequested == on) return;
        slowMoRequested = on;

        if (GameManager.instance != null)
            GameManager.instance.AddSlowMoRequest(on ? 1 : -1);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ball") && Peg == PegType.PEG_REQUIRED)
        {
            //SetSlowMo(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ball") && Peg == PegType.PEG_REQUIRED)
        {
            SetSlowMo(false);
        }
    }

    private void OnDestroy()
    {
        //A destroyed peg never gets an OnTriggerExit, so it hands its slow-mo request back here
        SetSlowMo(false);
    }

    //-----Destroy Peg----//
    //Called by the GameManager once the ball has stopped
    public bool IsRemoved { get; private set; }

    public void DestroyPeg()
    {
        IsRemoved = true; //So a save in the same frame skips this peg
        SoundManager.PlaySound(SoundType.Peg_Break, 1, true);
        Destroy(this.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Ball") || BeenHit) return;
        BeenHit = true;

        //Greys out straight away but stays solid, the GameManager removes it when the ball stops
        StartCoroutine(HitPopRoutine());


        GameManager.instance.RoundScore += ScoreAmount;
        GameManager.instance.RegisterHitPeg(this);

        if (Peg == PegType.PEG_REQUIRED)
        {
            GameManager.instance.OnRequiredPegHit();
        }
        else if (Peg == PegType.PEG_POWERUP)
        {
            GameManager.instance.OnPowerUpHit();
        }

        if (Peg == PegType.PEG_REQUIRED)
        {
            SoundManager.PlaySound(SoundType.Peg_HitSpecial, 1, true);
        }
        else
        {
            SoundManager.PlaySound(SoundType.Peg_Hit, 1, true);
        }

    }
    IEnumerator HitPopRoutine()
    {
        Color borderTarget = new Color(DisabledColour.r + 0.5f, DisabledColour.g + 0.5f, DisabledColour.b + 0.5f);

        float time = 0f;
        while (time < hitPopTime)
        {
            time += Time.unscaledDeltaTime; //Unscaled so the orange peg slow-mo doesn't drag it out
            float k = Mathf.Clamp01(time / hitPopTime);

            //0 -> 1 -> 0 over the animation, so the peg grows and then settles back to normal size
            float pop = Mathf.Sin(k * Mathf.PI);
            float scale = 1f + (hitPopScale - 1f) * pop;
            PegBoarderSR.transform.localScale = borderBaseScale * scale;
            InnerPegSR.transform.localScale = innerBaseScale * scale;

            //Starts as a white flash and fades into the greyed-out colour
            InnerPegSR.color = Color.Lerp(Color.white, DisabledColour, k);
            PegBoarderSR.color = Color.Lerp(Color.white, borderTarget, k);

            yield return null;
        }

        PegBoarderSR.transform.localScale = borderBaseScale;
        InnerPegSR.transform.localScale = innerBaseScale;
        InnerPegSR.color = DisabledColour;
        PegBoarderSR.color = borderTarget;
    }
}