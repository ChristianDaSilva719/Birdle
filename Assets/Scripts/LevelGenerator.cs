using System.Collections.Generic;
using UnityEngine;

public struct LevelData
{
    public int level;
    public int turns;
    public int requiredCount;
    public int totalPegs;
    public int holeCount;
    public string shapeName;
}

public class LevelGenerator : MonoBehaviour
{
    //A board outline. Points are in a -1 to 1 box and get scaled to the board size
    class ShapeDef
    {
        public string name;
        public int tier;
        public Vector2[] points;
    }

    [Header("References")]
    [SerializeField] PegBehaviour pegPrefab;
    [SerializeField] Transform pegParent; //Optional, must be an object in the scene (not from the Project window)
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

    [Header("Board Shapes")]
    [SerializeField] int levelsPerShapeTier = 2;        //Levels before the next group of shapes unlocks
    [SerializeField] bool randomFlip = true;            //Randomly mirrors shapes so they don't always look the same
    [SerializeField] float extraTurnsPerShapeTier = 0f; //Turns added per shape tier (0 = shape doesn't affect turns, negative = fewer turns)
    [SerializeField] float ballStartEdgeClearance = 0.6f; //How far from the walls the ball must start

    [Header("Holes")]
    [SerializeField] int holesStartLevel = 3;           //First level that has holes
    [SerializeField] int levelsPerExtraHole = 3;        //The most holes a level can have goes up by one every this many levels
    [SerializeField] int minHoles = 1;                  //Fewest holes on a level that has holes (0 allows some levels with none)
    [SerializeField] int maxHoles = 4;                  //Most holes any level can ever have
    [SerializeField] float biggestHoleRadius = 0.8f;    //Hole radius when a level has just 1 hole
    [SerializeField] float smallestHoleRadius = 0.4f;   //Hole radius when a level has the maximum number of holes
    [SerializeField] float biggestHoleCaptureFraction = 0.75f;  //Share of the radius where the ball falls in, when a level has 1 hole (big holes catch more)
    [SerializeField] float smallestHoleCaptureFraction = 0.5f;  //Same, when a level has the maximum number of holes (small holes are stricter)
    [SerializeField] float holeAreaShare = 0.08f;       //Holes never take more than this share of the board
    [SerializeField] float extraTurnsPerHole = 0.5f;    //Turns given back per hole (rounded up), so holes don't make levels unfair
    [SerializeField] Color holeColour = new Color(0.04f, 0.06f, 0.12f, 1f);
    [SerializeField] Color holeRimColour = new Color(0.16f, 0.22f, 0.38f, 1f);
    [SerializeField] int holeSortingOrder = -5;         //Below the ball and pegs

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
    [SerializeField] float ballStartBottomOffset = 1f; //How far above the lowest point of the board the ball looks for a start spot
    [SerializeField] float ballClearRadius = 1.5f;     //No pegs or holes spawn this close to the ball start

    public Vector2 BallStartPosition { get; private set; }
    public string CurrentShapeName { get; private set; }

    private readonly List<Vector2> positions = new List<Vector2>();
    private readonly List<Vector2> holePositions = new List<Vector2>();
    private readonly HashSet<int> powerUpIndices = new HashSet<int>();
    private readonly List<PegBehaviour> spawnedPegs = new List<PegBehaviour>();
    private readonly List<GameObject> spawnedHoles = new List<GameObject>();
    private readonly List<Vector2> boardPolygon = new List<Vector2>(); //The walls of the current level, in world space
    private List<ShapeDef> shapes;
    private ShapeDef currentShape;
    private ShapeDef lastShape;
    private int highestTier;
    private EdgeCollider2D wallCollider;
    private LineRenderer wallLine;
    private Transform runtimePegParent;
    private Transform runtimeHoleParent;
    private Sprite holeSprite;
    private float currentHoleRadius;          //All holes on a level share one size, which depends on how many there are
    private float currentHoleCaptureFraction; //...and one capture share, which grows with the size
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

