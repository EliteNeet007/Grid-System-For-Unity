using UnityEngine;

namespace MerelyGames.Grids
{
    /// <summary>
    /// Helper methods for square grid-specific queries.
    /// </summary>
    public static class SquareGridHelpers
    {
        /// <summary>
        /// Returns the bottom-left corner of a square cell in world space.
        /// </summary>
        /// <typeparam name="T">The grid object type.</typeparam>
        /// <param name="grid">The grid instance, which must use square geometry.</param>
        /// <param name="x">Grid cell X index.</param>
        /// <param name="y">Grid cell Y index.</param>
        /// <param name="depthOffset">Optional depth offset along the grid plane.</param>
        public static Vector3 GetSquareCellBottomLeft<T>(Grid2D<T> grid, int x, int y, float depthOffset = 0f)
        {
            if (grid.Geometry is not SquareGridGeometry2D) throw new System.NotSupportedException("SquareGridHelpers requires SquareGridGeometry2D.");

            // We do not have public access to layout here, so compute corners and rely on square geometry's defined order.
            Vector3[] corners = new Vector3[4];
            grid.GetCellCornersWorldPosition(x, y, corners, depthOffset);

            // Corner 0 in SquareGridGeometry2D is bottom-left by definition.
            return corners[0];
        }
    }
}
