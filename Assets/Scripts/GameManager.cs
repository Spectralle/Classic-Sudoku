using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Febucci.UI;
using System;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    [SerializeField, Range(0f, 0.3f)] private float _displayDelay;
    [SerializeField] private Difficulty _difficulty;
    [Space]
    [SerializeField] private Transform _sudokuObject;
    [SerializeField] private bool _showDebugValues;
    [SerializeField] private GameObject _numberChoicePanel;
    [SerializeField] private GameObject _genInteractionBlocker;
    [SerializeField] private GameObject _choiceInteractionBlocker;
    [SerializeField] private GameObject _gameWonPanel;

    private Cell[,] _cells;
    private IEnumerator _ongoingGeneration;
    private Vector2Int _selectedCell = new Vector2Int(-1, -1);
    private int _newChosenNumber = -1;
    private Stack<Cell> setCellsStack = new Stack<Cell>();

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
        SetCellButtonClickFunctions();

        GenerateNewSudoku();
    }

    private void GetCellReferences()
    {
        List<Cell> cells = new List<Cell>();
        foreach (Transform cell in _sudokuObject.Find("Cells").transform)
        {
            cells.Add(new Cell(
                new Vector2Int(cells.Count % 9, (int)cells.Count / 9),
                cell.Find("Cell Number").GetComponent<TextAnimator>(),
                cell.Find("Debug Number").GetComponent<TextMeshProUGUI>(),
                cell.Find("Debug Number Possibilities").GetComponent<TextMeshProUGUI>()
            ));
        }

        _cells = new Cell[,]
        {
            { cells[0], cells[9], cells[18], cells[27], cells[36], cells[45], cells[54], cells[63], cells[72] },
            { cells[1], cells[10], cells[19], cells[28], cells[37], cells[46], cells[55], cells[64], cells[73] },
            { cells[2], cells[11], cells[20], cells[29], cells[38], cells[47], cells[56], cells[65], cells[74] },
            { cells[3], cells[12], cells[21], cells[30], cells[39], cells[48], cells[57], cells[66], cells[75] },
            { cells[4], cells[13], cells[22], cells[31], cells[40], cells[49], cells[58], cells[67], cells[76] },
            { cells[5], cells[14], cells[23], cells[32], cells[41], cells[50], cells[59], cells[68], cells[77] },
            { cells[6], cells[15], cells[24], cells[33], cells[42], cells[51], cells[60], cells[69], cells[78] },
            { cells[7], cells[16], cells[25], cells[34], cells[43], cells[52], cells[61], cells[70], cells[79] },
            { cells[8], cells[17], cells[26], cells[35], cells[44], cells[53], cells[62], cells[71], cells[80] }
        };
    }

    private void SetCellButtonClickFunctions()
    {
        foreach (Cell cell in _cells)
            cell.Button.onClick.AddListener(() => CellClicked(cell.ID));

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
        CalculateCellValues();
        //_ongoingGeneration = PopulateCellsWithValues();
        //StartCoroutine(_ongoingGeneration);
    }

    [ContextMenu("Reset"), Obsolete]
    public void ResetExistingSudoku()
    {
        _numberChoicePanel.SetActive(false);
        _genInteractionBlocker.SetActive(false);
        _choiceInteractionBlocker.SetActive(false);
        _gameWonPanel.SetActive(false);

        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                _cells[x, y].Button.interactable = true;

        if (_ongoingGeneration != null)
            StopCoroutine(_ongoingGeneration);
        _ongoingGeneration = PopulateCellsWithValues();
        StartCoroutine(_ongoingGeneration);
    }
    #endregion

    #region Create Sudoku
    private void CalculateCellValues()
    {
        GenerateRealSudoku();

        // Hide most cell values from player
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                if (Random.Range(1, 11) <= _difficultyValue)
                    _cells[x, y].ShownValue = _cells[x, y].RealValue.ToString();
    }

    private void GenerateRealSudoku()
    {
        // 1. Assign all cells the POSSIBLE VALUES of 1-9.
        // 2. Randomly choose cell to SET THE VALUE of.
        // 3. Set the value of that cell to a RANDOM VALUE WITHIN POSSIBLE VALUES (1-9).
        //      3a. If there are possible values, randomly choose one and set.
        //      3b. If there are no possible values but there are unset cells, backtrack
        //          and set previous cell to a different value and try again.
        // 4. UPDATE CELLS in same row/column/square by removing Step-2 cell's value from possibilities.
        // 5. Randomly choose one of the UPDATED cells as the next to set.
        // 6. REPEAT steps 2-4 until all cells filled or no options available.


        setCellsStack.Clear();

        foreach (Cell cell in _cells)
        {
            cell.Reset();
            cell.PossibleValues = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        }

        while (setCellsStack.Count < 81)
        {
            Cell cellToSet;

            if (setCellsStack.Count == 0)
                ChooseRandomStartingCell(out cellToSet);
            else
                ChooseRandomUnsetCellOnSameRowOrColumn(out cellToSet);

            ChooseRandomValueFromPotentialValues(cellToSet, out cellToSet, out int randomCellValue);
            SetRealCellValue(cellToSet, randomCellValue);

            // DEBUG
            cellToSet.SetUIText($"<color=orange>{cellToSet.RealValue}</color>");
            cellToSet.SetDebugUIText($"{setCellsStack.Count}");

            UpdateCellsInSameRowAndColumn(cellToSet);
            Debug.Log("cells set of 81");
        }

        // DEBUG
        foreach (Cell cell in _cells)
            cell.SetDebugPossibilitiesUIText($"{cell.PossibleValuesString}");
    }

    private void ChooseRandomStartingCell(out Cell cellToSet) => cellToSet = _cells[Random.Range(0, 9), Random.Range(0, 9)];

    private void ChooseRandomUnsetCellOnSameRowOrColumn(out Cell cellToSet)
    {
        int randX, randY;
        bool backtrackNeeded;

        if (Random.Range(0, 2) == 0)
            FindUnsetCellOnColumn(false, out randX, out randY, out backtrackNeeded);
        else
            FindUnsetCellOnRow(false, out randX, out randY, out backtrackNeeded);

        if (backtrackNeeded)
        {
            if (setCellsStack.Count > 0)
            {
                Debug.Log($"{setCellsStack.Count}: Deadend cell search on lines {randX},{randY}. Backtracking...");
                setCellsStack.Pop();
                ChooseRandomUnsetCellOnSameRowOrColumn(out cellToSet);
            }
            else
            {
                Debug.LogError("BIG ERROR: Backtracked until beginning (stack empty)!");
                cellToSet = null;
                #if UNITY_EDITOR
                EditorApplication.isPlaying = false;
                #else
                Application.Quit();
                #endif
            }
        }
        else
            cellToSet = _cells[randX, randY];
    }

    private void FindUnsetCellOnRow(bool passedThrough, out int X, out int Y, out bool backtrackNeeded)
    {
        X = setCellsStack.Peek().ID.x;
        for (Y = 0; Y < 9; Y++)
        {
            if (!setCellsStack.Contains(_cells[X, Y]))
            {
                backtrackNeeded = false;
                return;
            }
        }

        if (!passedThrough)
            FindUnsetCellOnColumn(true, out X, out Y, out backtrackNeeded);
        else
        {
            X = -1;
            Y = -1;
            backtrackNeeded = true;
        }
    }

    private void FindUnsetCellOnColumn(bool passedThrough, out int X, out int Y, out bool backtrackNeeded)
    {
        Y = setCellsStack.Peek().ID.y;
        for (X = 0; X < 9; X++)
        {
            if (!setCellsStack.Contains(_cells[X, Y]))
            {
                backtrackNeeded = false;
                return;
            }
        }

        if (!passedThrough)
            FindUnsetCellOnRow(true, out X, out Y, out backtrackNeeded);
        else
        {
            X = -1;
            Y = -1;
            backtrackNeeded = true;
        }
    }

    private void ChooseRandomValueFromPotentialValues(Cell cellToSet, out Cell cellToSetBacktracked, out int randomCellValue)
    {
        cellToSetBacktracked = cellToSet;
        randomCellValue = 0;

        if (cellToSet.PossibleValues.Count > 0)
            randomCellValue = cellToSet.PossibleValues[Random.Range(0, cellToSet.PossibleValues.Count)];
        else
        {
            // DEBUG
            //randomCellValue = 9;

            if (setCellsStack.Count > 0)
            {
                Debug.Log($"{setCellsStack.Count}: No possible values for cell {cellToSet.ID} " +
                    $"(prev: {setCellsStack.Peek().ID}). Backtracking...");
                cellToSet = setCellsStack.Pop();
                cellToSet.PossibleValues.Remove(cellToSet.RealValue);
                ChooseRandomValueFromPotentialValues(cellToSet, out cellToSetBacktracked, out randomCellValue);
            }
            else
            {
                Debug.LogError("BIG ERROR: Backtracked until beginning (stack empty)!");
                #if UNITY_EDITOR
                EditorApplication.isPlaying = false;
                #else
                Application.Quit();
                #endif
            }
        }
    }

    private void SetRealCellValue(Cell cell, int value)
    {
        cell.RealValue = value;
        setCellsStack.Push(cell);
    }

    private void UpdateCellsInSameRowAndColumn(Cell cell)
    {
        for (int x = 0; x < 9; x++)
            if (x != cell.ID.x)
                _cells[x, cell.ID.y].PossibleValues.Remove(cell.RealValue);

        for (int y = 0; y < 9; y++)
            if (y != cell.ID.y)
                _cells[cell.ID.x, y].PossibleValues.Remove(cell.RealValue);
    }
    #endregion

    #region Validity Checks
    private bool IsAlreadyInHorizontalLine(Cell cell)
    {
        for (int x = 0; x < 9; x++)
        {
            Cell cell2 = _cells[x, cell.ID.y];
            if (x != cell.ID.x && cell2.ShownValue != "" && cell.ShownValue == cell2.ShownValue)
                return true;
        }
        return false;
    }

    private bool IsAlreadyInVerticalLine(Cell cell)
    {
        for (int y = 0; y < 9; y++)
        {
            Cell cell2 = _cells[cell.ID.x, y];
            if (y != cell.ID.y && cell2.ShownValue != "" && cell.ShownValue == cell2.ShownValue)
                return true;
        }
        return false;
    }

    private bool IsAlreadyInSquare(Cell cell)
    {
        Cell[] cellBlock = {
            _cells[0 + (cell.ID.x / 3 * 3), 0 + (cell.ID.y / 3 * 3)],
            _cells[1 + (cell.ID.x / 3 * 3), 0 + (cell.ID.y / 3 * 3)],
            _cells[2 + (cell.ID.x / 3 * 3), 0 + (cell.ID.y / 3 * 3)],
            _cells[0 + (cell.ID.x / 3 * 3), 1 + (cell.ID.y / 3 * 3)],
            _cells[1 + (cell.ID.x / 3 * 3), 1 + (cell.ID.y / 3 * 3)],
            _cells[2 + (cell.ID.x / 3 * 3), 1 + (cell.ID.y / 3 * 3)],
            _cells[0 + (cell.ID.x / 3 * 3), 2 + (cell.ID.y / 3 * 3)],
            _cells[1 + (cell.ID.x / 3 * 3), 2 + (cell.ID.y / 3 * 3)],
            _cells[2 + (cell.ID.x / 3 * 3), 2 + (cell.ID.y / 3 * 3)]
        };

        foreach (Cell neighbour in cellBlock)
            if (neighbour.ID != cell.ID)
                if (neighbour.RealValue == cell.RealValue)
                    return true;

        return false;
    }

    private void IsExistingSudokuValid()
    {
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                Cell cell = _cells[x, y];
                if (IsAlreadyInHorizontalLine(cell) || IsAlreadyInVerticalLine(cell) || IsAlreadyInSquare(cell))
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
    private IEnumerator PopulateCellsWithValues()
    {
        _genInteractionBlocker.SetActive(true);
        Debug.Log("Populate");

        
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                _cells[x, y].Reset();

        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                Cell cell = _cells[x, y];
                cell.Button.interactable = !cell.Visible;
                cell.SetUIText(cell.ShownValue);
                if (_showDebugValues)
                {
                    cell.DebugText.SetText(cell.RealValue.ToString(), false);
                    cell.DebugPossibilitiesText.SetText(cell.PossibleValuesString);
                }
                yield return new WaitForSecondsRealtime(_displayDelay);
            }
        }
        IsExistingSudokuValid();

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
            _cells[_selectedCell.x, _selectedCell.y].SetUIText(_newChosenNumber.ToString());
            _choiceInteractionBlocker.SetActive(false);
            _numberChoicePanel.SetActive(false);
            _newChosenNumber = -1;
            _selectedCell = new Vector2Int(-1, -1);
            CheckIfHasWonGame();
        }
    }
    #endregion

    #region Win/Lose Validity Checks
    private void CheckIfHasWonGame()
    {
        _gameWonPanel.SetActive(
            CheckForIdenticalValidSolution() ||
            CheckForDifferentValidSolution());
    }

    private bool CheckForIdenticalValidSolution()
    {
        for (int x = 0; x < 9; x++)
        {
            for (int y = 0; y < 9; y++)
            {
                if (!_cells[x, y].Correct)
                    return false;
            }
        }

        return true;
    }

    private bool CheckForDifferentValidSolution()
    {
        // TODO
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
