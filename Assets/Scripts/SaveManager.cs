using System;
using UnityEngine;

[Serializable]
public class PegSave
{
    public Vector2 position;
    public PegType type;
}

//Everything needed to rebuild the level you were on
[Serializable]
public class SaveData
{
    public int version;

    //Progress
    public int level;
    public int gameScore;       //Score banked from finished levels
    public int roundScore;      //Score of the level in progress
    public int turnsLeft;
    public int requiredPegsLeft;
    public bool levelCompleted; //True if the player quit on the "level complete" screen, so Continue starts the next level

    //Board
    public string shapeName;
    public bool flipX;
    public bool flipY;
    public Vector2 boardSize;

    //Ball
    public Vector2 ballPosition;

    //Holes (they all share one size)
    public float holeRadius;
    public float holeCaptureFraction;
    public Vector2[] holes;

    //Pegs still on the board
    public PegSave[] pegs;
}

public static class SaveManager
{
    const string Key = "SavedGame";
    const int CurrentVersion = 1;

    public static bool HasSave()
    {
        return Load() != null;
    }

    public static void Save(SaveData data)
    {
        data.version = CurrentVersion;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    //Returns null if there is no save, or if it's broken / from an older version
    public static SaveData Load()
    {
        if (!PlayerPrefs.HasKey(Key)) return null;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));

            if (data == null || data.version != CurrentVersion || data.pegs == null || string.IsNullOrEmpty(data.shapeName))
            {
                Debug.LogWarning("SaveManager: save data is invalid, ignoring it.");
                return null;
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveManager: couldn't read the save (" + e.Message + ").");
            return null;
        }
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}