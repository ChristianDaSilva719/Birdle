using System.Collections;
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
    public PegType Peg; //The Type of peg

    [SerializeField] float PegDestroyTime = 5; //How long it takes for the peg to be destoryed

    [Header("Peg Effects")]
    public Color PegColour;
    public Color DisabledColour;
    [SerializeField] SpriteRenderer PegBoarderSR;
    [SerializeField] SpriteRenderer InnerPegSR;

    [Header("SlowMo")]
    public bool SlowMoActive = false;
    [SerializeField] private float TimeSpeedRate;
    [SerializeField] private float TimeSpeedTarget;
    private float TimeScaleSpeed = 1;


    private void Awake()
    {

    }

    void Start()
    {
        TimeScaleSpeed = 1;
        SlowMoActive = false;

        //Colour Setup
        PegBoarderSR.color = new Color(PegColour.r + 0.5f, PegColour.g + 0.5f, PegColour.b + 0.5f);
        InnerPegSR.color = PegColour;

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Peg == PegType.PEG_REQUIRED)
        {
            if (SlowMoActive)
            {
                EnableSlowMo();

            }
            else
            {
                EndSlowMo();
            }
        }

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Ball")
        {
            if (Peg == PegType.PEG_REQUIRED)
            {
                SlowMoActive = true;
            }
        }

    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Ball")
        {
            if (Peg == PegType.PEG_REQUIRED)
            {
                SlowMoActive = false;
            }
        }
    }

    //-----Slow Motion Control-----//
    void EnableSlowMo()
    {
        if (TimeScaleSpeed > TimeSpeedTarget)
        {
            TimeScaleSpeed -= TimeSpeedRate;
            TimeScaleSpeed = Mathf.Clamp(TimeScaleSpeed, TimeSpeedTarget, 1);
        }
        Time.timeScale = TimeScaleSpeed;
        Time.fixedDeltaTime = Time.timeScale * .02f;
    }

    void EndSlowMo()
    {
        if (TimeScaleSpeed < 1)
        {
            TimeScaleSpeed += TimeSpeedRate;
        }
        Time.timeScale = TimeScaleSpeed;
    }

    //-----Destroy Peg----//
    void OnDestroyPeg() //Destroys The peg and plays break effects
    {
        Destroy(this.gameObject);
    }

    public void DestroyPeg() //Public function for peg destruction
    {
        StartCoroutine(StartDestroy());
    }

    IEnumerator StartDestroy() //IEnumerator that destroys peg after set amount of Time
    {
        PegBoarderSR.color = new Color(DisabledColour.r + 0.5f, DisabledColour.g + 0.5f, DisabledColour.b + 0.5f);
        InnerPegSR.color = DisabledColour;


        yield return new WaitForSeconds(PegDestroyTime);

        SlowMoActive = false;
        OnDestroyPeg();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.tag == "Ball")
        {
            DestroyPeg();
        }

    }
}
