using System;
using System.Collections.Generic;
using UnityEngine;

namespace MerelyGames.Grids
{
    /// <summary>
    /// Geometry strategy for a regular square grid.
    /// </summary>
    public sealed class SquareGridGeometry2D : ISpatialGridGeometry2D
    {
        /// <summary>
        /// Gets the number of corners in a square cell.
        /// </summary>
        public int CellCornerCount => 4;

        /// <summary>
        /// Gets the geometry shape represented by this strategy.
        /// </summary>
        public GridGeometryType2D GeometryType2D => GridGeometryType2D.Square;

        /// <summary>
        /// Converts square cell indices into the world-space position of the cell center.
        /// </summary>
        public Vector3 CellToWorldCenter(int x, int y, in Grid2DLayout layout, float depthOffset = 0f)
        {
            float step = layout.CellSize + layout.CellSpacing;
            float u = (x * step) + (layout.CellSize * 0.5f);
            float v = (y * step) + (layout.CellSize * 0.5f);
            return layout.ToWorldOnPlane(u, v, depthOffset);
        }

        /// <summary>
        /// Converts a world-space position into square grid indices.
        /// </summary>
        public void WorldToCell(Vector3 worldPosition, in Grid2DLayout layout, out int x, out int y)
        {
            layout.WorldToPlaneUV(worldPosition, out float u, out float v);

            // Match the historical no-spacing mapping when spacing is disabled.
            float denominator = (layout.CellSpacing > 0f) ? (layout.CellSize + layout.CellSpacing) : layout.CellSize;
            x = Mathf.FloorToInt(u / denominator);
            y = Mathf.FloorToInt(v / denominator);
        }

        /// <summary>
        /// Writes the ordered corner positions for a square cell into the provided buffer.
        /// </summary>
        public void GetCellCornersWorld(int x, int y, in Grid2DLayout layout, Vector3[] cornersBuffer, float depthOffset = 0f)
        {
            float step = layout.CellSize + layout.CellSpacing;

            float u0 = x * step;
            float v0 = y * step;

            float u1 = u0 + layout.CellSize;
            float v1 = v0 + layout.CellSize;

            // Keep a stable clockwise order so helpers can rely on corner 0 as bottom-left.
            cornersBuffer[0] = layout.ToWorldOnPlane(u0, v0, depthOffset);
            cornersBuffer[1] = layout.ToWorldOnPlane(u0, v1, depthOffset);
            cornersBuffer[2] = layout.ToWorldOnPlane(u1, v1, depthOffset);
            cornersBuffer[3] = layout.ToWorldOnPlane(u1, v0, depthOffset);
        }

        /// <summary>
        /// Appends debug line segments for the square grid boundaries.
        /// </summary>
        public void AppendDebugLineSegments(int gridWidth, int gridHeight, in Grid2DLayout layout, in GridDebugSettings settings, List<GridLineSegment> segments)
        {
            // Square grids can draw shared grid lines once instead of outlining every cell separately.
            float step = layout.CellSize + layout.CellSpacing;
            float depth = settings.DepthOffset;

            // Draw column separators across the full grid height.
            for (int x = 0; x <= gridWidth; x++)
            {
                float u = x * step;
                segments.Add(new GridLineSegment(
                    layout.ToWorldOnPlane(u, 0f, depth),
                    layout.ToWorldOnPlane(u, gridHeight * step, depth)));
            }

            // Draw row separators across the full grid width.
            for (int y = 0; y <= gridHeight; y++)
            {
                float v = y * step;
                segments.Add(new GridLineSegment(
                    layout.ToWorldOnPlane(0f, v, depth),
                    layout.ToWorldOnPlane(gridWidth * step, v, depth)));
            }
        }

        /// <summary>
        /// Computes the world-space bounds that surround the full square grid.
        /// </summary>
        public void GetGridWorldBounds(int gridWidth, int gridHeight, in Grid2DLayout layout, out Vector3 minBounds, out Vector3 maxBounds, float depthOffset = 0)
        {
            float step = layout.CellSize + layout.CellSpacing;

            float uMax = (gridWidth * step) - layout.CellSpacing;
            float vMax = (gridHeight * step) - layout.CellSpacing;

			Vector3 corner00 = layout.ToWorldOnPlane(0f, 0f, depthOffset);
			Vector3 cornerU0 = layout.ToWorldOnPlane(uMax, 0f, depthOffset);
			Vector3 corner0V = layout.ToWorldOnPlane(0f, vMax, depthOffset);
			Vector3 cornerUV = layout.ToWorldOnPlane(uMax, vMax, depthOffset);

			minBounds = Vector3.Min(Vector3.Min(corner00, cornerU0), Vector3.Min(corner0V, cornerUV));
			maxBounds = Vector3.Max(Vector3.Max(corner00, cornerU0), Vector3.Max(corner0V, cornerUV));
		}

        /// <summary>
        /// Returns the plane size of the square grid in layout coordinates.
        /// </summary>
        public Vector2 GetGridPlaneSize(int gridWidth, int gridHeight, in Grid2DLayout layout)
        {
            if (gridWidth <= 0 || gridHeight <= 0) return Vector2.zero;

			float step = layout.CellSize + layout.CellSpacing;

			float uMax = (gridWidth * step) - layout.CellSpacing;
			float vMax = (gridHeight * step) - layout.CellSpacing;

			return new Vector2(uMax, vMax);
		}

        /// <summary>
        /// Populates the neighbor buffer for a square cell using the requested adjacency mode.
        /// </summary>
        public int FillNeighborPositionsBuffer(int x, int y, GridAdjacencyMode2D mode, Vector2Int[] neighborsBuffer)
        {
            if (neighborsBuffer == null) throw new ArgumentNullException(nameof(neighborsBuffer));

			if (mode == GridAdjacencyMode2D.EdgeNeighborsOnly)
			{
				if (neighborsBuffer.Length < 4) throw new ArgumentException("neighborsBuffer length must be >= 4.");

				// Keep neighbor order stable for callers that process directions by index.
				neighborsBuffer[0] = new Vector2Int(x, y + 1); // top
				neighborsBuffer[1] = new Vector2Int(x + 1, y);     // right
				neighborsBuffer[2] = new Vector2Int(x, y - 1); // bottom
				neighborsBuffer[3] = new Vector2Int(x - 1, y);     // left
				return 4;
			}

			if (neighborsBuffer.Length < 8) throw new ArgumentException("neighborsBuffer length must be >= 8.");

			// Include diagonals between edge neighbors while preserving clockwise ordering.
			neighborsBuffer[0] = new Vector2Int(x, y + 1); // top
			neighborsBuffer[1] = new Vector2Int(x + 1, y + 1); // top-right
			neighborsBuffer[2] = new Vector2Int(x + 1, y);     // right
			neighborsBuffer[3] = new Vector2Int(x + 1, y - 1); // bottom-right
			neighborsBuffer[4] = new Vector2Int(x, y - 1); // bottom
			neighborsBuffer[5] = new Vector2Int(x - 1, y - 1); // bottom-left
			neighborsBuffer[6] = new Vector2Int(x - 1, y);     // left
			neighborsBuffer[7] = new Vector2Int(x - 1, y + 1); // top-left
			return 8;
		}

	}

}
