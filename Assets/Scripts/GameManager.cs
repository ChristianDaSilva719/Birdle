using System;
using System.Collections.Generic;
using UnityEngine;

public enum State
{
    Playing,
    Paused,
    GameOver,
    LevelComplete
}

public class GameManager : MonoBehaviour
{
    [Header("Game Info")]
    public static GameManager instance;
    public State GameState;
    public int CurrentLevel = 1; //Only ever goes up, there is no way back to earlier levels

    [Header("References")]
    [SerializeField] LevelGenerator levelGenerator;
    [SerializeField] DragLaunchBall ball;
    [SerializeField] UIController UIcontrol;

    [Header("Ball Info")]
    public int AmountOfBalls; //The turns the player has left
    public bool BallAlive;    //True from the moment the ball is shot until it stops

    [Header("Player Info")]
    public int RoundScore; //The Score of the current Round
    public int GameScore;  //The Score banked from finished levels
    public float ScoreMultipler = 1f; //How much points are increased by
    [SerializeField] int turnBonusScore = 100; //Points for every turn left over when a level is beaten

    [Header("Level Progress")]
    public int RequiredPegsLeft;

    [Header("Holes")]
    [SerializeField] int holeExtraTurnPenalty = 0; //Extra turns lost on top of the shot itself when the ball falls in a hole

    [Header("Revive")]
    [SerializeField] int reviveTurns = 3;           //Extra turns given by a rewarded ad
    [SerializeField] int maxRevivesPerAttempt = 1;
    private int revivesUsed;

    [Header("SlowMo")]
    [SerializeField] float slowMoTimeScale = 0.3f;
    [SerializeField] float slowMoSpeed = 4f; //How fast the time scale eases in and out
    private int slowMoRequests;
    private float defaultFixedDelta;

    //Pegs the ball has touched this shot, they disappear when the ball stops
    private readonly List<PegBehaviour> hitPegs = new List<PegBehaviour>();

    //The GameUI listens to these
    public event Action<int> OnLevelStarted;        //level number
    public event Action<int, int> OnLevelComplete;  //score banked, turns left
    public event Action OnGameOver;
    public event Action OnYourMove;                 //ball has stopped and the player can shoot again
    public event Action OnShotFired;                //ball has just been launched

    public bool CanShoot => GameState == State.Playing && !BallAlive && AmountOfBalls > 0;
    public bool CanRevive => GameState == State.GameOver && revivesUsed < maxRevivesPerAttempt;

    private void Awake()
    {
        instance = this;
        defaultFixedDelta = Time.fixedDeltaTime;

        if (FindFirstObjectByType<GameUI>() == null)
            new GameObject("GameUI").AddComponent<GameUI>();
    }

    private void Start()
    {
        CurrentLevel = 1;
        BeginAttempt(levelGenerator.GenerateNewLevel(CurrentLevel));
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDelta;
    }

    void Update()
    {
        if (GameState == State.Paused) return;

        float target = (GameState == State.Playing && slowMoRequests > 0)
            ? Mathf.Max(0.05f, slowMoTimeScale)
            : 1f;

        Time.timeScale = Mathf.MoveTowards(Time.timeScale, target, slowMoSpeed * Time.unscaledDeltaTime);
        Time.fixedDeltaTime = defaultFixedDelta * Time.timeScale;
    }

    //-----Slow Motion-----//
    public void AddSlowMoRequest(int amount)
    {
        slowMoRequests = Mathf.Max(0, slowMoRequests + amount);
    }

    //-----Level Flow-----//
    void BeginAttempt(LevelData data)
    {
        AmountOfBalls = data.turns;
        RequiredPegsLeft = data.requiredCount;
        RoundScore = 0;
        UIcontrol.ResetScoreText();
        BallAlive = false;
        revivesUsed = 0;
        hitPegs.Clear();

        ball.ResetTo(levelGenerator.BallStartPosition);

        GameState = State.Playing;
        OnLevelStarted?.Invoke(CurrentLevel);
    }

    //Beaten level -> brand new randomised level
    public void NextLevel()
    {
        if (GameState != State.LevelComplete) return;

        CurrentLevel++;
        BeginAttempt(levelGenerator.GenerateNewLevel(CurrentLevel));
    }

    //Lost -> same peg positions, power-ups stay put, orange pegs are re-rolled, turns reset
    public void RestartLevel()
    {
        if (GameState == State.LevelComplete) return;

        CurrentLevel = 1;
        GameScore = 0;
        BeginAttempt(levelGenerator.GenerateNewLevel(CurrentLevel));
    }

    //Lost -> watch an ad to carry on with the board exactly as it is
    public void ReviveWithAd()
    {
        if (!CanRevive) return;

        AdManager.ShowRewardedAd(success =>
        {
            if (!success || GameState != State.GameOver) return;

            revivesUsed++;
            AmountOfBalls += reviveTurns;
            GameState = State.Playing;
            OnYourMove?.Invoke();
        });
    }

    //-----Called by the ball-----//
    public void OnBallLaunched()
    {
        AmountOfBalls--;
        BallAlive = true;
        OnShotFired?.Invoke();
    }

    public void OnBallStopped()
    {
        BallAlive = false;

        if (GameState != State.Playing) return;

        ClearHitPegs(); //The pegs the ball touched disappear now that the ball can move again

        if (RequiredPegsLeft <= 0)
            CompleteLevel();
        else if (AmountOfBalls <= 0)
            EndGame();
        else
            OnYourMove?.Invoke();
    }

    //The ball dropped into a hole and has been put back where the shot started
    //The turn for the shot is already spent, this just takes any extra penalty and then ends the shot as normal
    public void OnBallFellInHole()
    {
        AmountOfBalls = Mathf.Max(0, AmountOfBalls - holeExtraTurnPenalty);
        OnBallStopped();
    }

    //-----Called by the pegs-----//
    public void RegisterHitPeg(PegBehaviour peg)
    {
        hitPegs.Add(peg);
    }

    public void OnRequiredPegHit()
    {
        RequiredPegsLeft = Mathf.Max(0, RequiredPegsLeft - 1);
    }

    public void OnPowerUpHit()
    {
        //Put the power-up effect here (extra ball, bigger ball, etc.)
        Debug.Log("Power-up hit");
    }

    void ClearHitPegs()
    {
        foreach (PegBehaviour peg in hitPegs)
        {
            if (peg != null) peg.DestroyPeg();
        }
        hitPegs.Clear();
    }

    //-----Level end-----//
    void CompleteLevel()
    {
        GameState = State.LevelComplete;

        int turnBonus = AmountOfBalls * turnBonusScore; //Every turn left over adds to the score
        float multiplier = ScoreMultipler > 0f ? ScoreMultipler : 1f;
        int banked = Mathf.RoundToInt((RoundScore + turnBonus) * multiplier);
        GameScore += banked;

        OnLevelComplete?.Invoke(RoundScore, turnBonus);
    }

    void EndGame()
    {
        GameState = State.GameOver;
        OnGameOver?.Invoke();
    }


}