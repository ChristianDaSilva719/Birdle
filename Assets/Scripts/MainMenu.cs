using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("High Score")]
    public TextMeshProUGUI ScoreText;
    public TextMeshProUGUI LevelText;

    [Header("Play Buttons")]
    [SerializeField] TMP_Text playButtonLabel;   //The text on the main button: "Start" or "Continue"
    [SerializeField] GameObject newGameButton;   //Only shown when there is a save
    [SerializeField] string startText = "Start";
    [SerializeField] string continueText = "Continue";

    void Start()
    {
        RefreshPlayButtons();
    }

    //Also call this if your menu panel is shown again later (e.g. after coming back from a settings screen)
    public void RefreshPlayButtons()
    {
        bool hasSave = SaveManager.HasSave();

        if (playButtonLabel != null) playButtonLabel.text = hasSave ? continueText : startText;
        if (newGameButton != null) newGameButton.SetActive(hasSave);
    }

    public void SetUpHighScore()
    {
        if (PlayerPrefs.GetInt("HighScore") < 1)
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

    //Start / Continue button. The GameManager loads the save itself if there is one
    public void Play(string _LevelToLoad)
    {
        SceneManager.LoadScene(_LevelToLoad);
    }

    //New Game button: throws the save away so the GameManager starts from level 1
    public void NewGame(string _LevelToLoad)
    {
        Debug.Log("NewGame pressed, deleting save");
        SaveManager.Delete();
        SceneManager.LoadScene(_LevelToLoad);
    }
}