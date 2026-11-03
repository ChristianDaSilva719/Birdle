using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public TextMeshProUGUI ScoreText;
    public TextMeshProUGUI TunrsText;
    private int score = 0;

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
}
