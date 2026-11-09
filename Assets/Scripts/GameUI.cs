using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//You build the canvas, texts, buttons and panels in the scene yourself (fonts, positions, colours, sizes).
//This script only shows/hides them and fills in the text. Drag your objects into the slots in the Inspector.
//Every slot is optional, anything you leave empty is just skipped.
public class GameUI : MonoBehaviour
{
    [Header("Your Move (any UI object, e.g. a TMP text or an image)")]
    [SerializeField] RectTransform yourMoveObject;
    [SerializeField] float popStartScale = 0.5f; //Starts at this fraction of the size you set in the scene
    [SerializeField] float popTime = 0.25f;
    [SerializeField] float holdTime = 1f;
    [SerializeField] float fadeTime = 0.4f;

    [Header("Level Complete")]
    [SerializeField] GameObject levelCompletePanel;
    [SerializeField] TMP_Text levelCompleteTitle;
    [SerializeField] string levelCompleteTitleFormat = "Level {level} complete!";
    [SerializeField] TMP_Text levelCompleteDetails;
    [TextArea(2, 5)]
    [SerializeField] string levelCompleteDetailsFormat = "Score +{banked}\nTurns left: {turns}\nTotal: {total}";
    [SerializeField] Button nextLevelButton;

    [Header("Game Over")]
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] Button reviveButton;   //Hidden automatically when no revive is available
    [SerializeField] Button tryAgainButton;
    public TextMeshProUGUI ScoreText;
    public GameObject NewBestScoreObj;
    public TextMeshProUGUI LevelText;
    public GameObject NewBestLevelObj;

    [Header("Setup")]
    [SerializeField] bool createEventSystemIfMissing = true; //Buttons need an EventSystem to be clickable

    private GameManager gm;
    private CanvasGroup yourMoveGroup;
    private Vector3 yourMoveBaseScale = Vector3.one;
    private Coroutine yourMoveRoutine;

    void Awake()
    {
        //Use FindObjectOfType<GameManager>() instead on Unity 2022 and older
        gm = GameManager.instance != null ? GameManager.instance : FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameUI: there is no GameManager in the scene.");
            enabled = false;
            return;
        }

        if (createEventSystemIfMissing && FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        if (yourMoveObject != null)
        {
            yourMoveBaseScale = yourMoveObject.localScale;

            //The CanvasGroup handles the fade, and stops the text from blocking the player's aim
            yourMoveGroup = yourMoveObject.GetComponent<CanvasGroup>();
            if (yourMoveGroup == null) yourMoveGroup = yourMoveObject.gameObject.AddComponent<CanvasGroup>();
            yourMoveGroup.blocksRaycasts = false;
            yourMoveGroup.interactable = false;

            yourMoveObject.gameObject.SetActive(false);
        }

        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelPressed);
        if (reviveButton != null) reviveButton.onClick.AddListener(OnRevivePressed);
        if (tryAgainButton != null) tryAgainButton.onClick.AddListener(OnTryAgainPressed);

        gm.OnLevelStarted += HandleLevelStarted;
        gm.OnLevelComplete += HandleLevelComplete;
        gm.OnGameOver += HandleGameOver;
        gm.OnYourMove += HandleYourMove;
        gm.OnShotFired += HandleShotFired;
    }

    void OnDestroy()
    {
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(OnNextLevelPressed);
        if (reviveButton != null) reviveButton.onClick.RemoveListener(OnRevivePressed);
        if (tryAgainButton != null) tryAgainButton.onClick.RemoveListener(OnTryAgainPressed);

        if (gm == null) return;

        gm.OnLevelStarted -= HandleLevelStarted;
        gm.OnLevelComplete -= HandleLevelComplete;
        gm.OnGameOver -= HandleGameOver;
        gm.OnYourMove -= HandleYourMove;
        gm.OnShotFired -= HandleShotFired;
    }

    //-----Game events-----//
    void HandleLevelStarted(int level)
    {
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        ShowYourMove();
    }

    void HandleYourMove()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false); //Also fires after a revive
        ShowYourMove();
    }

    void HandleShotFired()
    {
        HideYourMove();
    }

    void HandleLevelComplete(int banked, int turnsLeft)
    {
        HideYourMove();

        if (levelCompleteTitle != null)
        {
            levelCompleteTitle.text = levelCompleteTitleFormat
                .Replace("{level}", gm.CurrentLevel.ToString())
                .Replace("{banked}", banked.ToString())//Score without multiply
                .Replace("{turns}", (turnsLeft).ToString()) //Bonus
                .Replace("{total}", gm.GameScore.ToString());
        }

        if (levelCompleteDetails != null)
        {
            levelCompleteDetails.text = levelCompleteDetailsFormat
                .Replace("{level}", gm.CurrentLevel.ToString())
                .Replace("{banked}", banked.ToString())//Score without multiply
                .Replace("{turns}", (turnsLeft).ToString()) //Bonus
                .Replace("{total}", gm.GameScore.ToString());
        }

        if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
    }

    void HandleGameOver()
    {
        HideYourMove();
        FinalScoreTally();

        if (reviveButton != null) reviveButton.gameObject.SetActive(gm.CanRevive);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    //-----Button presses-----//
    void OnNextLevelPressed() { gm.NextLevel(); }
    void OnRevivePressed() { gm.ReviveWithAd(); }
    void OnTryAgainPressed() { gm.RestartLevel(); }

    //-----Your move-----//
    void ShowYourMove()
    {
        if (yourMoveObject == null) return;

        if (yourMoveRoutine != null) StopCoroutine(yourMoveRoutine);
        yourMoveRoutine = StartCoroutine(YourMoveRoutine());
    }

    void HideYourMove()
    {
        if (yourMoveObject == null) return;

        if (yourMoveRoutine != null) StopCoroutine(yourMoveRoutine);
        yourMoveRoutine = null;
        yourMoveObject.gameObject.SetActive(false);
    }

    void FinalScoreTally()
    {
        ScoreText.text = $"Score: {gm.GameScore}";
        Debug.Log($"Final Score: {gm.GameScore} & last score: {PlayerPrefs.GetInt("HighScore")}");

        if (gm.GameScore > PlayerPrefs.GetInt("HighScore"))
        {
            PlayerPrefs.SetInt("HighScore", gm.GameScore);
            Debug.Log("New High Score");
            NewBestScoreObj.SetActive(true);
        }
        else
        {
            NewBestScoreObj.SetActive(false);
        }

            LevelText.text = $"Level: {gm.CurrentLevel}";
        if(gm.CurrentLevel > PlayerPrefs.GetInt("HighestLevel") || PlayerPrefs.GetInt("HighestLevel") == null )
        {
            PlayerPrefs.SetInt("HighestLevel", gm.CurrentLevel);
            Debug.Log("New Highest Level");
            NewBestLevelObj.SetActive(true);
        }
        else
        {
            NewBestLevelObj.SetActive(false);
        }

    }

    IEnumerator YourMoveRoutine()
    {
        yourMoveObject.gameObject.SetActive(true);
        yourMoveGroup.alpha = 1f;

        //Pop in (unscaled time so the orange peg slow-mo doesn't slow it down)
        float t = 0f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popTime);
            yourMoveObject.localScale = yourMoveBaseScale * Mathf.LerpUnclamped(popStartScale, 1f, EaseOutBack(k));
            yield return null;
        }
        yourMoveObject.localScale = yourMoveBaseScale;

        yield return new WaitForSecondsRealtime(holdTime);

        //Fade out
        t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            yourMoveGroup.alpha = 1f - Mathf.Clamp01(t / fadeTime);
            yield return null;
        }

        yourMoveObject.gameObject.SetActive(false);
        yourMoveRoutine = null;
    }

    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = k - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}