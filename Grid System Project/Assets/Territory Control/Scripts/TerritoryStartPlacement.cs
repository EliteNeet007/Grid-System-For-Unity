using System;
using System.Collections.Generic;
using MerelyGames.Grids;
using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    public readonly struct TerritoryStartPlacement
    {
        public readonly int QuadrantIndex;
        public readonly RectInt Quadrant;
        public readonly Vector2Int AIStart;
        public readonly IReadOnlyList<Vector2Int> CandidateCells;

        /// <summary>
        /// Stores the selected AI start and the candidate region it came from.
        /// </summary>
        public TerritoryStartPlacement(int quadrantIndex, RectInt quadrant, Vector2Int aiStart, IReadOnlyList<Vector2Int> candidateCells)
        {
            QuadrantIndex = quadrantIndex;
            Quadrant = quadrant;
            AIStart = aiStart;
            CandidateCells = candidateCells;
        }
    }

    public static class TerritoryStartPlacementUtility
    {
        /// <summary>
        /// Creates a random AI start from the center candidates of a random quadrant.
        /// </summary>
        public static TerritoryStartPlacement CreateAIStart(TerritoryBoard board, float centerFraction, System.Random random)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));

            int quadrantIndex = random.Next(0, 4);
            RectInt quadrant = GetQuadrant(board.Width, board.Height, quadrantIndex);
            List<Vector2Int> candidates = GetQuadrantCenterCandidates(board, quadrant, centerFraction);
            Vector2Int aiStart = candidates[random.Next(0, candidates.Count)];

            return new TerritoryStartPlacement(quadrantIndex, quadrant, aiStart, candidates);
        }

        /// <summary>
        /// Finds an AI start that best reduces the player's reachable empty territory.
        /// </summary>
        public static bool TryCreateAIStartAgainstPlayer(TerritoryBoard board, Vector2Int playerStart, float playerReachableReductionWeight, System.Random random, out TerritoryStartPlacement placement)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));

            placement = default;

            if (!board.IsValidPosition(playerStart) || board.GetOwnership(playerStart) != TerritoryOwnership.Player)
                return false;

            HashSet<Vector2Int> playerReachableBefore = board.GetReachableEmptyCells(TerritorySide.Player);
            float bestScore = float.NegativeInfinity;
            List<Vector2Int> bestStarts = new List<Vector2Int>();
            List<Vector2Int> candidates = new List<Vector2Int>();

            // Temporarily test each empty cell as an AI start, then restore it before scoring the next one.
            foreach (Vector2Int candidate in board.AllPositions())
            {
                if (candidate == playerStart || board.GetOwnership(candidate) != TerritoryOwnership.Empty)
                    continue;

                candidates.Add(candidate);
                board.TrySetOwnership(candidate, TerritoryOwnership.AI);

                int playerReachableAfter = board.GetReachableEmptyCells(TerritorySide.Player).Count;
                int playerReduction = playerReachableBefore.Count - playerReachableAfter;
                float score = playerReduction * playerReachableReductionWeight;

                board.TrySetOwnership(candidate, TerritoryOwnership.Empty);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestStarts.Clear();
                    bestStarts.Add(candidate);
                }
                else if (Mathf.Approximately(score, bestScore))
                {
                    bestStarts.Add(candidate);
                }
            }

            if (bestStarts.Count == 0)
                return false;

            Vector2Int aiStart = bestStarts[random.Next(0, bestStarts.Count)];
            placement = new TerritoryStartPlacement(GetQuadrantIndex(board, aiStart), GetQuadrantForPosition(board, aiStart), aiStart, candidates);
            return true;
        }

        /// <summary>
        /// Finds an adjacent AI start that points outward from the player's start.
        /// </summary>
        public static bool TryCreateOutwardNeighborAIStart(TerritoryBoard board, Vector2Int playerStart, System.Random random, out TerritoryStartPlacement placement)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));

            placement = default;
            List<Vector2Int> candidates = GetEmptyNeighbors(board, playerStart);
            if (candidates.Count == 0)
                return false;

            Vector2Int aiStart = ChooseHighestScoredCell(candidates, random, cell => GetOutwardScore(board, cell));
            placement = CreatePlacement(board, aiStart, candidates);
            return true;
        }

        /// <summary>
        /// Finds an adjacent AI start and prepared opening move that blocks the player.
        /// </summary>
        public static bool TryCreateBlockingOpeningAgainstPlayer(TerritoryBoard board, Vector2Int playerStart, float playerReachableReductionWeight,
            System.Random random, out TerritoryStartPlacement placement, out Vector2Int openingMove)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));

            placement = default;
            openingMove = default;

            List<Vector2Int> startCandidates = GetEmptyNeighbors(board, playerStart);
            if (startCandidates.Count == 0)
                return false;

            HashSet<Vector2Int> playerReachableBefore = board.GetReachableEmptyCells(TerritorySide.Player);
            List<Vector2Int> playerDirections = board.GetLegalExpansions(TerritorySide.Player);
            float bestScore = float.NegativeInfinity;
            List<(Vector2Int Start, Vector2Int Move)> bestOpenings = new List<(Vector2Int, Vector2Int)>();

            for (int startIndex = 0; startIndex < startCandidates.Count; startIndex++)
            {
                Vector2Int start = startCandidates[startIndex];
                board.TrySetOwnership(start, TerritoryOwnership.AI);
                List<Vector2Int> aiMoves = board.GetLegalExpansions(TerritorySide.AI);

                for (int moveIndex = 0; moveIndex < aiMoves.Count; moveIndex++)
                {
                    Vector2Int move = aiMoves[moveIndex];
                    board.TrySetOwnership(move, TerritoryOwnership.AI);

                    int playerReachableAfter = board.GetReachableEmptyCells(TerritorySide.Player).Count;
                    int playerReduction = playerReachableBefore.Count - playerReachableAfter;
                    float score = playerReduction * playerReachableReductionWeight;
                    score += IsContinuationFromPlayer(playerStart, start, move) ? 1000f : 0f;
                    score += playerDirections.Contains(move) ? 500f : 0f;
                    score += GetOutwardScore(board, start) * 0.01f;

                    board.TrySetOwnership(move, TerritoryOwnership.Empty);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOpenings.Clear();
                        bestOpenings.Add((start, move));
                    }
                    else if (Mathf.Approximately(score, bestScore))
                    {
                        bestOpenings.Add((start, move));
                    }
                }

                board.TrySetOwnership(start, TerritoryOwnership.Empty);
            }

            if (bestOpenings.Count == 0)
                return false;

            (Vector2Int selectedStart, Vector2Int selectedMove) = bestOpenings[random.Next(0, bestOpenings.Count)];
            placement = CreatePlacement(board, selectedStart, startCandidates);
            openingMove = selectedMove;
            return true;
        }

        /// <summary>
        /// Gets the board rectangle represented by a quadrant index.
        /// </summary>
        public static RectInt GetQuadrant(int width, int height, int quadrantIndex)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (quadrantIndex < 0 || quadrantIndex > 3) throw new ArgumentOutOfRangeException(nameof(quadrantIndex));

            int leftWidth = (width + 1) / 2;
            int rightWidth = width - leftWidth;
            int bottomHeight = (height + 1) / 2;
            int topHeight = height - bottomHeight;

            bool right = (quadrantIndex & 1) == 1;
            bool top = (quadrantIndex & 2) == 2;

            int x = right ? leftWidth : 0;
            int y = top ? bottomHeight : 0;
            int quadrantWidth = right ? rightWidth : leftWidth;
            int quadrantHeight = top ? topHeight : bottomHeight;

            if (quadrantWidth <= 0)
            {
                x = 0;
                quadrantWidth = width;
            }

            if (quadrantHeight <= 0)
            {
                y = 0;
                quadrantHeight = height;
            }

            return new RectInt(x, y, quadrantWidth, quadrantHeight);
        }

        /// <summary>
        /// Gets the cells closest to the center of a quadrant.
        /// </summary>
        public static List<Vector2Int> GetQuadrantCenterCandidates(TerritoryBoard board, RectInt quadrant, float centerFraction)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (quadrant.width <= 0 || quadrant.height <= 0) throw new ArgumentOutOfRangeException(nameof(quadrant));

            int targetCount = Mathf.Max(1, Mathf.CeilToInt(quadrant.width * quadrant.height * Mathf.Clamp01(centerFraction)));
            Vector2Int center = new Vector2Int(
                quadrant.xMin + (quadrant.width - 1) / 2,
                quadrant.yMin + (quadrant.height - 1) / 2);

            List<Vector2Int> candidates = new List<Vector2Int> { center };
            HashSet<Vector2Int> visited = new HashSet<Vector2Int> { center };
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            Vector2Int[] neighbors = new Vector2Int[12];
            GridAdjacencyMode2D mode = board.GridKind == TerritoryGridKind.Square
                ? GridAdjacencyMode2D.EdgeNeighborsOnly
                : GridAdjacencyMode2D.IncludeVertexNeighbors;

            frontier.Enqueue(center);

            while (frontier.Count > 0 && candidates.Count < targetCount)
            {
                Vector2Int current = frontier.Dequeue();
                int count = board.Grid.FillNeighborsBuffer(current, neighbors, mode);

                for (int neighborIndex = 0; neighborIndex < count && candidates.Count < targetCount; neighborIndex++)
                {
                    Vector2Int neighbor = neighbors[neighborIndex];
                    if (visited.Contains(neighbor) || !quadrant.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);
                    candidates.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Gets the quadrant that contains a board position.
        /// </summary>
        private static RectInt GetQuadrantForPosition(TerritoryBoard board, Vector2Int position)
        {
            return GetQuadrant(board.Width, board.Height, GetQuadrantIndex(board, position));
        }

        /// <summary>
        /// Creates placement metadata for a selected AI start.
        /// </summary>
        private static TerritoryStartPlacement CreatePlacement(TerritoryBoard board, Vector2Int aiStart, IReadOnlyList<Vector2Int> candidates)
        {
            return new TerritoryStartPlacement(GetQuadrantIndex(board, aiStart), GetQuadrantForPosition(board, aiStart), aiStart, candidates);
        }

        /// <summary>
        /// Gets empty neighboring cells around a position.
        /// </summary>
        private static List<Vector2Int> GetEmptyNeighbors(TerritoryBoard board, Vector2Int position)
        {
            List<Vector2Int> candidates = board.GetNeighbors(position);
            candidates.RemoveAll(cell => board.GetOwnership(cell) != TerritoryOwnership.Empty);
            return candidates;
        }

        /// <summary>
        /// Chooses randomly among the highest-scored cells.
        /// </summary>
        private static Vector2Int ChooseHighestScoredCell(List<Vector2Int> cells, System.Random random, Func<Vector2Int, float> getScore)
        {
            float bestScore = float.NegativeInfinity;
            List<Vector2Int> bestCells = new List<Vector2Int>();

            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                Vector2Int cell = cells[cellIndex];
                float score = getScore(cell);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCells.Clear();
                    bestCells.Add(cell);
                }
                else if (Mathf.Approximately(score, bestScore))
                {
                    bestCells.Add(cell);
                }
            }

            return bestCells[random.Next(0, bestCells.Count)];
        }

        /// <summary>
        /// Scores a cell by how far it points away from the board center.
        /// </summary>
        private static float GetOutwardScore(TerritoryBoard board, Vector2Int cell)
        {
            float centerX = (board.Width - 1) * 0.5f;
            float centerY = (board.Height - 1) * 0.5f;
            float deltaX = cell.x - centerX;
            float deltaY = cell.y - centerY;
            return deltaX * deltaX + deltaY * deltaY;
        }

        /// <summary>
        /// Checks whether the AI move continues in the direction away from the player.
        /// </summary>
        private static bool IsContinuationFromPlayer(Vector2Int playerStart, Vector2Int aiStart, Vector2Int move)
        {
            Vector2Int direction = aiStart - playerStart;
            return move == aiStart + direction;
        }

        /// <summary>
        /// Gets the quadrant index containing a position.
        /// </summary>
        private static int GetQuadrantIndex(TerritoryBoard board, Vector2Int position)
        {
            int leftWidth = (board.Width + 1) / 2;
            int bottomHeight = (board.Height + 1) / 2;

            int quadrantIndex = 0;
            if (position.x >= leftWidth)
                quadrantIndex |= 1;
            if (position.y >= bottomHeight)
                quadrantIndex |= 2;

            return quadrantIndex;
        }


    }
}
