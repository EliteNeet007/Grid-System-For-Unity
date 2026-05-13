using System;
using System.Collections.Generic;
using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    /// <summary>
    /// Stores a debug score for a candidate AI move.
    /// </summary>
    public readonly struct TerritoryAIMoveScore
    {
        public readonly Vector2Int Move;
        public readonly float Score;

        public TerritoryAIMoveScore(Vector2Int move, float score)
        {
            Move = move;
            Score = score;
        }
    }

    /// <summary>
    /// Creates an AI move selector with the supplied tuning settings.
    /// </summary>
    public sealed class TerritoryAI
    {
        private readonly TerritoryAISettings _settings;
        private readonly Queue<Vector2Int> _recentMoves = new Queue<Vector2Int>();
        private readonly HashSet<int> _reachedEdges = new HashSet<int>();
        private int _lastSoughtEdge = -1;

        public TerritoryAI(TerritoryAISettings settings)
        {
            _settings = settings ?? new TerritoryAISettings();
        }

        /// <summary>
        /// Chooses a legal AI expansion move using the configured scoring weights.
        /// </summary>
        public bool TryChooseMove(TerritoryBoard board, System.Random random, out Vector2Int move)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));

            List<Vector2Int> legalMoves = board.GetLegalExpansions(TerritorySide.AI);
            move = default;

            if (legalMoves.Count == 0)
                return false;

            HashSet<Vector2Int> playerReachableBefore = board.GetReachableEmptyCells(TerritorySide.Player);
            float bestScore = float.NegativeInfinity;
            List<Vector2Int> bestMoves = new List<Vector2Int>();

            for (int moveIndex = 0; moveIndex < legalMoves.Count; moveIndex++)
            {
                Vector2Int candidate = legalMoves[moveIndex];
                float score = ScoreMove(board, candidate, playerReachableBefore);

                if (score > bestScore + _settings.EqualScoreTolerance)
                {
                    bestScore = score;
                    bestMoves.Clear();
                    bestMoves.Add(candidate);
                }
                else if (Mathf.Abs(score - bestScore) <= _settings.EqualScoreTolerance)
                {
                    bestMoves.Add(candidate);
                }
            }

            move = bestMoves[random.Next(0, bestMoves.Count)];
            RememberMove(board, move);
            return true;
        }

        /// <summary>
        /// Scores a specific move for debugging without choosing from all legal moves.
        /// </summary>
        public TerritoryAIMoveScore ScoreMoveForDebug(TerritoryBoard board, Vector2Int move)
        {
            HashSet<Vector2Int> playerReachableBefore = board.GetReachableEmptyCells(TerritorySide.Player);
            return new TerritoryAIMoveScore(move, ScoreMove(board, move, playerReachableBefore));
        }

        /// <summary>
        /// Scores a candidate AI move by simulating its immediate impact and strategic value.
        /// </summary>
        private float ScoreMove(TerritoryBoard board, Vector2Int move, HashSet<Vector2Int> playerReachableBefore)
        {
            // Temporarily claim the move so reachability and reply calculations see the candidate board.
            board.TrySetOwnership(move, TerritoryOwnership.AI);

            int aiReachable = board.GetReachableEmptyCells(TerritorySide.AI).Count;
            int playerReachableAfter = board.GetReachableEmptyCells(TerritorySide.Player).Count;
            int playerReduction = playerReachableBefore.Count - playerReachableAfter;
            bool guaranteedBlock = board.GetLegalExpansions(TerritorySide.Player).Count == 0 || playerReachableAfter == 0;
            float playerReplyPenalty = _settings.PlayerReplyLookaheadWeight > 0f
                ? GetBestPlayerReplyScore(board, aiReachable)
                : 0f;

            board.TrySetOwnership(move, TerritoryOwnership.Empty);

            // Edge and diversity terms encourage expansion that does not stall in one direction.
            int nearestUnreachedEdge = GetNearestUnreachedEdge(board, move, out int edgeDistance);
            bool landsOnEdge = board.IsEdgeCell(move);
            bool landsOnUnreachedEdge = landsOnEdge && !_reachedEdges.Contains(GetTouchedEdge(board, move));
            float diversity = GetDirectionDiversity(move);

            float score = 0f;
            score += aiReachable * _settings.FutureReachableWeight;
            score += playerReduction * _settings.PlayerReachableReductionWeight;
            score += landsOnEdge ? _settings.EdgeLandingWeight : 0f;
            score += landsOnUnreachedEdge ? _settings.UnreachedEdgeLandingWeight : 0f;
            score += nearestUnreachedEdge >= 0 ? _settings.EdgeApproachWeight / Mathf.Max(1, edgeDistance) : 0f;
            score += guaranteedBlock ? _settings.GuaranteedBlockWeight : 0f;
            score += diversity * _settings.DirectionDiversityWeight;
            score -= nearestUnreachedEdge == _lastSoughtEdge ? _settings.SameEdgeRepetitionPenalty : 0f;
            score -= playerReplyPenalty * _settings.PlayerReplyLookaheadWeight;

            return score;
        }

        /// <summary>
        /// Estimates the strongest player reply after a candidate AI move.
        /// </summary>
        private float GetBestPlayerReplyScore(TerritoryBoard board, int aiReachableBeforeReply)
        {
            List<Vector2Int> playerMoves = board.GetLegalExpansions(TerritorySide.Player);
            if (playerMoves.Count == 0)
                return 0f;

            float bestReplyScore = 0f;

            for (int moveIndex = 0; moveIndex < playerMoves.Count; moveIndex++)
            {
                Vector2Int reply = playerMoves[moveIndex];
                board.TrySetOwnership(reply, TerritoryOwnership.Player);

                int aiReachableAfterReply = board.GetReachableEmptyCells(TerritorySide.AI).Count;
                int playerReachableAfterReply = board.GetReachableEmptyCells(TerritorySide.Player).Count;
                int aiReduction = aiReachableBeforeReply - aiReachableAfterReply;
                bool aiBlocked = board.GetLegalExpansions(TerritorySide.AI).Count == 0 || aiReachableAfterReply == 0;

                float replyScore = 0f;
                replyScore += aiReduction * _settings.PlayerReachableReductionWeight;
                replyScore += Mathf.Max(0, playerReachableAfterReply - aiReachableAfterReply) * _settings.FutureReachableWeight;
                replyScore += aiBlocked ? _settings.GuaranteedBlockWeight : 0f;

                board.TrySetOwnership(reply, TerritoryOwnership.Empty);

                if (replyScore > bestReplyScore)
                    bestReplyScore = replyScore;
            }

            return bestReplyScore;
        }

        /// <summary>
        /// Records a chosen move so future scoring can favor directional variety.
        /// </summary>
        private void RememberMove(TerritoryBoard board, Vector2Int move)
        {
            _recentMoves.Enqueue(move);
            while (_recentMoves.Count > Mathf.Max(1, _settings.RecentMoveMemory))
                _recentMoves.Dequeue();

            int touchedEdge = GetTouchedEdge(board, move);
            if (touchedEdge >= 0)
                _reachedEdges.Add(touchedEdge);

            _lastSoughtEdge = GetNearestUnreachedEdge(board, move, out _);
        }

        /// <summary>
        /// Measures how far a candidate move is from the recent move history.
        /// </summary>
        private float GetDirectionDiversity(Vector2Int move)
        {
            if (_recentMoves.Count == 0)
                return 1f;

            float totalDistance = 0f;
            foreach (Vector2Int recentMove in _recentMoves)
                totalDistance += Vector2Int.Distance(move, recentMove);

            return totalDistance / _recentMoves.Count;
        }

        /// <summary>
        /// Finds the nearest board edge the AI has not already reached.
        /// </summary>
        private int GetNearestUnreachedEdge(TerritoryBoard board, Vector2Int move, out int distance)
        {
            int[] distances =
            {
                move.y,
                board.Width - 1 - move.x,
                board.Height - 1 - move.y,
                move.x,
            };

            int bestEdge = -1;
            distance = int.MaxValue;

            for (int edgeIndex = 0; edgeIndex < distances.Length; edgeIndex++)
            {
                if (_reachedEdges.Contains(edgeIndex) || distances[edgeIndex] >= distance)
                    continue;

                bestEdge = edgeIndex;
                distance = distances[edgeIndex];
            }

            return bestEdge;
        }

        /// <summary>
        /// Gets the edge index touched by a cell, or -1 when the cell is not on an edge.
        /// </summary>
        private static int GetTouchedEdge(TerritoryBoard board, Vector2Int move)
        {
            if (move.y == 0) return 0;
            if (move.x == board.Width - 1) return 1;
            if (move.y == board.Height - 1) return 2;
            if (move.x == 0) return 3;
            return -1;
        }

        
    }
}
