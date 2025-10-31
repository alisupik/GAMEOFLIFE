using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class GameOfLife2D : MonoBehaviour
{
    [Header("Настройки игры")]
    public int width = 30;
    public int height = 30;
    public float cellSize = 1f;
    public float updateInterval = 0.2f;
    
    [Header("Визуализация сетки")]
    public bool showGrid = true;
    public Color gridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public float gridLineWidth = 0.02f;
    
    [Header("Цвета игроков")]
    public Color player1Color = Color.blue;
    public Color player2Color = Color.red;
    public Color deadColor = Color.black;
    
    [Header("Ссылки на UI")]
    public Button startButton;
    public Button stopButton;
    public Button clearButton;
    public Button randomButton;
    public Button switchPlayerButton;
    public Button toggleGridButton;
    public Button endGameButton;
    public Slider speedSlider;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI player1ScoreText;
    public TextMeshProUGUI player2ScoreText;
    public TextMeshProUGUI currentPlayerText;
    public TextMeshProUGUI gameStatusText;
    public TextMeshProUGUI gridStatusText;
    
    [Header("Ссылки")]
    public GameObject cellPrefab;
    public Camera gameCamera;
    public Material gridMaterial;
    
    private CellState[,] grid;
    private CellState[,] nextGrid;
    private GameObject[,] cellObjects;
    private List<GameObject> gridLines;
    private int[,] previousGridState;
    private int stableGenerationCount = 0;
    private const int MAX_STABLE_GENERATIONS = 3;
    
    private float timer = 0f;
    private bool isPlaying = false;
    private int currentPlayer = 1;
    private int player1Score = 0;
    private int player2Score = 0;
    private bool isGameOver = false;
    
    private Dictionary<string, Vector2Int[]> predefinedShapes;
    
    void Start()
    {
        Debug.Log("Игра 'Жизнь' запущена - инициализация...");
        
        gridLines = new List<GameObject>();
        InitializeGrid();
        CreateVisualGrid();
        CreateGridLines();
        InitializePredefinedShapes();
        CenterCamera();
        SetupUI();
        UpdateUI();
        
        Debug.Log("Инициализация игры завершена");
    }
    
    void Update()
    {
        HandleInput();
        
        if (isPlaying && !isGameOver)
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                UpdateSimulation();
                timer = 0f;
            }
        }
    }
    
    void InitializeGrid()
    {
        grid = new CellState[width, height];
        nextGrid = new CellState[width, height];
        cellObjects = new GameObject[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = CellState.Dead;
            }
        }
    }
    
    void CreateVisualGrid()
    {
        Debug.Log($"Создание визуальной сетки: {width}x{height}");
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreateCell(x, y);
            }
        }
        
        Debug.Log("Создание визуальной сетки завершено");
    }
    
    void CreateCell(int x, int y)
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Префаб клетки не назначен!");
            return;
        }
        
        GameObject cell = Instantiate(cellPrefab);
        cell.name = $"Клетка_{x}_{y}";
        cell.transform.SetParent(transform);
        cell.transform.position = new Vector3(x * cellSize, y * cellSize, 0);
        
        SpriteRenderer renderer = cell.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Debug.LogError($"Не найден SpriteRenderer на префабе клетки в ({x},{y})");
        }
        else
        {
            renderer.color = deadColor;
        }
        
        cellObjects[x, y] = cell;
    }
    
    void CreateGridLines()
    {
        ClearGridLines();
        
        if (!showGrid) return;
        
        GameObject gridParent = new GameObject("ЛинииСетки");
        gridParent.transform.SetParent(transform);
        gridParent.transform.localPosition = Vector3.zero;
        
        for (int x = 0; x <= width; x++)
        {
            CreateGridLine(
                new Vector3(x * cellSize, 0, -0.1f),
                new Vector3(x * cellSize, height * cellSize, -0.1f),
                gridParent.transform
            );
        }
        
        for (int y = 0; y <= height; y++)
        {
            CreateGridLine(
                new Vector3(0, y * cellSize, -0.1f),
                new Vector3(width * cellSize, y * cellSize, -0.1f),
                gridParent.transform
            );
        }
    }
    
    void CreateGridLine(Vector3 start, Vector3 end, Transform parent)
    {
        GameObject lineObj = new GameObject("ЛинияСетки");
        lineObj.transform.SetParent(parent);
        
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = gridLineWidth;
        line.endWidth = gridLineWidth;
        line.material = gridMaterial != null ? gridMaterial : new Material(Shader.Find("Sprites/Default"));
        line.startColor = gridColor;
        line.endColor = gridColor;
        line.useWorldSpace = false;
        
        gridLines.Add(lineObj);
    }
    
    void ClearGridLines()
    {
        foreach (var line in gridLines)
        {
            if (line != null) Destroy(line);
        }
        gridLines.Clear();
    }
    
    void UpdateSimulation()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                nextGrid[x, y] = CalculateNewState(x, y);
            }
        }
        
        player1Score = 0;
        player2Score = 0;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = nextGrid[x, y];
                UpdateCellVisual(x, y);
                
                if (grid[x, y] == CellState.Player1) player1Score++;
                if (grid[x, y] == CellState.Player2) player2Score++;
            }
        }
        
        UpdateUI();
        CheckGameEnd();
    }
    
    CellState CalculateNewState(int x, int y)
    {
        Dictionary<CellState, int> neighborCount = CountNeighborsByPlayer(x, y);
        int totalNeighbors = neighborCount[CellState.Player1] + neighborCount[CellState.Player2];
        CellState currentState = grid[x, y];
        
        if (currentState != CellState.Dead)
        {
            return (totalNeighbors == 2 || totalNeighbors == 3) ? currentState : CellState.Dead;
        }
        else
        {
            if (totalNeighbors == 3)
            {
                return neighborCount[CellState.Player1] > neighborCount[CellState.Player2] ? 
                    CellState.Player1 : CellState.Player2;
            }
            return CellState.Dead;
        }
    }
    
    Dictionary<CellState, int> CountNeighborsByPlayer(int x, int y)
    {
        var count = new Dictionary<CellState, int>
        {
            { CellState.Player1, 0 },
            { CellState.Player2, 0 }
        };
        
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                
                int neighborX = (x + i + width) % width;
                int neighborY = (y + j + height) % height;
                
                CellState neighborState = grid[neighborX, neighborY];
                if (neighborState != CellState.Dead)
                {
                    count[neighborState]++;
                }
            }
        }
        
        return count;
    }
    
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleSimulation();
        }
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearGrid();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            RandomizeGrid();
        }
        
        if (Input.GetKeyDown(KeyCode.P) && !isPlaying)
        {
            SwitchPlayer();
        }
        
        if (Input.GetKeyDown(KeyCode.G) && !isPlaying)
        {
            PlaceGlider(5, 5);
        }
        
        if (Input.GetKeyDown(KeyCode.B) && !isPlaying)
        {
            PlaceBlinker(15, 15);
        }
        
        if (Input.GetMouseButton(0) && !isPlaying && !isGameOver)
        {
            HandleCellPlacement();
        }
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            gameCamera.orthographicSize = Mathf.Clamp(
                gameCamera.orthographicSize - scroll * 2f, 5f, 50f
            );
        }
        
        if (Input.GetMouseButton(2))
        {
            float moveX = Input.GetAxis("Mouse X") * 0.5f;
            float moveY = Input.GetAxis("Mouse Y") * 0.5f;
            gameCamera.transform.Translate(-moveX, -moveY, 0);
        }
    }
    
    void HandleCellPlacement()
    {
        Vector3 mousePos = gameCamera.ScreenToWorldPoint(Input.mousePosition);
        int x = Mathf.RoundToInt(mousePos.x / cellSize);
        int y = Mathf.RoundToInt(mousePos.y / cellSize);
        
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            CellState playerState = currentPlayer == 1 ? CellState.Player1 : CellState.Player2;
            grid[x, y] = grid[x, y] == playerState ? CellState.Dead : playerState;
            UpdateCellVisual(x, y);
            UpdateScores();
        }
    }
    
    void UpdateCellVisual(int x, int y)
    {
        if (cellObjects[x, y] != null)
        {
            SpriteRenderer renderer = cellObjects[x, y].GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                Color targetColor = GetColorForState(grid[x, y]);
                renderer.color = targetColor;
            }
        }
    }
    
    Color GetColorForState(CellState state)
    {
        switch (state)
        {
            case CellState.Player1:
                return player1Color;
            case CellState.Player2:
                return player2Color;
            default:
                return deadColor;
        }
    }
    
    void InitializePredefinedShapes()
    {
        predefinedShapes = new Dictionary<string, Vector2Int[]>
        {
            { "Глайдер", new Vector2Int[] {
                new Vector2Int(1,0), new Vector2Int(2,1), new Vector2Int(0,2), 
                new Vector2Int(1,2), new Vector2Int(2,2)
            }},
            { "Мигалка", new Vector2Int[] {
                new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0)
            }}
        };
    }
    
    public void PlaceGlider(int x, int y)
    {
        PlacePredefinedShape("Глайдер", x, y);
    }
    
    public void PlaceBlinker(int x, int y)
    {
        PlacePredefinedShape("Мигалка", x, y);
    }
    
    void PlacePredefinedShape(string shapeName, int centerX, int centerY)
    {
        if (!isPlaying && !isGameOver && predefinedShapes.ContainsKey(shapeName))
        {
            foreach (Vector2Int offset in predefinedShapes[shapeName])
            {
                int x = centerX + offset.x;
                int y = centerY + offset.y;
                
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    grid[x, y] = currentPlayer == 1 ? CellState.Player1 : CellState.Player2;
                    UpdateCellVisual(x, y);
                }
            }
            UpdateScores();
        }
    }
    
    void UpdateScores()
    {
        player1Score = 0;
        player2Score = 0;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == CellState.Player1) player1Score++;
                if (grid[x, y] == CellState.Player2) player2Score++;
            }
        }
    }
    
    void SetupUI()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartSimulation);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(StopSimulation);
        
        if (clearButton != null)
            clearButton.onClick.AddListener(ClearGrid);
        
        if (randomButton != null)
            randomButton.onClick.AddListener(RandomizeGrid);
        
        if (switchPlayerButton != null)
            switchPlayerButton.onClick.AddListener(SwitchPlayer);
        
        if (toggleGridButton != null)
            toggleGridButton.onClick.AddListener(ToggleGrid);
        
        if (endGameButton != null)
            endGameButton.onClick.AddListener(ForceEndGame);
        
        if (speedSlider != null)
        {
            speedSlider.minValue = 1f;
            speedSlider.maxValue = 20f;
            speedSlider.value = 1f / updateInterval;
            speedSlider.onValueChanged.AddListener(SetSimulationSpeed);
        }
        
        UpdateUI();
    }
    
    void UpdateUI()
    {
        if (player1ScoreText != null)
            player1ScoreText.text = $"Игрок 1: {player1Score}";
        
        if (player2ScoreText != null)
            player2ScoreText.text = $"Игрок 2: {player2Score}";
        
        if (currentPlayerText != null)
        {
            currentPlayerText.text = $"Текущий: Игрок {currentPlayer}";
            currentPlayerText.color = currentPlayer == 1 ? player1Color : player2Color;
        }
        
        if (gameStatusText != null)
        {
            if (isGameOver)
            {
                // Текст уже установлен в CheckGameEnd или ForceEndGame
            }
            else
            {
                gameStatusText.text = isPlaying ? "Статус: Играется" : "Статус: Пауза";
                gameStatusText.color = isPlaying ? Color.green : Color.yellow;
                gameStatusText.fontSize = 14;
            }
        }
        
        if (speedText != null && speedSlider != null)
            speedText.text = $"Скорость: {speedSlider.value:F1}x";
        
        if (gridStatusText != null)
            gridStatusText.text = showGrid ? "Сетка: ВКЛ" : "Сетка: ВЫКЛ";
        
        if (startButton != null)
            startButton.interactable = !isPlaying && !isGameOver;
        
        if (stopButton != null)
            stopButton.interactable = isPlaying && !isGameOver;
        
        if (switchPlayerButton != null)
            switchPlayerButton.interactable = !isPlaying && !isGameOver;
        
        if (endGameButton != null)
            endGameButton.interactable = !isGameOver;
    }
    
    void CheckGameEnd()
    {
        if (isGameOver) return;
        
        bool player1HasCells = player1Score > 0;
        bool player2HasCells = player2Score > 0;
        
        // Если у одного из игроков не осталось клеток
        if (!player1HasCells || !player2HasCells)
        {
            isGameOver = true;
            isPlaying = false;
            
            string winnerMessage = "";
            if (!player1HasCells && !player2HasCells)
            {
                winnerMessage = "НИЧЬЯ! У обоих игроков не осталось клеток!";
            }
            else if (!player1HasCells)
            {
                winnerMessage = "ПОБЕДИЛ ИГРОК 2! У игрока 1 не осталось клеток!";
            }
            else if (!player2HasCells)
            {
                winnerMessage = "ПОБЕДИЛ ИГРОК 1! У игрока 2 не осталось клеток!";
            }
            
            Debug.Log($"ИГРА ОКОНЧЕНА: {winnerMessage}");
            
            if (gameStatusText != null)
            {
                gameStatusText.text = $"ИГРА ОКОНЧЕНА\n{winnerMessage}";
                gameStatusText.color = Color.red;
                gameStatusText.fontSize = 20;
            }
            
            UpdateUI();
            return;
        }
        
        // Проверка стабильности поля
        if (IsGridStable())
        {
            isGameOver = true;
            isPlaying = false;
            
            string winnerMessage = "";
            if (player1Score > player2Score)
            {
                winnerMessage = $"ПОБЕДИЛ ИГРОК 1! {player1Score} vs {player2Score}";
            }
            else if (player2Score > player1Score)
            {
                winnerMessage = $"ПОБЕДИЛ ИГРОК 2! {player2Score} vs {player1Score}";
            }
            else
            {
                winnerMessage = $"НИЧЬЯ! {player1Score} vs {player2Score}";
            }
            
            Debug.Log($"ИГРА ОКОНЧЕНА: Поле стабилизировалось. {winnerMessage}");
            
            if (gameStatusText != null)
            {
                gameStatusText.text = $"ИГРА ОКОНЧЕНА\nПоле стабилизировалось\n{winnerMessage}";
                gameStatusText.color = Color.red;
                gameStatusText.fontSize = 18;
            }
            
            UpdateUI();
        }
    }
    
    bool IsGridStable()
    {
        if (previousGridState == null)
        {
            previousGridState = new int[width, height];
            SaveGridState();
            return false;
        }
        
        bool isSame = true;
        for (int x = 0; x < width && isSame; x++)
        {
            for (int y = 0; y < height && isSame; y++)
            {
                int currentState = (int)grid[x, y];
                if (currentState != previousGridState[x, y])
                {
                    isSame = false;
                }
            }
        }
        
        if (isSame)
        {
            stableGenerationCount++;
            Debug.Log($"Сетка стабильна уже {stableGenerationCount} поколений");
        }
        else
        {
            stableGenerationCount = 0;
            SaveGridState();
        }
        
        return stableGenerationCount >= MAX_STABLE_GENERATIONS;
    }
    
    void SaveGridState()
    {
        if (previousGridState == null)
            previousGridState = new int[width, height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                previousGridState[x, y] = (int)grid[x, y];
            }
        }
    }
    
    public void StartSimulation()
    {
        if (!isGameOver)
        {
            isPlaying = true;
            UpdateUI();
        }
    }
    
    public void StopSimulation()
    {
        isPlaying = false;
        UpdateUI();
    }
    
    public void ToggleSimulation()
    {
        if (!isGameOver)
        {
            isPlaying = !isPlaying;
            UpdateUI();
        }
    }
    
    public void ClearGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = CellState.Dead;
                UpdateCellVisual(x, y);
            }
        }
        player1Score = 0;
        player2Score = 0;
        isGameOver = false;
        stableGenerationCount = 0;
        previousGridState = null;
        
        if (gameStatusText != null)
        {
            gameStatusText.text = "Статус: Пауза";
            gameStatusText.color = Color.yellow;
            gameStatusText.fontSize = 14;
        }
        
        UpdateUI();
    }
    
    public void RandomizeGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float rand = Random.value;
                if (rand < 0.1f)
                    grid[x, y] = CellState.Player1;
                else if (rand < 0.2f)
                    grid[x, y] = CellState.Player2;
                else
                    grid[x, y] = CellState.Dead;
                
                UpdateCellVisual(x, y);
            }
        }
        isGameOver = false;
        stableGenerationCount = 0;
        previousGridState = null;
        UpdateScores();
        
        if (gameStatusText != null)
        {
            gameStatusText.text = "Статус: Пауза";
            gameStatusText.color = Color.yellow;
            gameStatusText.fontSize = 14;
        }
        
        UpdateUI();
    }
    
    public void SwitchPlayer()
    {
        if (!isPlaying && !isGameOver)
        {
            currentPlayer = currentPlayer == 1 ? 2 : 1;
            UpdateUI();
        }
    }
    
    public void ToggleGrid()
    {
        if (!isPlaying)
        {
            showGrid = !showGrid;
            CreateGridLines();
            UpdateUI();
        }
    }
    
    public void ForceEndGame()
    {
        if (!isGameOver)
        {
            isGameOver = true;
            isPlaying = false;
            
            string winnerMessage = "";
            if (player1Score > player2Score)
            {
                winnerMessage = $"ПОБЕДИЛ ИГРОК 1! {player1Score} vs {player2Score}";
            }
            else if (player2Score > player1Score)
            {
                winnerMessage = $"ПОБЕДИЛ ИГРОК 2! {player2Score} vs {player1Score}";
            }
            else
            {
                winnerMessage = $"НИЧЬЯ! {player1Score} vs {player2Score}";
            }
            
            if (gameStatusText != null)
            {
                gameStatusText.text = $"ИГРА ОКОНЧЕНА\n{winnerMessage}";
                gameStatusText.color = Color.red;
                gameStatusText.fontSize = 20;
            }
            
            UpdateUI();
        }
    }
    
    public void SetSimulationSpeed(float speed)
    {
        updateInterval = 1f / speed;
        UpdateUI();
    }
    
    void CenterCamera()
    {
        if (gameCamera != null)
        {
            gameCamera.transform.position = new Vector3(
                width * cellSize * 0.5f, 
                height * cellSize * 0.5f, 
                -10f
            );
            gameCamera.orthographicSize = Mathf.Max(width, height) * 0.6f;
        }
    }
}
