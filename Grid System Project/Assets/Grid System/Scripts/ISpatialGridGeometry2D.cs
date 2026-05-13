using System.Collections.Generic;
using UnityEngine;

namespace MerelyGames.Grids
{
    /// <summary>
    /// Strategy interface for mapping between grid indices (int,int) and world space,
    /// plus producing debug geometry as line segments.
    /// 
    /// IMPORTANT: The canonical mapping is:
    /// - CellToWorldCenter
    /// - GetCellCornersWorld
    /// - WorldToCell
    /// </summary>
    public interface ISpatialGridGeometry2D
    {
        /// <summary>
        /// Gets the geometry shape represented by this strategy.
        /// </summary>
        GridGeometryType2D GeometryType2D { get; }

        /// <summary>
        /// Gets the number of corners in each cell polygon.
        /// </summary>
        int CellCornerCount { get; }

        /// <summary>
        /// Converts cell indices into the world-space position at the center of the cell.
        /// </summary>
        /// <param name="x">Grid cell X index.</param>
        /// <param name="y">Grid cell Y index.</param>
        /// <param name="layout">The current grid layout parameters.</param>
        /// <param name="depthOffset">An optional offset along the grid's depth axis.</param>
        Vector3 CellToWorldCenter(int x, int y, in Grid2DLayout layout, float depthOffset = 0f);

        /// <summary>
        /// Converts a world position into grid cell indices.
        /// </summary>
        /// <param name="worldPosition">The world-space point to map onto the grid.</param>
        /// <param name="layout">The current grid layout parameters.</param>
        /// <param name="x">Output grid cell X index.</param>
        /// <param name="y">Output grid cell Y index.</param>
        void WorldToCell(Vector3 worldPosition, in Grid2DLayout layout, out int x, out int y);

        /// <summary>
        /// Writes the world-space corners of a cell into the provided buffer.
        /// </summary>
        /// <param name="x">Grid cell X index.</param>
        /// <param name="y">Grid cell Y index.</param>
        /// <param name="layout">The current grid layout parameters.</param>
        /// <param name="cornersBuffer">A buffer with length at least <see cref="CellCornerCount"/>.</param>
        /// <param name="depthOffset">An optional offset along the grid's depth axis.</param>
        void GetCellCornersWorld(int x, int y, in Grid2DLayout layout, Vector3[] cornersBuffer, float depthOffset = 0f);

        /// <summary>
        /// Appends debug line segments representing the grid visualization.
        /// </summary>
        /// <param name="gridWidth">The number of columns in the grid.</param>
        /// <param name="gridHeight">The number of rows in the grid.</param>
        /// <param name="layout">The current grid layout parameters.</param>
        /// <param name="settings">Additional debug geometry options.</param>
        /// <param name="segments">The output segment list to append values to.</param>
        void AppendDebugLineSegments(int gridWidth, int gridHeight, in Grid2DLayout layout, in GridDebugSettings settings, List<GridLineSegment> segments);

        /// <summary>
        /// Computes the world-space bounds that contain the full grid.
        /// </summary>
        /// <param name="gridWidth">The number of columns in the grid.</param>
        /// <param name="gridHeight">The number of rows in the grid.</param>
        /// <param name="layout">The current grid layout parameters.</param>
        /// <param name="minBounds">Output minimum world-space bounds.</param>
        /// <param name="maxBounds">Output maximum world-space bounds.</param>
        /// <param name="depthOffset">An optional offset along the grid's depth axis.</param>
        void GetGridWorldBounds(int gridWidth, int gridHeight, in Grid2DLayout layout, out Vector3 minBounds, out Vector3 maxBounds, float depthOffset = 0f);

        /// <summary>
        /// Returns the size of the grid in plane coordinates.
        /// </summary>
        /// <param name="gridWidth">The number of columns in the grid.</param>
        /// <param name="gridHeight">The number of rows in the grid.</param>
        /// <param name="layout">The current grid layout parameters.</param>
        Vector2 GetGridPlaneSize(int gridWidth, int gridHeight, in Grid2DLayout layout);

        /// <summary>
        /// Fills a buffer with adjacent cell indices for the provided cell.
        /// </summary>
        /// <param name="x">Grid cell X index.</param>
        /// <param name="y">Grid cell Y index.</param>
        /// <param name="mode">The adjacency mode to use for neighbors.</param>
        /// <param name="neighborsBuffer">The buffer to write neighbor coordinates into.</param>
        int FillNeighborPositionsBuffer(int x, int y, GridAdjacencyMode2D mode, Vector2Int[] neighborsBuffer);

    }

