using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class MazeSpawner : MonoBehaviour
{
    private const int SeedValue = 70;
    private const int CoinCount = 1;

    public GameObject Floor;
    public GameObject Wall;
    public GameObject Pillar;
    public GameObject GoalPrefab;

    public bool ReadMazeDataFromFile;
    public int Rows;
    public int Cols;
    public int BallEnergy;

    private void InitializeMazeVariables()
    {
        MazeDescription.Rows = Rows;
        MazeDescription.Cols = Cols;
        MazeDescription.Coins = CoinCount;
        MazeDescription.BallEnergy = BallEnergy;

        MazeDescription.PillarPrefab = Pillar;
        MazeDescription.WallPrefab = Wall;
        MazeDescription.CoinPrefab = GoalPrefab;
    }

    private enum WallType { Left, Right, Back, Front };

    private readonly Dictionary<int, Dictionary<WallType, bool>> restrictedWalls = new Dictionary<int, Dictionary<WallType, bool>>();

    private bool RestrictWall(int column, int row, WallType wallType)
    {
        if (row == 0 && wallType == WallType.Back || row == Rows - 1 && wallType == WallType.Front || column == 0 && wallType == WallType.Left || column == Cols - 1 && wallType == WallType.Right)
            return false;
        int vertex = row + column * Rows;

        int otherRow = row;
        int otherColumn = column;
        WallType otherWallType = wallType;
        switch (wallType) {
            case WallType.Back: --otherRow; otherWallType = WallType.Front; break;
            case WallType.Front: ++otherRow; otherWallType = WallType.Back; break;
            case WallType.Left: --otherColumn; otherWallType = WallType.Right; break;
            case WallType.Right: ++otherColumn; otherWallType = WallType.Left; break;
            default: throw new NotImplementedException();
        }
        int otherVertex = otherRow + otherColumn * Rows;
        Debug.Assert(otherWallType != wallType);
        Debug.Assert(otherVertex != vertex);
        bool restrict = restrictedWalls.ContainsKey(otherVertex) && restrictedWalls[otherVertex].ContainsKey(otherWallType)
            ? restrictedWalls[otherVertex][otherWallType]
            : Random.Range(0, 16) == 0;
        if (!restrictedWalls.ContainsKey(vertex))
            restrictedWalls[vertex] = new Dictionary<WallType, bool>();
        restrictedWalls[vertex][wallType] = restrict;
        return restrict;
    }

    private void Awake()
    {
        InitializeMazeVariables();
        Random.InitState(SeedValue);

        var mazeDescriptionCells = new List<MazeDescriptionCell>();
        var cellsWithCoinIndexes = new List<int>();
        var emptyCellsIndexes = new List<int>();
        var mazeGenerator = new TreeMazeGenerator(MazeDescription.Rows, MazeDescription.Cols);
        mazeGenerator.GenerateMaze();
        for (var row = 0; row < MazeDescription.Rows; ++row) {
            for (var column = 0; column < MazeDescription.Cols; ++column) {
                var x = column * MazeDescription.CellWidth;
                var z = row * MazeDescription.CellHeight;
                var cell = mazeGenerator.GetMazeCell(row, column);

                var floor = Instantiate(Floor, new Vector3(x, 0, z), Quaternion.Euler(0, 0, 0));
                floor.transform.parent = transform;

                // TODO copy-paste;
                cell.WallRight = cell.WallRight && !RestrictWall(column, row, WallType.Right);
                cell.WallFront = cell.WallFront && !RestrictWall(column, row, WallType.Front);
                cell.WallLeft = cell.WallLeft && !RestrictWall(column, row, WallType.Left);
                cell.WallBack = cell.WallBack && !RestrictWall(column, row, WallType.Back);

                if (cell.WallRight) {
                    var wall = Instantiate(Wall, new Vector3(x + MazeDescription.CellWidth / 2, 0, z) + Wall.transform.position, Quaternion.Euler(0, 90, 0));
                    wall.transform.parent = transform;
                }
                if (cell.WallFront) {
                    var wall = Instantiate(Wall, new Vector3(x, 0, z + MazeDescription.CellHeight / 2) + Wall.transform.position, Quaternion.Euler(0, 0, 0));
                    wall.transform.parent = transform;
                }
                if (cell.WallLeft) {
                    var wall = Instantiate(Wall, new Vector3(x - MazeDescription.CellWidth / 2, 0, z) + Wall.transform.position, Quaternion.Euler(0, 270, 0));
                    wall.transform.parent = transform;
                }
                if (cell.WallBack) {
                    var wall = Instantiate(Wall, new Vector3(x, 0, z - MazeDescription.CellHeight / 2) + Wall.transform.position, Quaternion.Euler(0, 180, 0));
                    wall.transform.parent = transform;
                }

                var mazeDescriptionCell = new MazeDescriptionCell {
                    Row = row,
                    Column = column,
                    CanMoveBackward = !cell.WallBack,
                    CanMoveLeft = !cell.WallLeft,
                    CanMoveRight = !cell.WallRight,
                    CanMoveForward = !cell.WallFront,
                    HasCoin = false,
                };
                if (row == Rows - 1 && column == Cols - 1)
                    cellsWithCoinIndexes.Add(mazeDescriptionCells.Count);
                else if (row != 0 && column != 0)
                    emptyCellsIndexes.Add(mazeDescriptionCells.Count);
                mazeDescriptionCells.Add(mazeDescriptionCell);
            }
        }

        emptyCellsIndexes = emptyCellsIndexes.OrderBy(a => Random.Range(-1000000, 1000000)).ToList();
        var count = Mathf.Min(mazeDescriptionCells.Count, MazeDescription.Coins - cellsWithCoinIndexes.Count);
        for (var i = 0; i < count; ++i)
            cellsWithCoinIndexes.Add(emptyCellsIndexes[i]);

        Debug.Assert(cellsWithCoinIndexes.Count == MazeDescription.Coins);
        cellsWithCoinIndexes = cellsWithCoinIndexes.OrderBy(x => Random.value).ToList();
        for (var i = 0; i < MazeDescription.Coins; ++i) {
            var cell = mazeDescriptionCells[cellsWithCoinIndexes[i]];
            cell.HasCoin = true;
            var x = cell.Column * MazeDescription.CellWidth;
            var z = cell.Row * MazeDescription.CellHeight;
            var coin = Instantiate(GoalPrefab, new Vector3(x, 1, z), Quaternion.Euler(0, 0, 0));
            coin.transform.parent = transform;
        }

        if (Pillar != null) {
            for (var row = 0; row <= MazeDescription.Rows; ++row) {
                for (var column = 0; column <= MazeDescription.Cols; ++column) {
                    var x = column * MazeDescription.CellWidth;
                    var z = row * MazeDescription.CellHeight;
                    var pillar = Instantiate(Pillar, new Vector3(x - MazeDescription.CellWidth / 2, 0, z - MazeDescription.CellHeight / 2), Quaternion.identity);
                    pillar.transform.parent = transform;
                }
            }
        }

        MazeDescription.Cells = mazeDescriptionCells;
    }
}
