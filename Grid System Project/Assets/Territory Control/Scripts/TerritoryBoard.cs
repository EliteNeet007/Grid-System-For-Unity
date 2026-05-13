using System;
using System.Collections.Generic;
using MerelyGames.Grids;
using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    public sealed class TerritoryBoard
    {
        private readonly Grid2D<TerritoryOwnership> _grid;
        private readonly GridAdjacencyMode2D _adjacencyMode;
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[8];

        /// <summary>
        /// Creates a board backed by the requested grid geometry.
        /// </summary>
        public TerritoryBoard(int width, int height, float cellSize, TerritoryGridKind gridKind)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));

            GridKind = gridKind;
            _adjacencyMode = GridAdjacencyMode2D.EdgeNeighborsOnly;

            _grid = new Grid2D<TerritoryOwnership>(
                width,
                height,
                Vector3.zero,
                CreateOwnership,
                cellSize,
                geometryType: CreateGeometry(gridKind));
        }

        public int Width => _grid.GridWidth;
        public int Height => _grid.GridHeight;
        public TerritoryGridKind GridKind { get; }
        public Grid2D<TerritoryOwnership> Grid => _grid;

        /// <summary>
        /// Checks whether a position is inside the board.
        /// </summary>
        public bool IsValidPosition(Vector2Int position) => _grid.IsValidGridPosition(position);

        /// <summary>
        /// Gets the ownership value at a valid board position.
        /// </summary>
        public TerritoryOwnership GetOwnership(Vector2Int position)
        {
            if (!_grid.TryGetGridObject(position, out TerritoryOwnership ownership))
                throw new ArgumentOutOfRangeException(nameof(position));

            return ownership;
        }

        /// <summary>
        /// Attempts to set ownership at a valid board position.
        /// </summary>
        public bool TrySetOwnership(Vector2Int position, TerritoryOwnership ownership)
        {
            if (!IsValidPosition(position))
                return false;

            return _grid.SetGridObject(position, ownership);
        }

        /// <summary>
        /// Checks whether a player start is valid against a known AI start.
        /// </summary>
        public bool IsLegalStartingCell(Vector2Int position, Vector2Int aiStart)
        {
            return IsValidPosition(position)
                && position != aiStart
                && GetOwnership(position) == TerritoryOwnership.Empty;
        }

        /// <summary>
        /// Checks whether a side can expand into the given empty cell.
        /// </summary>
        public bool IsLegalExpansion(TerritorySide side, Vector2Int position)
        {
            if (!IsValidPosition(position) || GetOwnership(position) != TerritoryOwnership.Empty)
                return false;

            TerritoryOwnership sideOwnership = ToOwnership(side);
            int count = _grid.FillNeighborsBuffer(position, _neighborBuffer, _adjacencyMode);

            for (int neighborIndex = 0; neighborIndex < count; neighborIndex++)
            {
                if (GetOwnership(_neighborBuffer[neighborIndex]) == sideOwnership)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets every cell currently owned by a side.
        /// </summary>
        public List<Vector2Int> GetOwnedCells(TerritorySide side)
        {
            TerritoryOwnership ownership = ToOwnership(side);
            List<Vector2Int> cells = new List<Vector2Int>();

            _grid.ForEachCell((xPosition, yPosition, cellOwnership) =>
            {
                if (cellOwnership == ownership)
                    cells.Add(new Vector2Int(xPosition, yPosition));
            });

            return cells;
        }

        /// <summary>
        /// Gets every empty cell a side can legally expand into.
        /// </summary>
        public List<Vector2Int> GetLegalExpansions(TerritorySide side)
        {
            TerritoryOwnership sideOwnership = ToOwnership(side);
            HashSet<Vector2Int> legal = new HashSet<Vector2Int>();

            _grid.ForEachCell((xPosition, yPosition, ownership) =>
            {
                if (ownership != sideOwnership)
                    return;

                int count = _grid.FillNeighborsBuffer(xPosition, yPosition, _neighborBuffer, _adjacencyMode);
                for (int neighborIndex = 0; neighborIndex < count; neighborIndex++)
                {
                    Vector2Int neighbor = _neighborBuffer[neighborIndex];
                    if (GetOwnership(neighbor) == TerritoryOwnership.Empty)
                        legal.Add(neighbor);
                }
            });

            return new List<Vector2Int>(legal);
        }

        /// <summary>
        /// Gets empty cells reachable by a side without crossing opponent-owned cells.
        /// </summary>
        public HashSet<Vector2Int> GetReachableEmptyCells(TerritorySide side)
        {
            TerritoryOwnership sideOwnership = ToOwnership(side);
            TerritoryOwnership opponentOwnership = ToOwnership(GetOpponent(side));
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();

            _grid.ForEachCell((xPosition, yPosition, ownership) =>
            {
                if (ownership != sideOwnership)
                    return;

                Vector2Int owned = new Vector2Int(xPosition, yPosition);
                frontier.Enqueue(owned);
                visited.Add(owned);
            });

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                int count = _grid.FillNeighborsBuffer(current, _neighborBuffer, _adjacencyMode);

                for (int neighborIndex = 0; neighborIndex < count; neighborIndex++)
                {
                    Vector2Int neighbor = _neighborBuffer[neighborIndex];
                    if (visited.Contains(neighbor) || GetOwnership(neighbor) == opponentOwnership)
                        continue;

                    visited.Add(neighbor);

                    if (GetOwnership(neighbor) == TerritoryOwnership.Empty)
                        reachable.Add(neighbor);

                    frontier.Enqueue(neighbor);
                }
            }

            return reachable;
        }

        /// <summary>
        /// Gets valid neighboring board positions for a cell.
        /// </summary>
        public List<Vector2Int> GetNeighbors(Vector2Int position)
        {
            if (!IsValidPosition(position))
                throw new ArgumentOutOfRangeException(nameof(position));

            int count = _grid.FillNeighborsBuffer(position, _neighborBuffer, _adjacencyMode);
            List<Vector2Int> neighbors = new List<Vector2Int>(count);

            for (int neighborIndex = 0; neighborIndex < count; neighborIndex++)
                neighbors.Add(_neighborBuffer[neighborIndex]);

            return neighbors;
        }

        /// <summary>
        /// Counts player, AI, and empty tiles and returns a score snapshot.
        /// </summary>
        public TerritoryScore GetScore()
        {
            int playerTiles = 0;
            int aiTiles = 0;
            int emptyTiles = 0;

            _grid.ForEachCell((_, _, ownership) =>
            {
                if (ownership == TerritoryOwnership.Player)
                    playerTiles++;
                else if (ownership == TerritoryOwnership.AI)
                    aiTiles++;
                else
                    emptyTiles++;
            });

            return new TerritoryScore(playerTiles, aiTiles, emptyTiles);
        }

        /// <summary>
        /// Enumerates every valid board position.
        /// </summary>
        public IEnumerable<Vector2Int> AllPositions()
        {
            for (int xPosition = 0; xPosition < Width; xPosition++)
            for (int yPosition = 0; yPosition < Height; yPosition++)
                yield return new Vector2Int(xPosition, yPosition);
        }

        /// <summary>
        /// Checks whether a position lies on the outer edge of the board.
        /// </summary>
        public bool IsEdgeCell(Vector2Int position)
        {
            return position.x == 0 || position.y == 0 || position.x == Width - 1 || position.y == Height - 1;
        }

        /// <summary>
        /// Converts a side identifier into its ownership value.
        /// </summary>
        public static TerritoryOwnership ToOwnership(TerritorySide side)
        {
            return side == TerritorySide.Player ? TerritoryOwnership.Player : TerritoryOwnership.AI;
        }

        /// <summary>
        /// Gets the opposing side.
        /// </summary>
        public static TerritorySide GetOpponent(TerritorySide side)
        {
            return side == TerritorySide.Player ? TerritorySide.AI : TerritorySide.Player;
        }

        /// <summary>
        /// Creates the default ownership value for new grid cells.
        /// </summary>
        private static TerritoryOwnership CreateOwnership(int xPosition, int yPosition)
        {
            return TerritoryOwnership.Empty;
        }

        /// <summary>
        /// Creates the grid geometry used by the board.
        /// </summary>
        private static ISpatialGridGeometry2D CreateGeometry(TerritoryGridKind gridKind)
        {
            return gridKind switch
            {
                TerritoryGridKind.Hex => new HexGridGeometry2D(HexOrientation.FlatTop, HexOffsetParity.Even),
                TerritoryGridKind.Triangle => new EquilateralTriangleGridGeometry2D(EquilateralTriangleLayout2D.StackedRows, EquilateralTriangleStackedRowMode2D.OddRowsFlipped),
                _ => new SquareGridGeometry2D(),
            };
        }

        
    }
}
