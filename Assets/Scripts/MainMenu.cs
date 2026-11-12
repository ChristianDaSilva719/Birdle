using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{

    public TextMeshProUGUI ScoreText;
    public TextMeshProUGUI LevelText;

    public void SetUpHighScore()
    {
        if(PlayerPrefs.GetInt("HighScore") < 1)
        {
            ScoreText.text = $"Score: None";
            LevelText.text = $"Level: None";
        }
        else
        {
            ScoreText.text = $"Score: {PlayerPrefs.GetInt("HighScore")}";
            LevelText.text = $"Level: {PlayerPrefs.GetInt("HighestLevel")}";
        }


    }

    public void ResetHighScore()
    {
        PlayerPrefs.SetInt("HighScore", 0);
        PlayerPrefs.SetInt("HighestLevel", 0);
        SetUpHighScore();
    }

    public void Play(string _LevelToLoad) 
    {
        SceneManager.LoadScene(_LevelToLoad);
    }

}
