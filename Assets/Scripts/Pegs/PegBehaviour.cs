using UnityEngine;

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

    private bool slowMoRequested = false;

    private void Awake()
    {
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
    public void DestroyPeg() //Destroys The peg and plays break effects
    {
        Destroy(this.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Ball") || BeenHit) return;
        BeenHit = true;

        //Greys out straight away but stays solid, the GameManager removes it when the ball stops
        PegBoarderSR.color = new Color(DisabledColour.r + 0.5f, DisabledColour.g + 0.5f, DisabledColour.b + 0.5f);
        InnerPegSR.color = DisabledColour;

        GameManager.instance.RoundScore += ScoreAmount;
        GameManager.instance.RegisterHitPeg(this);

        if (Peg == PegType.PEG_REQUIRED)
            GameManager.instance.OnRequiredPegHit();
        else if (Peg == PegType.PEG_POWERUP)
            GameManager.instance.OnPowerUpHit();
    }
}