    //Where spawned pegs get parented: the assigned Peg Parent if it's a scene object, otherwise an auto-made "Pegs" container
    Transform GetPegParent()
    {
        if (pegParent != null)
        {
            //Assets from the Project window (prefabs) don't belong to a scene, and Unity won't parent clones to them
            if (pegParent.gameObject.scene.IsValid())
                return pegParent;

            Debug.LogWarning("LevelGenerator: Peg Parent '" + pegParent.name + "' is an asset from the Project window, not an object in the scene, so it can't be used. " +
                             "Using an automatic 'Pegs' object instead. Clear the slot or drag in an object from the Hierarchy.");
            pegParent = null; //Only warn once
        }

        if (runtimePegParent == null)
            runtimePegParent = new GameObject("Pegs").transform;

        return runtimePegParent;
    }

    //-----Public-----//

    //Brand new layout: new shape, new board size, new holes, new peg positions, new power-up positions
    public LevelData GenerateNewLevel(int level)
    {
        currentLevel = level;

        if (!PrefabIsSafe())
            return new LevelData { level = level, turns = 0, requiredCount = 0, totalPegs = 0, holeCount = 0, shapeName = "" };

        if (clearLeftoverPegs) RemoveLeftoverPegs();

        Vector2 size = BoardSizeFor(level);
        currentShape = PickShape(level);
        BuildBoard(size);

        //Holes go down first so the pegs can be placed around them
        PlaceHoles();
        SpawnHoles();

        int wanted = Mathf.Min(firstLevelPegs + pegsPerLevelIncrease * (level - 1), maxPegCount);
        PlacePegs(wanted);
        PickPowerUps();

        LevelData data = SpawnPegs();
        Debug.Log("LevelGenerator: level " + level + " shape '" + currentShape.name + "' (tier " + currentShape.tier + "), asked for " + wanted +
                  " pegs, placed " + data.totalPegs + ", holes " + data.holeCount +
                  " (radius " + currentHoleRadius.ToString("F2") + ", falls in within " + (currentHoleRadius * currentHoleCaptureFraction).ToString("F2") + ")" +
                  ", board size " + size + ", turns " + data.turns);
        return data;
    }

    //Same shape, same holes, same positions and same power-ups, orange pegs are re-rolled
    public LevelData RespawnSameLevel()
    {
        if (!PrefabIsSafe())
            return new LevelData { level = currentLevel, turns = 0, requiredCount = 0, totalPegs = 0, holeCount = 0, shapeName = "" };

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

    //-----Shapes-----//
    void BuildShapes()
    {
        shapes = new List<ShapeDef>();
        highestTier = 0;

        //Tier 0 - the starter board
        AddShape("Rectangle", 0,
            -1, -1, -1, 1, 1, 1, 1, -1);

        //Tier 1 - gentle changes
        float c = 0.35f;
        AddShape("Chamfered", 1,
            -1 + c, -1, -1, -1 + c, -1, 1 - c, -1 + c, 1,
            1 - c, 1, 1, 1 - c, 1, -1 + c, 1 - c, -1);

        AddShape("Hexagon", 1,
            -0.6f, -1, -1, 0, -0.6f, 1, 0.6f, 1, 1, 0, 0.6f, -1);

        AddShape("Funnel", 1,
            -0.55f, -1, -1, 1, 1, 1, 0.55f, -1);

        //Tier 2 - angles and steps
        AddShape("Diamond", 2,
            0, -1, -1, 0, 0, 1, 1, 0);

        AddShape("L-Shape", 2,
            -1, -1, -1, 1, 0.1f, 1, 0.1f, -0.1f, 1, -0.1f, 1, -1);

        AddShape("Arrow", 2,
            0, 1, 1, 0.1f, 0.45f, 0.1f, 0.45f, -1, -0.45f, -1, -0.45f, 0.1f, -1, 0.1f);

        AddShape("Staircase", 2,
            -1, -1, -1, 1, -0.3f, 1, -0.3f, 0.35f, 0.35f, 0.35f, 0.35f, -0.3f, 1, -0.3f, 1, -1);

        //Tier 3 - narrow gaps and spikes
        AddShape("Hourglass", 3,
            -1, -1, -1, -0.5f, -0.4f, 0, -1, 0.5f, -1, 1,
            1, 1, 1, 0.5f, 0.4f, 0, 1, -0.5f, 1, -1);

        AddShape("T-Shape", 3,
            -1, 1, 1, 1, 1, 0.3f, 0.4f, 0.3f, 0.4f, -1, -0.4f, -1, -0.4f, 0.3f, -1, 0.3f);

        AddShape("Crown", 3,
            -1, -1, -1, 1, -0.5f, 0.2f, 0, 1, 0.5f, 0.2f, 1, 1, 1, -1);

        //Tier 4 - the extravagant ones
        float a = 0.45f; //Arm thickness
        AddShape("Plus", 4,
            -a, -1, -a, -a, -1, -a, -1, a, -a, a, -a, 1,
            a, 1, a, a, 1, a, 1, -a, a, -a, a, -1);

        AddShape("H-Shape", 4,
            -1, -1, -1, 1, -0.4f, 1, -0.4f, 0.25f, 0.4f, 0.25f, 0.4f, 1,
            1, 1, 1, -1, 0.4f, -1, 0.4f, -0.25f, -0.4f, -0.25f, -0.4f, -1);
    }

    void AddShape(string shapeName, int tier, params float[] xy)
    {
        Vector2[] pts = new Vector2[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++)
            pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);

        AddShapePoints(shapeName, tier, pts);
    }

