using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public TextMeshProUGUI ScoreText;
    public TextMeshProUGUI TunrsText;
    private int score = 0;
    public bool GamePaused = false;
    public GameObject GamePauseScreen;
    [SerializeField] private GameManager GM;

    // Update is called once per frame
    void Update()
    {
        if(score < GameManager.instance.RoundScore)
        {
            score++;
        }
        ScoreText.text = $"Score: {score}";
        TunrsText.text = $"Moves left: {GameManager.instance.AmountOfBalls}";
    }

    public void ResetScoreText()
    {
        score = 0;
    }

    public void PauseGame()
    {
        if(GamePauseScreen.activeSelf)
        {
            GamePaused = false;
            GM.GameState = State.Playing;
        }
        else
        {
            GamePaused = true;
            GM.GameState = State.Paused;
        }
        GamePauseScreen.SetActive(GamePaused);
    }
}
