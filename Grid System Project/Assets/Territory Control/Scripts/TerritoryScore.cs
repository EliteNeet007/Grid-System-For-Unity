namespace MerelyGames.TerritoryControl
{
    public readonly struct TerritoryScore
    {
        public readonly int PlayerTiles;
        public readonly int AITiles;
        public readonly int EmptyTiles;
        public readonly TerritoryWinner Winner;

        /// <summary>
        /// Creates a score snapshot and determines the winner from tile counts.
        /// </summary>
        public TerritoryScore(int playerTiles, int aiTiles, int emptyTiles)
        {
            PlayerTiles = playerTiles;
            AITiles = aiTiles;
            EmptyTiles = emptyTiles;

            if (playerTiles > aiTiles)
                Winner = TerritoryWinner.Player;
            else if (aiTiles > playerTiles)
                Winner = TerritoryWinner.AI;
            else
                Winner = TerritoryWinner.Tie;
        }
    }
}
