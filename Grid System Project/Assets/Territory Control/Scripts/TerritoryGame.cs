using System;
using System.Collections.Generic;
using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    public sealed class TerritoryGame
    {
        private readonly TerritoryGameSettings _settings;
        private readonly TerritoryAISettings _aiSettings;
        private readonly System.Random _random;
        private readonly TerritoryAI _ai;
        private readonly List<Vector2Int> _pendingAutoFillCells = new List<Vector2Int>();
        private TerritoryOwnership _pendingAutoFillOwnership = TerritoryOwnership.Empty;
        private Vector2Int? _openingAIMove;

        /// <summary>
        /// Creates a territory game and initializes the board, AI, and opening setup state.
        /// </summary>
        public TerritoryGame(TerritoryGameSettings settings = null, TerritoryAISettings aiSettings = null)
        {
            _settings = settings ?? new TerritoryGameSettings();
            _aiSettings = aiSettings ?? new TerritoryAISettings();
            _random = _settings.UseRandomSeed
                ? new System.Random(_settings.RandomSeed)
                : new System.Random();
            _ai = new TerritoryAI(_aiSettings);

            Board = new TerritoryBoard(_settings.Width, _settings.Height, _settings.CellSize, _settings.GridKind);

            if (_aiSettings.StartBehavior == TerritoryAIStartBehavior.PreselectBeforePlayerStart)
            {
                // Some difficulties reserve an AI start before the player chooses their first tile.
                AIStartPlacement = TerritoryStartPlacementUtility.CreateAIStart(
                    Board,
                    _settings.AIStartQuadrantCenterFraction,
                    _random);
                HasAIStartPlacement = true;
            }

            Phase = TerritoryGamePhase.Setup;
            FinalScore = Board.GetScore();
        }

        public TerritoryBoard Board { get; }
        public TerritoryStartPlacement AIStartPlacement { get; private set; }
        public bool HasAIStartPlacement { get; private set; }
        public TerritoryGamePhase Phase { get; private set; }
        public TerritoryScore FinalScore { get; private set; }
        public Vector2Int? LastAIMove { get; private set; }
        public bool DeferAutoFillResolution { get; set; }
        public IReadOnlyList<Vector2Int> PendingAutoFillCells => _pendingAutoFillCells;
        public TerritoryOwnership PendingAutoFillOwnership => _pendingAutoFillOwnership;
        public bool HasPendingAutoFill => _pendingAutoFillCells.Count > 0;

        /// <summary>
        /// Attempts to place the player's start tile and advance the game into player turns.
        /// </summary>
        public bool TrySelectPlayerStart(Vector2Int playerStart)
        {
            if (Phase != TerritoryGamePhase.Setup)
                return false;

            if (!IsLegalPlayerStart(playerStart))
                return false;

            Board.TrySetOwnership(playerStart, TerritoryOwnership.Player);

            if (!HasAIStartPlacement)
            {
                // Responsive AI start placement depends on the player's actual opening cell.
                if (TryCreateResponsiveAIStart(playerStart, out TerritoryStartPlacement aiStartPlacement))
                {
                    AIStartPlacement = aiStartPlacement;
                    HasAIStartPlacement = true;
                }
            }

            if (HasAIStartPlacement)
                Board.TrySetOwnership(AIStartPlacement.AIStart, TerritoryOwnership.AI);

            Phase = TerritoryGamePhase.PlayerTurn;

            // The hardest opening can give the AI its prepared first move immediately after setup.
            if (_aiSettings.StartBehavior == TerritoryAIStartBehavior.SelectAfterPlayerStartAndMove && !ResolveEndStateIfNeeded())
                ResolveAIMove(_openingAIMove);

            ResolveEndStateIfNeeded();
            return true;
        }

        /// <summary>
        /// Attempts to expand the player's territory into a legal neighboring cell.
        /// </summary>
        public bool TryPlayerExpand(Vector2Int position)
        {
            if (Phase != TerritoryGamePhase.PlayerTurn || !Board.IsLegalExpansion(TerritorySide.Player, position))
                return false;

            Board.TrySetOwnership(position, TerritoryOwnership.Player);
            ResolveAfterPlayerMove();
            return true;
        }

        /// <summary>
        /// Gets the cells the player may legally expand into this turn.
        /// </summary>
        public IReadOnlyList<Vector2Int> GetLegalPlayerMoves()
        {
            return Board.GetLegalExpansions(TerritorySide.Player);
        }

        /// <summary>
        /// Gets the current board score without changing game state.
        /// </summary>
        public TerritoryScore GetCurrentScore()
        {
            return Board.GetScore();
        }

        /// <summary>
        /// Applies a subset of deferred final-fill cells for the auto-fill animation.
        /// </summary>
        public void ApplyPendingAutoFillCells(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (!HasPendingAutoFill)
                return;

            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                Vector2Int cell = cells[cellIndex];
                if (_pendingAutoFillCells.Contains(cell) && Board.GetOwnership(cell) == TerritoryOwnership.Empty)
                    Board.TrySetOwnership(cell, _pendingAutoFillOwnership);
            }

            // Remove any cells that were consumed by this animation step or external state changes.
            _pendingAutoFillCells.RemoveAll(cell => Board.GetOwnership(cell) != TerritoryOwnership.Empty);

            if (_pendingAutoFillCells.Count == 0)
                CompleteGame();
        }

        /// <summary>
        /// Resolves the AI response and any end-state after a successful player move.
        /// </summary>
        private void ResolveAfterPlayerMove()
        {
            if (ResolveEndStateIfNeeded())
                return;

            ResolveAIMove();
            ResolveEndStateIfNeeded();
        }

        /// <summary>
        /// Resolves a normal AI move.
        /// </summary>
        private void ResolveAIMove()
        {
            ResolveAIMove(null);
        }

        /// <summary>
        /// Resolves an AI move, optionally prioritizing a prepared opening move.
        /// </summary>
        private void ResolveAIMove(Vector2Int? preferredMove)
        {
            Phase = TerritoryGamePhase.AITurn;

            // Prepared moves are only used if they are still legal after setup.
            if (preferredMove.HasValue && Board.IsLegalExpansion(TerritorySide.AI, preferredMove.Value))
            {
                Board.TrySetOwnership(preferredMove.Value, TerritoryOwnership.AI);
                LastAIMove = preferredMove.Value;
                _openingAIMove = null;
            }
            else if (_ai.TryChooseMove(Board, _random, out Vector2Int aiMove))
            {
                _openingAIMove = null;
                Board.TrySetOwnership(aiMove, TerritoryOwnership.AI);
                LastAIMove = aiMove;
            }

            Phase = TerritoryGamePhase.PlayerTurn;
        }

        /// <summary>
        /// Creates an AI start placement that responds to the player's selected start.
        /// </summary>
        private bool TryCreateResponsiveAIStart(Vector2Int playerStart, out TerritoryStartPlacement aiStartPlacement)
        {
            if (_aiSettings.StartBehavior == TerritoryAIStartBehavior.SelectAfterPlayerStartAndMove)
            {
                if (TerritoryStartPlacementUtility.TryCreateBlockingOpeningAgainstPlayer(
                    Board,
                    playerStart,
                    _aiSettings.StartPlayerReachableReductionWeight,
                    _random,
                    out aiStartPlacement,
                    out Vector2Int openingMove))
                {
                    _openingAIMove = openingMove;
                    return true;
                }
            }

            _openingAIMove = null;
            // Prefer a simple adjacent start before falling back to a scored board-wide placement.
            if (TerritoryStartPlacementUtility.TryCreateOutwardNeighborAIStart(
                Board,
                playerStart,
                _random,
                out aiStartPlacement))
            {
                return true;
            }

            return TerritoryStartPlacementUtility.TryCreateAIStartAgainstPlayer(
                Board,
                playerStart,
                _aiSettings.StartPlayerReachableReductionWeight,
                _random,
                out aiStartPlacement);
        }

        /// <summary>
        /// Checks whether the player can use the given cell as a starting tile.
        /// </summary>
        private bool IsLegalPlayerStart(Vector2Int playerStart)
        {
            if (!Board.IsValidPosition(playerStart) || Board.GetOwnership(playerStart) != TerritoryOwnership.Empty)
                return false;

            return !HasAIStartPlacement || playerStart != AIStartPlacement.AIStart;
        }

        /// <summary>
        /// Resolves completion and automatic territory fill when either side is blocked.
        /// </summary>
        private bool ResolveEndStateIfNeeded()
        {
            List<Vector2Int> playerLegalMoves = Board.GetLegalExpansions(TerritorySide.Player);
            List<Vector2Int> aiLegalMoves = Board.GetLegalExpansions(TerritorySide.AI);

            if (playerLegalMoves.Count > 0 && aiLegalMoves.Count > 0)
                return false;

            // Reachability decides whether blocked empty territory belongs entirely to one side.
            HashSet<Vector2Int> playerReachable = Board.GetReachableEmptyCells(TerritorySide.Player);
            HashSet<Vector2Int> aiReachable = Board.GetReachableEmptyCells(TerritorySide.AI);

            if (playerLegalMoves.Count == 0 && aiLegalMoves.Count == 0)
            {
                CompleteGame();
                return true;
            }

            Phase = TerritoryGamePhase.Resolving;

            if (playerLegalMoves.Count > 0 && aiReachable.Count == 0)
            {
                ResolveReachableFill(playerReachable, TerritoryOwnership.Player);
                return true;
            }

            if (aiLegalMoves.Count > 0 && playerReachable.Count == 0)
            {
                ResolveReachableFill(aiReachable, TerritoryOwnership.AI);
                return true;
            }

            if (playerReachable.Count == 0 && aiReachable.Count == 0)
            {
                CompleteGame();
                return true;
            }

            Phase = TerritoryGamePhase.PlayerTurn;
            return false;
        }

        /// <summary>
        /// Resolves reachable empty cells either immediately or through deferred animation data.
        /// </summary>
        private void ResolveReachableFill(HashSet<Vector2Int> reachableCells, TerritoryOwnership ownership)
        {
            if (!DeferAutoFillResolution)
            {
                FillReachable(reachableCells, ownership);
                CompleteGame();
                return;
            }

            _pendingAutoFillCells.Clear();
            foreach (Vector2Int cell in reachableCells)
            {
                if (Board.GetOwnership(cell) == TerritoryOwnership.Empty)
                    _pendingAutoFillCells.Add(cell);
            }

            _pendingAutoFillOwnership = ownership;

            if (_pendingAutoFillCells.Count == 0)
                CompleteGame();
        }

        /// <summary>
        /// Immediately claims all empty reachable cells for an owner.
        /// </summary>
        private void FillReachable(HashSet<Vector2Int> reachableCells, TerritoryOwnership ownership)
        {
            foreach (Vector2Int cell in reachableCells)
            {
                if (Board.GetOwnership(cell) == TerritoryOwnership.Empty)
                    Board.TrySetOwnership(cell, ownership);
            }
        }

        /// <summary>
        /// Captures the final score and marks the game complete.
        /// </summary>
        private void CompleteGame()
        {
            FinalScore = Board.GetScore();
            Phase = TerritoryGamePhase.Complete;
        }

        
    }
}
