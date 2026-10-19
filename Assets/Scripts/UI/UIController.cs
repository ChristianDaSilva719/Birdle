using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public TextMeshProUGUI ScoreText;
    private int score = 0;

    // Update is called once per frame
    void Update()
    {
        if(score < GameManager.instance.RoundScore)
        {
            score++;
        }
        ScoreText.text = $"Score: {score}";
    }
}
