using UnityEngine;

public enum State
{
    Playing,
    Paused,
    GameOver
}

public class GameManager : MonoBehaviour
{
    

    [Header("Game Info")]
    public static GameManager instance;
    public State GameState;

    [Header("Ball Info")]
    [SerializeField] int BallStartAmount; //Starting amount of balls
    public int AmountOfBalls; //The Amount of balls the player has left to use
    public bool BallAlive; //The check for if the ball is in play

    [Header("Player Info")]
    public int RoundScore; //The Score of the current Round
    public int GameScore; //The Score at the end of the game
    public float ScoreMultipler; //How much points are increased by

    private void Awake()
    {
            instance = this;

    }

    private void Start()
    {
        AmountOfBalls = BallStartAmount;
        GameState = State.Playing;
    }

    // Update is called once per frame
    void Update()
    {
        if(AmountOfBalls <= 0 && BallAlive == false)
        {
            GameState = State.GameOver;
        }
    }
}
