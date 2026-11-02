using System.Collections.Generic;
using UnityEngine;

public struct LevelData
{
    public int level;
    public int turns;
    public int requiredCount;
    public int totalPegs;
}

public class LevelGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PegBehaviour pegPrefab;
    [SerializeField] Transform pegParent; //Optional, keeps the hierarchy tidy
    [SerializeField] Camera cam;

    [Header("Cleanup")]
    [SerializeField] bool clearLeftoverPegs = true; //Removes pegs left in the scene that this script didn't spawn

    [Header("Peg Spacing")]
    [SerializeField] float pegRadius = 0.25f;  //Radius of the solid part of your peg
    [SerializeField] float minPegGap = 0.8f;   //Free space between pegs, keep this bigger than the ball

    [Header("Board Size (world units)")]
    [SerializeField] Vector2 boardCentre = Vector2.zero;
    [SerializeField] Vector2 fullBoardSize = new Vector2(8f, 14f); //The biggest the board ever gets
    [SerializeField] float firstLevelFill = 0.65f;  //Level 1 board = 65% of the full size
    [SerializeField] int levelsToFullSize = 8;      //The level where the board reaches full size

    [Header("Phone Screen Fit")]
    [SerializeField] bool fitCameraToPhone = true;
    [SerializeField] float sideMargin = 0.08f;   //Share of the screen width left empty on each side
    [SerializeField] float topMargin = 0.18f;    //Share of the screen height kept clear at the top (for UI)
    [SerializeField] float bottomMargin = 0.10f; //Share of the screen height kept clear at the bottom

    [Header("Board Walls")]
    [SerializeField] float wallThickness = 0.2f;
    [SerializeField] Color wallColour = Color.white;
    [SerializeField] Material wallMaterial; //Optional, leave empty to use a plain sprite material
    [SerializeField] int wallSortingOrder = 5;

    [Header("Peg Amounts")]
    [SerializeField] int firstLevelPegs = 6;      //Pegs on level 1 (power-ups included)
    [SerializeField] int pegsPerLevelIncrease = 2; //Extra pegs added each level
    [SerializeField] int maxPegCount = 100;
    [SerializeField] int maxPowerUps = 2;
    [SerializeField] float startRequiredRatio = 0.25f;           //Share of normal pegs that are orange
    [SerializeField] float requiredRatioGrowthPerLevel = 0.01f;
    [SerializeField] float maxRequiredRatio = 0.4f;

    [Header("Turns")]
    [SerializeField] int bonusTurns = 3;                       //Spare turns on top of the maths below
    [SerializeField] float startTurnsPerRequired = 1f;         //1 = one turn for every orange peg
    [SerializeField] float turnsPerRequiredDropPerLevel = 0.05f;
    [SerializeField] float minTurnsPerRequired = 0.5f;         //0.5 = you need ~2 orange pegs per shot

    [Header("Ball Start")]
    [SerializeField] float ballStartBottomOffset = 1f; //How far above the bottom wall the ball starts
    [SerializeField] float ballClearRadius = 1.5f;     //No pegs spawn this close to the ball start

    public Vector2 BallStartPosition { get; private set; }

    private readonly List<Vector2> positions = new List<Vector2>();
    private readonly HashSet<int> powerUpIndices = new HashSet<int>();
    private readonly List<PegBehaviour> spawnedPegs = new List<PegBehaviour>();
    private EdgeCollider2D wallCollider;
    private LineRenderer wallLine;
    private int currentLevel;

    //-----Safety-----//

    //Returns false if the peg prefab would clone a manager or the ball every time it spawns
    bool PrefabIsSafe()
    {
        if (pegPrefab == null)
        {
            Debug.LogError("LevelGenerator: Peg Prefab is not assigned.");
            return false;
        }

        GameObject go = pegPrefab.gameObject;

        if (go.GetComponentInChildren<LevelGenerator>(true) != null ||
            go.GetComponentInChildren<GameManager>(true) != null ||
            go.GetComponentInChildren<DragLaunchBall>(true) != null)
        {
            Debug.LogError("LevelGenerator: the peg prefab '" + go.name + "' has LevelGenerator, GameManager or DragLaunchBall on it. " +
                           "Spawning pegs would clone those too and freeze Unity. Remove them from the peg and put them on their own objects.");
            return false;
        }

        if (go.scene.IsValid())
        {
            Debug.LogWarning("LevelGenerator: Peg Prefab '" + go.name + "' is an object in the scene, not a prefab asset. Drag the prefab from the Project window instead.");
        }

        return true;
    }

    //-----Public-----//

    //Brand new layout: new board size, new peg positions, new power-up positions
    public LevelData GenerateNewLevel(int level)
    {
        currentLevel = level;

        if (!PrefabIsSafe())
            return new LevelData { level = level, turns = 0, requiredCount = 0, totalPegs = 0 };

        if (clearLeftoverPegs) RemoveLeftoverPegs();

        Vector2 size = BoardSizeFor(level);
        BuildBoard(size);

        int wanted = Mathf.Min(firstLevelPegs + pegsPerLevelIncrease * (level - 1), maxPegCount);
        PlacePegs(wanted, size);
        PickPowerUps();

        LevelData data = SpawnPegs();
        Debug.Log("LevelGenerator: level " + level + " asked for " + wanted + " pegs, placed " + data.totalPegs + ", board size " + size);
        return data;
    }

    //Same positions and same power-ups, orange pegs are re-rolled
    public LevelData RespawnSameLevel()
    {
        if (!PrefabIsSafe())
            return new LevelData { level = currentLevel, turns = 0, requiredCount = 0, totalPegs = 0 };

        return SpawnPegs();
    }

    //-----Cleanup-----//
    void RemoveLeftoverPegs()
    {
        //Use FindObjectsOfType<PegBehaviour>() instead on Unity 2022 and older
        PegBehaviour[] all = FindObjectsByType<PegBehaviour>(FindObjectsSortMode.None);
        int removed = 0;

        foreach (PegBehaviour peg in all)
        {
            if (peg == pegPrefab || spawnedPegs.Contains(peg)) continue;

            peg.gameObject.SetActive(false); //Stops it colliding for the rest of this frame
            Destroy(peg.gameObject);
            removed++;
        }

        if (removed > 0)
            Debug.LogWarning("LevelGenerator: removed " + removed + " leftover pegs that were already in the scene.");
    }

    //-----Board-----//

    //Grows from firstLevelFill of the full size up to the full size over levelsToFullSize levels
    Vector2 BoardSizeFor(int level)
    {
        float t = levelsToFullSize <= 1 ? 1f : Mathf.Clamp01((level - 1) / (float)(levelsToFullSize - 1));
        float fill = Mathf.Lerp(firstLevelFill, 1f, t);
        return fullBoardSize * fill;
    }

    //Zooms the camera so the FULL size board fits the phone screen with the margins around it
    //The camera stays the same every level, only the board grows inside it
    void FitCameraToPhone()
    {
        Camera camera = cam != null ? cam : Camera.main;
        if (camera == null || !camera.orthographic) return;

        float freeW = Mathf.Max(0.1f, 1f - sideMargin * 2f);
        float freeH = Mathf.Max(0.1f, 1f - topMargin - bottomMargin);

        //Outer edge of the walls counts as part of the board
        float neededW = fullBoardSize.x + wallThickness;
        float neededH = fullBoardSize.y + wallThickness;

        //Whichever of width or height is the tighter fit on this phone decides the zoom
        float visibleHeight = Mathf.Max(neededH / freeH, (neededW / freeW) / camera.aspect);
        camera.orthographicSize = visibleHeight * 0.5f;

        //Shift the camera so the board sits in the middle of the free area (the top margin is bigger for UI)
        float yShift = (topMargin - bottomMargin) * 0.5f * visibleHeight;
        camera.transform.position = new Vector3(boardCentre.x, boardCentre.y + yShift, camera.transform.position.z);
    }

    void BuildBoard(Vector2 size)
    {
        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        Vector2 c = boardCentre;

        if (wallCollider == null)
        {
            GameObject walls = new GameObject("BoardWalls");
            wallCollider = walls.AddComponent<EdgeCollider2D>();
            wallLine = walls.AddComponent<LineRenderer>();
        }

        //Collision: closed loop, edgeRadius gives the walls the same thickness as the drawn line
        wallCollider.points = new Vector2[]
        {
            new Vector2(c.x - hw, c.y - hh),
            new Vector2(c.x - hw, c.y + hh),
            new Vector2(c.x + hw, c.y + hh),
            new Vector2(c.x + hw, c.y - hh),
            new Vector2(c.x - hw, c.y - hh)
        };
        wallCollider.edgeRadius = wallThickness * 0.5f;

        //Visual: the same rectangle drawn as a line
        wallLine.useWorldSpace = true;
        wallLine.loop = true;
        wallLine.positionCount = 4;
        wallLine.SetPosition(0, new Vector3(c.x - hw, c.y - hh, 0f));
        wallLine.SetPosition(1, new Vector3(c.x - hw, c.y + hh, 0f));
        wallLine.SetPosition(2, new Vector3(c.x + hw, c.y + hh, 0f));
        wallLine.SetPosition(3, new Vector3(c.x + hw, c.y - hh, 0f));
        wallLine.startWidth = wallThickness;
        wallLine.endWidth = wallThickness;
        wallLine.numCornerVertices = 4;
        wallLine.startColor = wallColour;
        wallLine.endColor = wallColour;
        wallLine.sortingOrder = wallSortingOrder;
        wallLine.sharedMaterial = wallMaterial != null ? wallMaterial : new Material(Shader.Find("Sprites/Default"));

        BallStartPosition = new Vector2(c.x, c.y - hh + ballStartBottomOffset);

        if (fitCameraToPhone) FitCameraToPhone();
    }

    //-----Peg placement-----//
    void PlacePegs(int wanted, Vector2 size)
    {
        positions.Clear();

        float minDist = pegRadius * 2f + minPegGap;
        float minDistSqr = minDist * minDist;
        float edge = pegRadius + minPegGap * 0.5f + wallThickness * 0.5f; //Keeps pegs off the walls
        float halfW = size.x * 0.5f - edge;
        float halfH = size.y * 0.5f - edge;
        float ballClearSqr = ballClearRadius * ballClearRadius;

        //Stops us asking for more pegs than can physically fit on the board
        float usableArea = (halfW * 2f) * (halfH * 2f);
        int capacity = Mathf.FloorToInt(usableArea * 0.45f / (Mathf.PI * minDist * minDist * 0.25f));
        wanted = Mathf.Min(wanted, capacity);

        int attempts = 0;
        int maxAttempts = wanted * 60;

        while (positions.Count < wanted && attempts < maxAttempts)
        {
            attempts++;

            Vector2 p = boardCentre + new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH));

            if ((p - BallStartPosition).sqrMagnitude < ballClearSqr) continue;

            bool free = true;
            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i] - p).sqrMagnitude < minDistSqr)
                {
                    free = false;
                    break;
                }
            }

            if (free) positions.Add(p);
        }

        if (positions.Count < wanted)
            Debug.LogWarning("LevelGenerator: only placed " + positions.Count + " of " + wanted + " pegs. Lower minPegGap or make the full board bigger.");
    }

    void PickPowerUps()
    {
        powerUpIndices.Clear();

        //1-2 power-ups, but never more than 1 per 5 pegs so small levels stay simple
        int count = Mathf.Min(Random.Range(1, maxPowerUps + 1), Mathf.Max(1, positions.Count / 5));
        count = Mathf.Min(count, positions.Count);

        while (powerUpIndices.Count < count)
        {
            powerUpIndices.Add(Random.Range(0, positions.Count));
        }
    }

    //-----Peg types-----//
    PegType[] RollTypes()
    {
        PegType[] types = new PegType[positions.Count];
        List<int> normal = new List<int>();

        for (int i = 0; i < positions.Count; i++)
        {
            if (powerUpIndices.Contains(i)) types[i] = PegType.PEG_POWERUP;
            else normal.Add(i);
        }

        Shuffle(normal);

        int required = RequiredCountFor(normal.Count);
        for (int k = 0; k < normal.Count; k++)
        {
            types[normal[k]] = k < required ? PegType.PEG_REQUIRED : PegType.PEG_BASIC;
        }

        return types;
    }

    int RequiredCountFor(int normalCount)
    {
        if (normalCount <= 0) return 0;

        float ratio = Mathf.Min(startRequiredRatio + requiredRatioGrowthPerLevel * (currentLevel - 1), maxRequiredRatio);
        int required = Mathf.RoundToInt(normalCount * ratio);

        //(count - 1) / 2 keeps the orange pegs strictly fewer than the blue ones
        int maxAllowed = Mathf.Max(1, (normalCount - 1) / 2);
        return Mathf.Clamp(required, 1, maxAllowed);
    }

    int TurnsFor(int requiredCount)
    {
        float perRequired = Mathf.Max(minTurnsPerRequired, startTurnsPerRequired - turnsPerRequiredDropPerLevel * (currentLevel - 1));
        return Mathf.CeilToInt(requiredCount * perRequired) + bonusTurns;
    }

    //-----Spawning-----//
    LevelData SpawnPegs()
    {
        ClearPegs();

        PegType[] types = RollTypes();
        int requiredCount = 0;

        for (int i = 0; i < positions.Count; i++)
        {
            PegBehaviour peg = Instantiate(pegPrefab, positions[i], Quaternion.identity, pegParent);
            peg.SetType(types[i]);
            spawnedPegs.Add(peg);

            if (types[i] == PegType.PEG_REQUIRED) requiredCount++;
        }

        return new LevelData
        {
            level = currentLevel,
            turns = TurnsFor(requiredCount),
            requiredCount = requiredCount,
            totalPegs = positions.Count
        };
    }

    void ClearPegs()
    {
        foreach (PegBehaviour peg in spawnedPegs)
        {
            if (peg == null) continue;
            peg.gameObject.SetActive(false); //Stops it colliding for the rest of this frame
            Destroy(peg.gameObject);
        }
        spawnedPegs.Clear();
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}