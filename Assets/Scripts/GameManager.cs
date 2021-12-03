using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Febucci.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    [SerializeField, Range(0f, 0.3f)] private float _displayDelay;
    [SerializeField] private Difficulty _difficulty;
    [Space]
    [SerializeField] private Transform _sudokuObject;
    [SerializeField] private Transform _debugSudokuObject;
    [SerializeField] private GameObject _numberChoicePanel;
    [SerializeField] private GameObject _genInteractionBlocker;
    [SerializeField] private GameObject _choiceInteractionBlocker;
    [SerializeField] private GameObject _gameWonPanel;

    private TextAnimator[,] _cells;
    private TextAnimator[,] _debugCells;
    private int[,] _completeValueGrid = new int[9,9];
    private int[,] _initialValueGrid = new int[9, 9];
    private int[,] _currentValueGrid = new int[9, 9];
    private IEnumerator _ongoingGeneration;
    private Vector2Int _selectedCell = new Vector2Int(-1,-1);
    private int _newChosenNumber = -1;

    private enum Difficulty
    {
        ChildsPlay = -1,
        Easy = 0,
        Medium = 1,
        Hard = 2,
        Extreme = 3
    }
    private int _difficultyValue
    {
        get
        {
            switch (_difficulty)
            {
                case Difficulty.ChildsPlay:
                    return 9;
                default:
                case Difficulty.Easy:
                    return 6;
                case Difficulty.Medium:
                    return 4;
                case Difficulty.Hard:
                    return 2;
                case Difficulty.Extreme:
                    return 1;
            }
        }
    }


    #region Setup
    private void Awake()
    {
        _genInteractionBlocker.SetActive(false);
        _choiceInteractionBlocker.SetActive(false);
        _numberChoicePanel.SetActive(false);
        GetCellReferences();
        GetDebugCellReferences();
        SetCellButtonClickFunctions();

        GenerateNewSudoku();
    }

    private void GetCellReferences()
    {
        TextAnimator[] taa = _sudokuObject.GetComponentsInChildren<TextAnimator>();
        _cells = new TextAnimator[,]
        {
            { taa[0], taa[1], taa[2], taa[3], taa[4], taa[5], taa[6], taa[7], taa[8] },
            { taa[9], taa[10], taa[11], taa[12], taa[13], taa[14], taa[15], taa[16], taa[17] },
            { taa[18], taa[19], taa[20], taa[21], taa[22], taa[23], taa[24], taa[25], taa[26] },
            { taa[27], taa[28], taa[29], taa[30], taa[31], taa[32], taa[33], taa[34], taa[35] },
            { taa[36], taa[37], taa[38], taa[39], taa[40], taa[41], taa[42], taa[43], taa[44] },
            { taa[45], taa[46], taa[47], taa[48], taa[49], taa[50], taa[51], taa[52], taa[53] },
            { taa[54], taa[55], taa[56], taa[57], taa[58], taa[59], taa[60], taa[61], taa[62] },
            { taa[63], taa[64], taa[65], taa[66], taa[67], taa[68], taa[69], taa[70], taa[71] },
            { taa[72], taa[73], taa[74], taa[75], taa[76], taa[77], taa[78], taa[79], taa[80] }
        };
    }

    private void GetDebugCellReferences()
    {
        if (_debugSudokuObject)
        {
            TextAnimator[] taa = _debugSudokuObject.GetComponentsInChildren<TextAnimator>();
            _debugCells = new TextAnimator[,]
            {
            { taa[0], taa[1], taa[2], taa[3], taa[4], taa[5], taa[6], taa[7], taa[8] },
            { taa[9], taa[10], taa[11], taa[12], taa[13], taa[14], taa[15], taa[16], taa[17] },
            { taa[18], taa[19], taa[20], taa[21], taa[22], taa[23], taa[24], taa[25], taa[26] },
            { taa[27], taa[28], taa[29], taa[30], taa[31], taa[32], taa[33], taa[34], taa[35] },
            { taa[36], taa[37], taa[38], taa[39], taa[40], taa[41], taa[42], taa[43], taa[44] },
            { taa[45], taa[46], taa[47], taa[48], taa[49], taa[50], taa[51], taa[52], taa[53] },
            { taa[54], taa[55], taa[56], taa[57], taa[58], taa[59], taa[60], taa[61], taa[62] },
            { taa[63], taa[64], taa[65], taa[66], taa[67], taa[68], taa[69], taa[70], taa[71] },
            { taa[72], taa[73], taa[74], taa[75], taa[76], taa[77], taa[78], taa[79], taa[80] }
            };
        }
    }

    private void SetCellButtonClickFunctions()
    {
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                Vector2Int xy = new Vector2Int(x, y);
                Button b = _cells[xy.x, xy.y].transform.parent.GetComponent<Button>();
                b.onClick.AddListener(() => CellClicked(new Vector2Int(xy.x, xy.y)));
            }
        }

        Button[] choiceButtons = _numberChoicePanel.transform.Find("Number Buttons").GetComponentsInChildren<Button>();
        for (int id = 0; id < 9; id++)
        {
            int index = id;
            choiceButtons[index].onClick.AddListener(() => ChooseNewNumber(index + 1));
        }

        _numberChoicePanel.transform.Find("Confirm Button").GetComponent<Button>().
            onClick.AddListener(() => ConfirmNumberChange());
    }

    [ContextMenu("Generate")]
    public void GenerateNewSudoku()
    {
        if (_ongoingGeneration != null)
            StopCoroutine(_ongoingGeneration);
        _ongoingGeneration = PopulateCellsWithValues(CalculateCellValues());
        StartCoroutine(_ongoingGeneration);
    }

    [ContextMenu("Reset")]
    public void ResetExistingSudoku()
    {
        _numberChoicePanel.SetActive(false);
        _genInteractionBlocker.SetActive(false);
        _choiceInteractionBlocker.SetActive(false);
        _gameWonPanel.SetActive(false);

        _currentValueGrid = (int[,])_initialValueGrid.Clone();

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
                _cells[x, y].GetComponentInParent<Button>().interactable = true;
        }

        if (_ongoingGeneration != null)
            StopCoroutine(_ongoingGeneration);
        _ongoingGeneration = PopulateCellsWithValues(_currentValueGrid);
        StartCoroutine(_ongoingGeneration);
    }
    #endregion

    #region Calculate values
    private int[,] CalculateCellValues()
    {
        _completeValueGrid = new int[9,9];
        // Generate default sudoku
        //for (int x = 0; x < 9; x++)
        //{
        //    for (int y = 0; y < 9; y++)
        //        _completeValueGrid[x, y] = ((x * 3 + x / 3 + y) % 9 + 1);
        //}

        // Generate real sudoku
        GenerateRealSudoku();

        _initialValueGrid = (int[,])_completeValueGrid.Clone();
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (Random.Range(1, 11) > _difficultyValue)
                    _initialValueGrid[x, y] = -1;
            }
        }

        _currentValueGrid = (int[,])_initialValueGrid.Clone();
        return _initialValueGrid;
    }

    private void GenerateRealSudoku()
    {
        // 1. Assign all cells the POTENTIAL values of 1-9

        // 2. Randomly choose first cell to set
        Vector2Int randomStartingCell = new Vector2Int(Random.Range(0, 9), Random.Range(0, 9));
        int randomStartingValue = Random.Range(0, 9);
        _completeValueGrid[randomStartingCell.x, randomStartingCell.y] = randomStartingValue;

        // 3. Update cells in same row/column/square by removing 1st cell value

        // 4. Randomly choose one of the updated cells as the next to set

        // 5. Repeat steps 2-4 until all cells filled or no options available
        // If no options are available, undo previous cell set and try again. Repeat until all filled
    }

    private bool IsAlreadyInHorizontalLine(Vector2Int cellIndex, int[,] potentialGrid, int potentialValue)
    {
        for (int x = 0; x < 9; x++)
        {
            if (x != cellIndex.x && potentialValue != -1 && potentialGrid[x, cellIndex.y] == potentialValue)
            {
                Debug.Log(potentialGrid[x, cellIndex.y] + " = " + potentialValue);
                return true;
            }
        }
        return false;
    }

    private bool IsAlreadyInVerticalLine(Vector2Int cellIndex, int[,] potentialGrid, int potentialValue)
    {
        for (int y = 0; y < 9; y++)
        {
            if (y != cellIndex.y && potentialValue != -1 && potentialGrid[cellIndex.x, y] == potentialValue)
            {
                Debug.Log(potentialGrid[cellIndex.x, y] + " = " + potentialValue);
                return true;
            }
        }
        return false;
    }

    private bool IsAlreadyInSquare(Vector2Int cellIndex, int[,] potentialGrid, int potentialValue)
    {
        return false;
    }

    private void IsExistingSudokuValid()
    {
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (IsAlreadyInHorizontalLine(new Vector2Int(x, y), _completeValueGrid, _completeValueGrid[x, y]) ||
                    IsAlreadyInVerticalLine(new Vector2Int(x, y), _completeValueGrid, _completeValueGrid[x, y]) ||
                    IsAlreadyInSquare(new Vector2Int(x, y), _completeValueGrid, _completeValueGrid[x, y]))
                {
                    Debug.LogError("Invalid sudoku!");
                    return;
                }
            }
        }
        Debug.Log("Valid sudoku!");
    }
    #endregion

    #region Display Sudoku
    private IEnumerator PopulateCellsWithValues(int[,] gridValues)
    {
        _genInteractionBlocker.SetActive(true);

        if (gridValues.Length != _cells.Length)
            Debug.LogError($"Length Mismatch: Cell Count ({_cells.Length}) != Value Count ({gridValues.Length})");
        else
        {
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 9; y++)
                    _cells[x, y].SetText("?", false);
            }

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 9; y++)
                {
                    _cells[x, y].GetComponentInParent<Button>().interactable = gridValues[x, y] == -1;
                    _cells[x, y].SetText(gridValues[x, y] != -1 ? gridValues[x,y].ToString() : "", false);
                    if (_debugSudokuObject)
                        _debugCells[x, y].SetText(_completeValueGrid[x,y].ToString(), false);
                    yield return new WaitForSecondsRealtime(_displayDelay);
                }
            }
            IsExistingSudokuValid();
        }

        _genInteractionBlocker.SetActive(false);
    }

    public void CellClicked(Vector2Int cellID)
    {
        _selectedCell = cellID;
        _choiceInteractionBlocker.SetActive(true);
        _numberChoicePanel.SetActive(true);
    }

    public void ChooseNewNumber(int number) => _newChosenNumber = number;

    public void ConfirmNumberChange()
    {
        if (_selectedCell.x > -1 && _newChosenNumber > 0)
        {
            _currentValueGrid[_selectedCell.x, _selectedCell.y] = _newChosenNumber;
            _cells[_selectedCell.x, _selectedCell.y].SetText(_newChosenNumber.ToString(), false);
            _choiceInteractionBlocker.SetActive(false);
            _numberChoicePanel.SetActive(false);
            _newChosenNumber = -1;
            _selectedCell = new Vector2Int(-1, -1);
            CheckIfHasWonGame();
        }
    }
    #endregion

    #region Grid Checks
    private void CheckIfHasWonGame()
    {
        _gameWonPanel.SetActive(
            CheckForIdenticalValidSolution() ||
            CheckForDifferentValidSolution());
    }

    private bool CheckForIdenticalValidSolution()
    {
        bool _isIdentical = true;
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                int comp = _completeValueGrid[x, y];
                int curr = _currentValueGrid[x, y];
                if (comp != curr || curr == -1)
                    _isIdentical = false;
            }
        }

        return _isIdentical;
    }

    private bool CheckForDifferentValidSolution()
    {
        return false;
    }
    #endregion

    #region Application Management
    public void QuitGame()
    {
        Application.Quit();

        #if UNITY_EDITOR
        EditorApplication.isPlaying = false;
        #endif
    }
    #endregion
}
