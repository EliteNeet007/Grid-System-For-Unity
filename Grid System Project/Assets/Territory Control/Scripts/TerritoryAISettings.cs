using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    public enum TerritoryAIDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    public enum TerritoryAIStartBehavior
    {
        PreselectBeforePlayerStart,
        SelectAfterPlayerStart,
        SelectAfterPlayerStartAndMove,
    }

    [System.Serializable]
    public class TerritoryAISettings
    {
        [Tooltip("Controls when and how the AI chooses its starting territory.")]
        public TerritoryAIStartBehavior StartBehavior = TerritoryAIStartBehavior.SelectAfterPlayerStart;
        [Tooltip("Weight for reducing the player's reachable area when choosing AI starts.")]
        public float StartPlayerReachableReductionWeight = 1f;
        [Tooltip("Weight for moves that preserve future AI expansion options.")]
        public float FutureReachableWeight = 1.2f;
        [Tooltip("Weight for moves that reduce the player's reachable empty cells.")]
        public float PlayerReachableReductionWeight = 1f;
        [Tooltip("Score bonus for landing on any board edge.")]
        public float EdgeLandingWeight = 5f;
        [Tooltip("Score bonus for landing on an edge the AI has not reached yet.")]
        public float UnreachedEdgeLandingWeight = 18f;
        [Tooltip("Score bonus for moving closer to an unreached edge.")]
        public float EdgeApproachWeight = 2.5f;
        [Tooltip("Score bonus for moves that leave the player without expansion options.")]
        public float GuaranteedBlockWeight = 100f;
        [Tooltip("Weight for simulating the best player reply to an AI move.")]
        public float PlayerReplyLookaheadWeight = 0f;
        [Tooltip("Weight for preferring moves away from the AI's recent move history.")]
        public float DirectionDiversityWeight = 1.5f;
        [Tooltip("Penalty for repeatedly pursuing the same nearest edge.")]
        public float SameEdgeRepetitionPenalty = 4f;
        [Tooltip("Number of recent AI moves considered by direction diversity scoring.")]
        public int RecentMoveMemory = 3;
        [Tooltip("Score difference treated as a tie when randomly choosing among best moves.")]
        public float EqualScoreTolerance = 0.001f;

        /// <summary>
        /// Creates the AI tuning values associated with a difficulty level.
        /// </summary>
        public static TerritoryAISettings CreatePreset(TerritoryAIDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryAIDifficulty.Easy => new TerritoryAISettings
                {
                    StartBehavior = TerritoryAIStartBehavior.PreselectBeforePlayerStart,
                    StartPlayerReachableReductionWeight = 0f,
                    FutureReachableWeight = 0.15f,
                    PlayerReachableReductionWeight = 0.1f,
                    EdgeLandingWeight = 0.5f,
                    UnreachedEdgeLandingWeight = 1f,
                    EdgeApproachWeight = 0.25f,
                    GuaranteedBlockWeight = 0f,
                    DirectionDiversityWeight = 2.5f,
                    SameEdgeRepetitionPenalty = 0f,
                    RecentMoveMemory = 1,
                    EqualScoreTolerance = 20f,
                },

                TerritoryAIDifficulty.Hard => new TerritoryAISettings
                {
                    StartBehavior = TerritoryAIStartBehavior.SelectAfterPlayerStartAndMove,
                    StartPlayerReachableReductionWeight = 2f,
                    FutureReachableWeight = 1.8f,
                    PlayerReachableReductionWeight = 2.2f,
                    EdgeLandingWeight = 4f,
                    UnreachedEdgeLandingWeight = 8f,
                    EdgeApproachWeight = 1f,
                    GuaranteedBlockWeight = 250f,
                    PlayerReplyLookaheadWeight = 0.85f,
                    DirectionDiversityWeight = 0.5f,
                    SameEdgeRepetitionPenalty = 1f,
                    RecentMoveMemory = 2,
                    EqualScoreTolerance = 0.0001f,
                },

                _ => new TerritoryAISettings(),
            };
        }

        
    }
}
