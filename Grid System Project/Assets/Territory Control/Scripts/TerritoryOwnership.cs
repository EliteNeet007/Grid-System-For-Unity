namespace MerelyGames.TerritoryControl
{
    public enum TerritoryOwnership
    {
        Empty,
        Player,
        AI,
    }

    public enum TerritorySide
    {
        Player,
        AI,
    }

    public enum TerritoryGridKind
    {
        Square,
        Hex,
        Triangle,
    }

    public enum TerritoryGamePhase
    {
        Setup,
        PlayerTurn,
        AITurn,
        Resolving,
        Complete,
    }

    public enum TerritoryWinner
    {
        None,
        Player,
        AI,
        Tie,
    }
}
