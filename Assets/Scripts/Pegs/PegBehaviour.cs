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
    [Header("Colliders")]
    public CircleCollider2D PegCollider;
    public CircleCollider2D SlowMoCollider;

    [Header("Peg Settings")]
    public PegType PegType; //The Type of peg

    float PegDestroyTime = 5; //How long it takes for the peg to be destoryed

    [Header("Peg Effects")]
    public Color PegColour;
    [SerializeField] SpriteRenderer PegBoarderSR;
    [SerializeField] SpriteRenderer InnerPegSR;

    private bool SlowMoActive = false;
    [SerializeField] private float TimeSpeedRate;
    [SerializeField] private float TimeSpeedTarget;
    private float TimeScaleSpeed = 1;

    private void Awake()
    {
        PegCollider = GetComponent<CircleCollider2D>();
    }

    void Start()
    {
        TimeScaleSpeed = 1;
        SlowMoActive = false;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if(SlowMoActive)
        {
            EnableSlowMo();
        }
        else
        {
            EndSlowMo();
        }


    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        SlowMoActive = true;

    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        SlowMoActive = false;
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
        Destroy(this);
    }

    public void DestroyPeg() //Public function for peg destruction
    {
        StartCoroutine(StartDestroy());
    }

    IEnumerator StartDestroy() //IEnumerator that destroys peg after set amount of Time
    {


        yield return new WaitForSeconds(PegDestroyTime);

        OnDestroyPeg();
    }
}
