using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    public enum TerritoryGameSettingsPreset
    {
        Square,
        Hex,
        Triangle,
    }

    [System.Serializable]
    public class TerritoryGameSettings
    {
        [Tooltip("Number of cells across the board.")]
        [Min(1)] public int Width = 12;
        [Tooltip("Number of cells up the board.")]
        [Min(1)] public int Height = 12;
        [Tooltip("World-space size used when generating each grid cell.")]
        public float CellSize = 1f;
        [Tooltip("Geometry used to build the territory board.")]
        public TerritoryGridKind GridKind = TerritoryGridKind.Square;
        [Tooltip("Uses the random seed value to make AI placement and choices deterministic.")]
        public bool UseRandomSeed;
        [Tooltip("Seed used when deterministic random behavior is enabled.")]
        public int RandomSeed;
        [Tooltip("Fraction of a quadrant considered when preselecting an AI start near its center.")]
        [Range(0.01f, 1f)] public float AIStartQuadrantCenterFraction = 0.08f;
    }
}