    void AddShapePoints(string shapeName, int tier, Vector2[] pts)
    {
        //Stretch every shape to fill the -1 to 1 box, so they all fit the board size the same way
        Vector2 min = pts[0];
        Vector2 max = pts[0];
        foreach (Vector2 p in pts)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Vector2[] fitted = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            fitted[i] = new Vector2(
                (pts[i].x - min.x) / (max.x - min.x) * 2f - 1f,
                (pts[i].y - min.y) / (max.y - min.y) * 2f - 1f);
        }

        shapes.Add(new ShapeDef { name = shapeName, tier = tier, points = fitted });
        highestTier = Mathf.Max(highestTier, tier);
    }

    //Shapes unlock tier by tier as the levels go up, harder shapes are picked more often, and never the same shape twice in a row
    ShapeDef PickShape(int level)
    {
        if (shapes == null) BuildShapes();

        int unlockedTier = Mathf.Min(highestTier, (level - 1) / Mathf.Max(1, levelsPerShapeTier));

        List<ShapeDef> pool = new List<ShapeDef>();
        foreach (ShapeDef s in shapes)
        {
            if (s.tier <= unlockedTier && s != lastShape) pool.Add(s);
        }

        if (pool.Count == 0) //Only one shape unlocked (level 1), so repeating it is fine
        {
            foreach (ShapeDef s in shapes)
            {
                if (s.tier <= unlockedTier) pool.Add(s);
            }
        }

        float total = 0f;
        foreach (ShapeDef s in pool) total += 1f + s.tier;

        float roll = Random.value * total;
        ShapeDef picked = pool[pool.Count - 1];
        foreach (ShapeDef s in pool)
        {
            roll -= 1f + s.tier;
            if (roll <= 0f)
            {
                picked = s;
                break;
            }
        }

        lastShape = picked;
        return picked;
    }

    //-----Polygon maths-----//
    static bool PointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[j];

            if (((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    static float DistanceToEdge(Vector2 p, List<Vector2> poly)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[j];
            Vector2 ab = poly[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = (p - (a + ab * t)).magnitude;
            if (d < best) best = d;
        }
        return best;
    }

    static float PolygonArea(List<Vector2> poly)
    {
        float sum = 0f;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            sum += poly[j].x * poly[i].y - poly[i].x * poly[j].y;
        return Mathf.Abs(sum) * 0.5f;
    }

    static float PolygonPerimeter(List<Vector2> poly)
    {
        float sum = 0f;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            sum += Vector2.Distance(poly[i], poly[j]);
        return sum;
    }

    void GetPolygonBounds(out Vector2 min, out Vector2 max)
    {
        min = boardPolygon[0];
        max = boardPolygon[0];
        foreach (Vector2 p in boardPolygon)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
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

        //Scale the shape's -1 to 1 points up to the board size, with an optional random mirror
        bool flipX = randomFlip && Random.value < 0.5f;
        bool flipY = randomFlip && Random.value < 0.5f;

        boardPolygon.Clear();
        foreach (Vector2 u in currentShape.points)
        {
            float x = (flipX ? -u.x : u.x) * hw;
            float y = (flipY ? -u.y : u.y) * hh;
            boardPolygon.Add(new Vector2(c.x + x, c.y + y));
        }

        CurrentShapeName = currentShape.name;

        if (wallCollider == null)
        {
            GameObject walls = new GameObject("BoardWalls");
            wallCollider = walls.AddComponent<EdgeCollider2D>();
            wallLine = walls.AddComponent<LineRenderer>();
        }

        //Collision: closed loop, edgeRadius gives the walls the same thickness as the drawn line
        Vector2[] colliderPoints = new Vector2[boardPolygon.Count + 1];
        for (int i = 0; i < boardPolygon.Count; i++) colliderPoints[i] = boardPolygon[i];
        colliderPoints[boardPolygon.Count] = boardPolygon[0];
        wallCollider.points = colliderPoints;
        wallCollider.edgeRadius = wallThickness * 0.5f;

        //Visual: the same outline drawn as a line
        wallLine.useWorldSpace = true;
        wallLine.loop = true;
        wallLine.positionCount = boardPolygon.Count;
        for (int i = 0; i < boardPolygon.Count; i++)
            wallLine.SetPosition(i, new Vector3(boardPolygon[i].x, boardPolygon[i].y, 0f));
        wallLine.startWidth = wallThickness;
        wallLine.endWidth = wallThickness;
        wallLine.numCornerVertices = 4;
        wallLine.startColor = wallColour;
        wallLine.endColor = wallColour;
        wallLine.sortingOrder = wallSortingOrder;
        wallLine.sharedMaterial = wallMaterial != null ? wallMaterial : new Material(Shader.Find("Sprites/Default"));

        BallStartPosition = FindBallStart(new Vector2(c.x - hw, c.y - hh), new Vector2(c.x + hw, c.y + hh));

        if (fitCameraToPhone) FitCameraToPhone();
    }

    //Lowest spot near the middle that is inside the shape and clear of the walls (shapes like the H have no floor at the exact centre)
    Vector2 FindBallStart(Vector2 min, Vector2 max)
    {
        float step = 0.2f;
        float centreX = (min.x + max.x) * 0.5f;
        float reachX = (max.x - min.x) * 0.5f;

        for (float y = min.y + ballStartBottomOffset; y <= max.y; y += step)
        {
            for (float dx = 0f; dx <= reachX; dx += step)
            {
                Vector2 right = new Vector2(centreX + dx, y);
                if (IsClearSpot(right)) return right;

                if (dx > 0f)
                {
                    Vector2 left = new Vector2(centreX - dx, y);
                    if (IsClearSpot(left)) return left;
                }
            }
        }

        Debug.LogWarning("LevelGenerator: couldn't find a clear start spot inside the '" + currentShape.name + "' shape, using the board centre.");
        return new Vector2(centreX, (min.y + max.y) * 0.5f);
    }

    bool IsClearSpot(Vector2 p)
    {
        return PointInPolygon(p, boardPolygon) && DistanceToEdge(p, boardPolygon) >= ballStartEdgeClearance;
    }

    //-----Holes-----//

    //0 = a level with 1 hole, 1 = a level with the maximum number of holes. The size and the capture share both follow this
    float HoleCountT(int count)
    {
        return maxHoles <= 1 ? 0f : Mathf.Clamp01((count - 1) / (float)(maxHoles - 1));
    }

    //Fewer holes = bigger holes, more holes = smaller holes
    float HoleRadiusFor(int count)
    {
        return Mathf.Lerp(biggestHoleRadius, smallestHoleRadius, HoleCountT(count));
    }

    //Bigger holes also catch the ball over more of their surface, smaller holes are stricter
    float HoleCaptureFractionFor(int count)
    {
        float fraction = Mathf.Lerp(biggestHoleCaptureFraction, smallestHoleCaptureFraction, HoleCountT(count));
        return Mathf.Clamp01(fraction);
    }

    void PlaceHoles()
    {
        holePositions.Clear();
        currentHoleRadius = 0f;
        currentHoleCaptureFraction = 0f;

        if (currentLevel < holesStartLevel) return;

        //The most holes this level can have goes up as the levels go up, the actual number is random up to that
        int upper = Mathf.Min(maxHoles, 1 + (currentLevel - holesStartLevel) / Mathf.Max(1, levelsPerExtraHole));
        int lower = Mathf.Clamp(minHoles, 0, upper);

        //Only counts whose holes fit under the area cap on this board are allowed, so a small board never gets crowded
        float boardArea = PolygonArea(boardPolygon);
        List<int> options = new List<int>();
        for (int n = lower; n <= upper; n++)
        {
            if (n == 0)
            {
                options.Add(0);
                continue;
            }

            float r = HoleRadiusFor(n);
            if (n * Mathf.PI * r * r <= boardArea * holeAreaShare) options.Add(n);
        }

        if (options.Count == 0) return;

        int wanted = options[Random.Range(0, options.Count)];
        if (wanted <= 0) return;

        currentHoleRadius = HoleRadiusFor(wanted);
        currentHoleCaptureFraction = HoleCaptureFractionFor(wanted);

        //These distances always leave a gap wider than the ball around a hole, so a hole can never seal off part of the board
        float wallDist = currentHoleRadius + minPegGap + wallThickness * 0.5f; //Hole centre to wall
        float holeGap = currentHoleRadius * 2f + minPegGap;                    //Hole centre to another hole centre
        float holeGapSqr = holeGap * holeGap;
        float ballDist = ballClearRadius + currentHoleRadius;                  //Hole centre to the ball's start
        float ballDistSqr = ballDist * ballDist;

        GetPolygonBounds(out Vector2 min, out Vector2 max);

        int attempts = 0;
        int maxAttempts = Mathf.Max(200, wanted * 200);

        while (holePositions.Count < wanted && attempts < maxAttempts)
        {
            attempts++;

            Vector2 p = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));

            if (!PointInPolygon(p, boardPolygon)) continue;
            if (DistanceToEdge(p, boardPolygon) < wallDist) continue;
            if ((p - BallStartPosition).sqrMagnitude < ballDistSqr) continue;

            bool free = true;
            for (int i = 0; i < holePositions.Count; i++)
            {
                if ((holePositions[i] - p).sqrMagnitude < holeGapSqr)
                {
                    free = false;
                    break;
                }
            }

            if (free) holePositions.Add(p);
        }

        if (holePositions.Count < wanted)
            Debug.Log("LevelGenerator: only room for " + holePositions.Count + " of " + wanted + " holes in the '" + currentShape.name + "' shape.");
    }

    void SpawnHoles()
    {
        ClearHoles();
        if (holePositions.Count == 0) return;

        if (holeSprite == null) holeSprite = MakeHoleSprite();
        if (runtimeHoleParent == null) runtimeHoleParent = new GameObject("Holes").transform;

        foreach (Vector2 pos in holePositions)
        {
            GameObject go = new GameObject("Hole");
            go.transform.SetParent(runtimeHoleParent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (currentHoleRadius * 2f); //The sprite is 1 unit wide, so this sets the hole's diameter

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = holeSprite;
            sr.sortingOrder = holeSortingOrder;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f; //Half of the 1 unit sprite, the scale above turns this into the hole's real radius

            HoleBehaviour hole = go.AddComponent<HoleBehaviour>();
            hole.captureRadius = currentHoleRadius * currentHoleCaptureFraction; //In world units
            spawnedHoles.Add(go);
        }
    }

    void ClearHoles()
    {
        foreach (GameObject hole in spawnedHoles)
        {
            if (hole == null) continue;
            hole.SetActive(false);
            Destroy(hole);
        }
        spawnedHoles.Clear();
    }

    //A dark circle with a lighter rim, drawn in code so there's no sprite to set up. It is 1 world unit wide and gets scaled per hole
    Sprite MakeHoleSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                Color col = Color.Lerp(holeColour, holeRimColour, Mathf.Pow(Mathf.Clamp01(d), 3f));
                col.a = Mathf.Clamp01((1f - d) / 0.06f); //Soft edge on the outer 6%
                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    //-----Peg placement-----//
    void PlacePegs(int wanted)
    {
        positions.Clear();

        float minDist = pegRadius * 2f + minPegGap;
        float minDistSqr = minDist * minDist;
        float edge = pegRadius + minPegGap * 0.5f + wallThickness * 0.5f; //Keeps pegs off the walls
        float ballClearSqr = ballClearRadius * ballClearRadius;
        float holePegDist = currentHoleRadius + pegRadius + minPegGap;    //Keeps pegs off the holes
        float holePegDistSqr = holePegDist * holePegDist;

        GetPolygonBounds(out Vector2 min, out Vector2 max);

        //Stops us asking for more pegs than can physically fit in the shape (holes take up room too)
        float usableArea = PolygonArea(boardPolygon) - PolygonPerimeter(boardPolygon) * edge
                           - holePositions.Count * Mathf.PI * holePegDist * holePegDist;
        usableArea = Mathf.Max(0f, usableArea);
        int capacity = Mathf.FloorToInt(usableArea * 0.45f / (Mathf.PI * minDist * minDist * 0.25f));
        wanted = Mathf.Min(wanted, capacity);

        int attempts = 0;
        int maxAttempts = Mathf.Max(200, wanted * 200);

        while (positions.Count < wanted && attempts < maxAttempts)
        {
            attempts++;

            Vector2 p = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));

            if (!PointInPolygon(p, boardPolygon)) continue;
            if (DistanceToEdge(p, boardPolygon) < edge) continue;
            if ((p - BallStartPosition).sqrMagnitude < ballClearSqr) continue;

            bool free = true;

            for (int i = 0; i < holePositions.Count; i++)
            {
                if ((holePositions[i] - p).sqrMagnitude < holePegDistSqr)
                {
                    free = false;
                    break;
                }
            }
            if (!free) continue;

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
            Debug.LogWarning("LevelGenerator: only placed " + positions.Count + " of " + wanted + " pegs in the '" + currentShape.name + "' shape. Lower minPegGap or make the full board bigger.");
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
        int turns = Mathf.CeilToInt(requiredCount * perRequired) + bonusTurns;

        //Optional: harder shapes give more (or with a negative value, fewer) turns
        if (currentShape != null)
            turns += Mathf.RoundToInt(currentShape.tier * extraTurnsPerShapeTier);

        //Holes can cost the player a turn, so every hole gives some turns back
        turns += Mathf.CeilToInt(holePositions.Count * extraTurnsPerHole);

        return Mathf.Max(1, turns);
    }

    //-----Spawning-----//
    LevelData SpawnPegs()
    {
        ClearPegs();

        PegType[] types = RollTypes();
        int requiredCount = 0;
        Transform parent = GetPegParent();

        for (int i = 0; i < positions.Count; i++)
        {
            PegBehaviour peg = Instantiate(pegPrefab, positions[i], Quaternion.identity, parent);
            peg.SetType(types[i]);
            spawnedPegs.Add(peg);

            if (types[i] == PegType.PEG_REQUIRED) requiredCount++;
        }

        return new LevelData
        {
            level = currentLevel,
            turns = TurnsFor(requiredCount),
            requiredCount = requiredCount,
            totalPegs = positions.Count,
            holeCount = holePositions.Count,
            shapeName = CurrentShapeName
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