    public enum GridGeometryType2D
    {
        Square,
        Hexagon,
        EquilateralTriangle
    }

    /// <summary>
    /// Immutable layout data used by Grid2D geometry implementations.
    /// </summary>
    public readonly struct Grid2DLayout
    {
        /// <summary>
        /// The size of a cell before spacing is applied.
        /// </summary>
        public readonly float CellSize;

        /// <summary>
        /// The distance between adjacent cells.
        /// </summary>
        public readonly float CellSpacing;

        /// <summary>
        /// The world-space origin for the grid.
        /// </summary>
        public readonly Vector3 OriginPosition;

        /// <summary>
        /// The plane used by the grid.
        /// </summary>
        public readonly GridLayoutType2D LayoutType;

        /// <summary>
        /// Creates an immutable snapshot of grid layout values.
        /// </summary>
        public Grid2DLayout(float cellSize, float cellSpacing, Vector3 originPosition, GridLayoutType2D layoutType)
        {
            CellSize = cellSize;
            CellSpacing = cellSpacing;
            OriginPosition = originPosition;
            LayoutType = layoutType;
        }

        /// <summary>
        /// Converts plane coordinates into world space according to the selected layout.
        /// </summary>
        public Vector3 ToWorldOnPlane(float u, float v, float depthOffset = 0f)
        {
            return LayoutType switch
            {
                GridLayoutType2D.Horizontal     => OriginPosition + new Vector3(u, depthOffset, v), // XZ, depth Y
                GridLayoutType2D.VerticalDepth  => OriginPosition + new Vector3(depthOffset, v, u), // ZY, depth X
                _                               => OriginPosition + new Vector3(u, v, depthOffset), // XY, depth Z
            };
        }

        /// <summary>
        /// Converts a world position back into the layout's plane coordinates.
        /// </summary>
        public void WorldToPlaneUV(Vector3 worldPosition, out float u, out float v)
        {
            Vector3 deltaPosition = worldPosition - OriginPosition;

            switch (LayoutType)
            {
                case GridLayoutType2D.Horizontal:
                    u = deltaPosition.x; v = deltaPosition.z;
                    break;

                case GridLayoutType2D.VerticalDepth:
                    u = deltaPosition.z; v = deltaPosition.y;
                    break;

                default:
                    u = deltaPosition.x; v = deltaPosition.y;
                    break;
            }
        }

        /// <summary>
        /// Returns the rotation that aligns a visual with the grid plane.
        /// </summary>
        public Quaternion GetFacingRotation()
        {
            return LayoutType switch
            {
                GridLayoutType2D.Horizontal => Quaternion.Euler(new Vector3(-90, 0, 0)),
                GridLayoutType2D.VerticalDepth => Quaternion.Euler(new Vector3(0, -90, 0)),
                _ => Quaternion.Euler(Vector3.zero),
            };
        }

        /// <summary>
        /// Returns the inverse of the grid plane facing rotation.
        /// </summary>
        public Quaternion GetInvertedFacingRotation() => Quaternion.Inverse(GetFacingRotation());

    }

    public readonly struct GridLineSegment
    {
        /// <summary>
        /// The segment start point.
        /// </summary>
        public readonly Vector3 A;

        /// <summary>
        /// The segment end point.
        /// </summary>
        public readonly Vector3 B;

        /// <summary>
        /// Creates a debug line segment between two points.
        /// </summary>
        public GridLineSegment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }

    }

    public readonly struct GridDebugSettings
    {
        /// <summary>
        /// Offset applied along the grid depth axis when drawing debug geometry.
        /// </summary>
        public readonly float DepthOffset;

        /// <summary>
        /// Whether to draw individual cell outlines.
        /// </summary>
        public readonly bool DrawCellOutlines;

        /// <summary>
        /// Whether geometry implementations should try to remove shared duplicate edges.
        /// </summary>
        public readonly bool TryDedupeSharedEdges;

        /// <summary>
        /// Creates debug drawing settings for grid visualizations.
        /// </summary>
        public GridDebugSettings(float depthOffset, bool drawCellOutlines = true, bool tryDedupeSharedEdges = false)
        {
            DepthOffset = depthOffset;
            DrawCellOutlines = drawCellOutlines;
            TryDedupeSharedEdges = tryDedupeSharedEdges;
        }

    }

}
