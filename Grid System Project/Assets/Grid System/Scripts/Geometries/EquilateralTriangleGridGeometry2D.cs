using System;
using System.Collections.Generic;
using UnityEngine;

namespace MerelyGames.Grids
{
    /// <summary>
    /// Geometry strategy for equilateral triangle grid layouts.
    /// </summary>
    /// <remarks>
    /// Supports both the rhombus lattice layout and stacked row layout modes.
    /// </remarks>
    public class EquilateralTriangleGridGeometry2D : ISpatialGridGeometry2D
    {
        /// <summary>
        /// Gets the number of corners in a triangle cell.
        /// </summary>
		public int CellCornerCount => 3;

        /// <summary>
        /// Gets the geometry shape represented by this strategy.
        /// </summary>
		public GridGeometryType2D GeometryType2D => GridGeometryType2D.EquilateralTriangle;

        /// <summary>
        /// Gets the triangle layout mode.
        /// </summary>
		public EquilateralTriangleLayout2D TriangleLayout => _triangleLayout;

        /// <summary>
        /// Gets the row flipping mode used by stacked-row layouts.
        /// </summary>
		public EquilateralTriangleStackedRowMode2D StackedRowMode => _stackedRowMode;

		private const float SQRT3 = 1.7320508075688772f;

		private readonly EquilateralTriangleLayout2D _triangleLayout;
		private readonly EquilateralTriangleStackedRowMode2D _stackedRowMode;

        /// <summary>
        /// Creates an equilateral triangle geometry strategy.
        /// </summary>
		public EquilateralTriangleGridGeometry2D(
			EquilateralTriangleLayout2D triangleLayout = EquilateralTriangleLayout2D.RhombusLattice,
			EquilateralTriangleStackedRowMode2D stackedRowMode = EquilateralTriangleStackedRowMode2D.EvenRowsFlipped)
		{
			_triangleLayout = triangleLayout;
			_stackedRowMode = stackedRowMode;
		}

        /// <summary>
        /// Converts triangle cell indices into the world-space centroid of the cell.
        /// </summary>
        public Vector3 CellToWorldCenter(int x, int y, in Grid2DLayout layout, float depthOffset = 0f)
        {
			GetTriangleCornersUV(x, y, in layout, out Vector2 cornerA, out Vector2 cornerB, out Vector2 cornerC);

			Vector2 centroid = (cornerA + cornerB + cornerC) / 3f;
			return layout.ToWorldOnPlane(centroid.x, centroid.y, depthOffset);
		}

        /// <summary>
        /// Converts a world position into equilateral triangle grid indices.
        /// </summary>
        public void WorldToCell(Vector3 worldPosition, in Grid2DLayout layout, out int x, out int y)
        {
			if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
			{
				WorldToCellStackedRows(worldPosition, in layout, out x, out y);
				return;
			}

			WorldToCellRhombusLattice(worldPosition, in layout, out x, out y);
		}

        /// <summary>
        /// Writes the world-space corners of a triangle cell into the provided buffer.
        /// </summary>
        public void GetCellCornersWorld(int x, int y, in Grid2DLayout layout, Vector3[] cornersBuffer, float depthOffset = 0f)
        {
			GetTriangleCornersUV(x, y, in layout, out Vector2 cornerA, out Vector2 cornerB, out Vector2 cornerC);

			cornersBuffer[0] = layout.ToWorldOnPlane(cornerA.x, cornerA.y, depthOffset);
			cornersBuffer[1] = layout.ToWorldOnPlane(cornerB.x, cornerB.y, depthOffset);
			cornersBuffer[2] = layout.ToWorldOnPlane(cornerC.x, cornerC.y, depthOffset);
		}

        /// <summary>
        /// Appends debug segments for triangle cell outlines.
        /// </summary>
        public void AppendDebugLineSegments(int gridWidth, int gridHeight, in Grid2DLayout layout, in GridDebugSettings settings, List<GridLineSegment> segments)
        {
			if (!settings.DrawCellOutlines)
				return;

			if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
			{
				AppendDebugLineSegmentsByCell(gridWidth, gridHeight, in layout, in settings, segments);
				return;
			}

			AppendDebugLineSegmentsRhombusLattice(gridWidth, gridHeight, in layout, in settings, segments);
		}

        /// <summary>
        /// Computes the world-space bounds that contain the triangle grid.
        /// </summary>
        public void GetGridWorldBounds(int gridWidth, int gridHeight, in Grid2DLayout layout, out Vector3 minBounds, out Vector3 maxBounds, float depthOffset = 0f)
        {
			if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
			{
				GetGridWorldBoundsFromCells(gridWidth, gridHeight, in layout, out minBounds, out maxBounds, depthOffset);
				return;
			}

			GetGridWorldBoundsRhombusLattice(gridWidth, gridHeight, in layout, out minBounds, out maxBounds, depthOffset);
		}

        /// <summary>
        /// Returns the plane size of the triangle grid for the current layout.
        /// </summary>
        public Vector2 GetGridPlaneSize(int gridWidth, int gridHeight, in Grid2DLayout layout)
        {
			if (gridWidth <= 0 || gridHeight <= 0)
				return Vector2.zero;

			if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
				return GetGridPlaneSizeStackedRows(gridWidth, gridHeight, in layout);

			return GetGridPlaneSizeRhombusLattice(gridWidth, gridHeight, in layout);
		}

        /// <summary>
        /// Fills the neighbor buffer for the specified triangle cell.
        /// </summary>
        public int FillNeighborPositionsBuffer(int x, int y, GridAdjacencyMode2D mode, Vector2Int[] neighborsBuffer)
        {
            if (neighborsBuffer == null)
				throw new ArgumentNullException(nameof(neighborsBuffer));

			if (mode == GridAdjacencyMode2D.EdgeNeighborsOnly)
			{
				if (neighborsBuffer.Length < 3)
					throw new ArgumentException("neighborsBuffer length must be >= 3.");

				if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
					return FillNeighborPositionsBufferStackedRows(x, y, neighborsBuffer);

				return FillNeighborPositionsBufferRhombusLattice(x, y, neighborsBuffer);
			}

			if (neighborsBuffer.Length < 12)
				throw new ArgumentException("neighborsBuffer length must be >= 12.");

			if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
				return FillVertexNeighborPositionsBufferStackedRows(x, y, neighborsBuffer);

			return FillVertexNeighborPositionsBufferRhombusLattice(x, y, neighborsBuffer);
		}

        /// <summary>
        /// Determines whether the given triangle cell is oriented up or down.
        /// </summary>
        public EquilateralTriangleOrientation2D GetTriangleOrientation(int x, int y, in Grid2DLayout layout)
        {
			GetTriangleCornersUV(x, y, in layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2);

			float averageY = (corner0.y + corner1.y + corner2.y) / 3f;

			float highestY = Mathf.Max(corner0.y, corner1.y, corner2.y);
			float lowestY = Mathf.Min(corner0.y, corner1.y, corner2.y);

			float distanceToHighest = highestY - averageY;
			float distanceToLowest = averageY - lowestY;

			return distanceToHighest > distanceToLowest
				? EquilateralTriangleOrientation2D.Up
				: EquilateralTriangleOrientation2D.Down;
		}

        /// <summary>
        /// Gets a triangle cell's corners in layout plane coordinates.
        /// </summary>
		private void GetTriangleCornersUV(int x, int y, in Grid2DLayout layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2)
		{
			if (_triangleLayout == EquilateralTriangleLayout2D.StackedRows)
			{
				GetTriangleCornersUVStackedRows(x, y, in layout, out corner0, out corner1, out corner2);
				return;
			}

			GetTriangleCornersUVRhombusLattice(x, y, in layout, out corner0, out corner1, out corner2);
		}

        /// <summary>
        /// Gets corners for a rhombus-lattice triangle cell in layout plane coordinates.
        /// </summary>
		private static void GetTriangleCornersUVRhombusLattice(int x, int y, in Grid2DLayout layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2)
		{
			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			float h = side * (SQRT3 * 0.5f);

			int rhX = x >> 1;
			int parity = x & 1;

			float u0 = (rhX * stepSide) + (y * stepSide * 0.5f);
			float v0 = y * stepH;

			float uFar = u0 + stepSide + (stepSide * 0.5f);
			float vFar = v0 + stepH;

			if (parity == 0)
			{
				corner0 = new Vector2(u0, v0);
				corner1 = new Vector2(u0 + side, v0);
				corner2 = new Vector2(u0 + (side * 0.5f), v0 + h);
			}
			else
			{
				Vector2 far = new Vector2(uFar, vFar);

				corner0 = far;
				corner1 = new Vector2(far.x - side, far.y);
				corner2 = new Vector2(far.x - (side * 0.5f), far.y - h);
			}
		}

        /// <summary>
        /// Gets corners for a stacked-row triangle cell in layout plane coordinates.
        /// </summary>
		private void GetTriangleCornersUVStackedRows(int x, int y, in Grid2DLayout layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2)
		{
			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			float triangleHeight = side * (SQRT3 * 0.5f);

			float u0 = x * stepSide * 0.5f;
			float v0 = y * stepH;

			if (IsUpwardTriangleStackedRows(x, y))
			{
				corner0 = new Vector2(u0, v0);
				corner1 = new Vector2(u0 + side, v0);
				corner2 = new Vector2(u0 + (side * 0.5f), v0 + triangleHeight);
			}
			else
			{
				corner0 = new Vector2(u0, v0 + triangleHeight);
				corner1 = new Vector2(u0 + side, v0 + triangleHeight);
				corner2 = new Vector2(u0 + (side * 0.5f), v0);
			}
		}

        /// <summary>
        /// Maps a world position into rhombus-lattice triangle coordinates.
        /// </summary>
		private static void WorldToCellRhombusLattice(Vector3 worldPosition, in Grid2DLayout layout, out int x, out int y)
		{
			layout.WorldToPlaneUV(worldPosition, out float u, out float v);

			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			float beta = v / stepH;
			int rhY = Mathf.FloorToInt(beta);
			float localBeta = beta - rhY;

			float alpha = (u - (beta * stepSide * 0.5f)) / stepSide;
			int rhX = Mathf.FloorToInt(alpha);
			float localAlpha = alpha - rhX;

			bool isUpperRightTriangle = localAlpha + localBeta > 1f;

			x = (rhX * 2) + (isUpperRightTriangle ? 1 : 0);
			y = rhY;
		}

        /// <summary>
        /// Maps a world position into stacked-row triangle coordinates.
        /// </summary>
		private void WorldToCellStackedRows(Vector3 worldPosition, in Grid2DLayout layout, out int x, out int y)
		{
			layout.WorldToPlaneUV(worldPosition, out float u, out float v);

			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			int estimatedX = Mathf.FloorToInt(u / (stepSide * 0.5f));
			int estimatedY = Mathf.FloorToInt(v / stepH);

			Vector2 point = new Vector2(u, v);

			for (int candidateY = estimatedY - 1; candidateY <= estimatedY + 1; candidateY++)
			{
				for (int candidateX = estimatedX - 2; candidateX <= estimatedX + 2; candidateX++)
				{
					GetTriangleCornersUVStackedRows(candidateX, candidateY, in layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2);

					if (IsPointInsideTriangle(point, corner0, corner1, corner2))
					{
						x = candidateX;
						y = candidateY;
						return;
					}
				}
			}

			// Fallback for spacing gaps or edge precision: choose the nearest tested triangle centroid.
			float closestDistanceSqr = float.PositiveInfinity;
			int closestX = estimatedX;
			int closestY = estimatedY;

			for (int candidateY = estimatedY - 1; candidateY <= estimatedY + 1; candidateY++)
			{
				for (int candidateX = estimatedX - 2; candidateX <= estimatedX + 2; candidateX++)
				{
					GetTriangleCornersUVStackedRows(candidateX, candidateY, in layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2);

					Vector2 centroid = (corner0 + corner1 + corner2) / 3f;
					float distanceSqr = (point - centroid).sqrMagnitude;

					if (distanceSqr < closestDistanceSqr)
					{
						closestDistanceSqr = distanceSqr;
						closestX = candidateX;
						closestY = candidateY;
					}
				}
			}

			x = closestX;
			y = closestY;
		}

        /// <summary>
        /// Fills edge neighbors for a rhombus-lattice triangle cell.
        /// </summary>
		private static int FillNeighborPositionsBufferRhombusLattice(int x, int y, Vector2Int[] neighborsBuffer)
		{
			int parity = x & 1;

			if (parity == 0)
			{
				neighborsBuffer[0] = new Vector2Int(x ^ 1, y);
				neighborsBuffer[1] = new Vector2Int(x - 1, y);
				neighborsBuffer[2] = new Vector2Int(x + 1, y - 1);
				return 3;
			}

			neighborsBuffer[0] = new Vector2Int(x ^ 1, y);
			neighborsBuffer[1] = new Vector2Int(x + 1, y);
			neighborsBuffer[2] = new Vector2Int(x - 1, y + 1);
			return 3;
		}

        /// <summary>
        /// Fills edge neighbors for a stacked-row triangle cell.
        /// </summary>
		private int FillNeighborPositionsBufferStackedRows(int x, int y, Vector2Int[] neighborsBuffer)
		{
			neighborsBuffer[0] = new Vector2Int(x - 1, y);
			neighborsBuffer[1] = new Vector2Int(x + 1, y);

			if (IsUpwardTriangleStackedRows(x, y))
				neighborsBuffer[2] = new Vector2Int(x, y - 1);
			else
				neighborsBuffer[2] = new Vector2Int(x, y + 1);

			return 3;
		}

        /// <summary>
        /// Returns true when a stacked-row triangle cell points upward.
        /// </summary>
		private bool IsUpwardTriangleStackedRows(int x, int y)
		{
			bool defaultOrientation = (x & 1) == 0;
			bool isEvenRow = (y & 1) == 0;

			bool shouldFlipRow = _stackedRowMode == EquilateralTriangleStackedRowMode2D.EvenRowsFlipped
				? isEvenRow
				: !isEvenRow;

			return shouldFlipRow
				? !defaultOrientation
				: defaultOrientation;
		}

        /// <summary>
        /// Returns true when a point lies inside or on the edge of a triangle.
        /// </summary>
		private static bool IsPointInsideTriangle(Vector2 point, Vector2 cornerA, Vector2 cornerB, Vector2 cornerC)
		{
			float signA = GetTriangleSign(point, cornerA, cornerB);
			float signB = GetTriangleSign(point, cornerB, cornerC);
			float signC = GetTriangleSign(point, cornerC, cornerA);

			bool hasNegative = signA < 0f || signB < 0f || signC < 0f;
			bool hasPositive = signA > 0f || signB > 0f || signC > 0f;

			return !(hasNegative && hasPositive);
		}

        /// <summary>
        /// Computes the signed area helper used for point-in-triangle tests.
        /// </summary>
		private static float GetTriangleSign(Vector2 pointA, Vector2 pointB, Vector2 pointC)
		{
			return ((pointA.x - pointC.x) * (pointB.y - pointC.y)) - ((pointB.x - pointC.x) * (pointA.y - pointC.y));
		}

        /// <summary>
        /// Appends per-cell debug outlines for stacked-row triangle grids.
        /// </summary>
		private void AppendDebugLineSegmentsByCell(int gridWidth, int gridHeight, in Grid2DLayout layout, in GridDebugSettings settings, List<GridLineSegment> segments)
		{
			float depth = settings.DepthOffset;

			for (int x = 0; x < gridWidth; x++)
			{
				for (int y = 0; y < gridHeight; y++)
				{
					GetTriangleCornersUVStackedRows(x, y, in layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2);

					Vector3 cornerA = layout.ToWorldOnPlane(corner0.x, corner0.y, depth);
					Vector3 cornerB = layout.ToWorldOnPlane(corner1.x, corner1.y, depth);
					Vector3 cornerC = layout.ToWorldOnPlane(corner2.x, corner2.y, depth);

					segments.Add(new GridLineSegment(cornerA, cornerB));
					segments.Add(new GridLineSegment(cornerB, cornerC));
					segments.Add(new GridLineSegment(cornerC, cornerA));
				}
			}
		}

        /// <summary>
        /// Appends lattice debug segments for rhombus-lattice triangle grids.
        /// </summary>
		private static void AppendDebugLineSegmentsRhombusLattice(int gridWidth, int gridHeight, in Grid2DLayout layout, in GridDebugSettings settings, List<GridLineSegment> segments)
		{
			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			float depth = settings.DepthOffset;

			int rhWidth = (gridWidth + 1) / 2;

			Grid2DLayout localLayout = layout;

			Vector3 LatticePoint(int rhX, int rhY)
			{
				float u = (rhX * stepSide) + (rhY * stepSide * 0.5f);
				float v = rhY * stepH;
				return localLayout.ToWorldOnPlane(u, v, depth);
			}

			for (int x0 = 0; x0 <= rhWidth; x0++)
			{
				segments.Add(new GridLineSegment(
					LatticePoint(x0, 0),
					LatticePoint(x0, gridHeight)));
			}

			for (int y0 = 0; y0 <= gridHeight; y0++)
			{
				segments.Add(new GridLineSegment(
					LatticePoint(0, y0),
					LatticePoint(rhWidth, y0)));
			}

			for (int rx = 0; rx < rhWidth; rx++)
			{
				for (int ry = 0; ry < gridHeight; ry++)
				{
					segments.Add(new GridLineSegment(
						LatticePoint(rx + 1, ry),
						LatticePoint(rx, ry + 1)));
				}
			}
		}

        /// <summary>
        /// Computes grid bounds by inspecting each stacked-row triangle cell.
        /// </summary>
		private void GetGridWorldBoundsFromCells(int gridWidth, int gridHeight, in Grid2DLayout layout, out Vector3 minBounds, out Vector3 maxBounds, float depthOffset = 0f)
		{
			if (gridWidth <= 0 || gridHeight <= 0)
			{
				Vector3 origin = layout.ToWorldOnPlane(0f, 0f, depthOffset);
				minBounds = origin;
				maxBounds = origin;
				return;
			}

			bool hasBounds = false;
			minBounds = Vector3.zero;
			maxBounds = Vector3.zero;

			for (int x = 0; x < gridWidth; x++)
			{
				for (int y = 0; y < gridHeight; y++)
				{
					GetTriangleCornersUVStackedRows(x, y, in layout, out Vector2 corner0, out Vector2 corner1, out Vector2 corner2);

					IncludeBoundsPoint(corner0, in layout, ref hasBounds, ref minBounds, ref maxBounds, depthOffset);
					IncludeBoundsPoint(corner1, in layout, ref hasBounds, ref minBounds, ref maxBounds, depthOffset);
					IncludeBoundsPoint(corner2, in layout, ref hasBounds, ref minBounds, ref maxBounds, depthOffset);
				}
			}
		}

        /// <summary>
        /// Expands bounds so they include a point from the layout plane.
        /// </summary>
		private static void IncludeBoundsPoint(Vector2 point, in Grid2DLayout layout, ref bool hasBounds, ref Vector3 minBounds, ref Vector3 maxBounds, float depthOffset)
		{
			Vector3 worldPoint = layout.ToWorldOnPlane(point.x, point.y, depthOffset);

			if (!hasBounds)
			{
				minBounds = worldPoint;
				maxBounds = worldPoint;
				hasBounds = true;
				return;
			}

			minBounds = Vector3.Min(minBounds, worldPoint);
			maxBounds = Vector3.Max(maxBounds, worldPoint);
		}

        /// <summary>
        /// Computes grid bounds for the rhombus-lattice triangle layout.
        /// </summary>
		private static void GetGridWorldBoundsRhombusLattice(int gridWidth, int gridHeight, in Grid2DLayout layout, out Vector3 minBounds, out Vector3 maxBounds, float depthOffset = 0f)
		{
			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			int rhWidth = (gridWidth + 1) / 2;

			Vector2 p00 = new Vector2(0f, 0f);
			Vector2 basisA = new Vector2(rhWidth * stepSide, 0f);
			Vector2 basisB = new Vector2(gridHeight * stepSide * 0.5f, gridHeight * stepH);
			Vector2 combinedBasis = basisA + basisB;

			float h = side * (SQRT3 * 0.5f);
			Vector2 expand = new Vector2(side, h);

			Vector3 boundsCorner0 = layout.ToWorldOnPlane(p00.x - expand.x, p00.y - expand.y, depthOffset);
			Vector3 boundsCorner1 = layout.ToWorldOnPlane(basisA.x + expand.x, basisA.y - expand.y, depthOffset);
			Vector3 boundsCorner2 = layout.ToWorldOnPlane(basisB.x - expand.x, basisB.y + expand.y, depthOffset);
			Vector3 boundsCorner3 = layout.ToWorldOnPlane(combinedBasis.x + expand.x, combinedBasis.y + expand.y, depthOffset);

			minBounds = Vector3.Min(Vector3.Min(boundsCorner0, boundsCorner1), Vector3.Min(boundsCorner2, boundsCorner3));
			maxBounds = Vector3.Max(Vector3.Max(boundsCorner0, boundsCorner1), Vector3.Max(boundsCorner2, boundsCorner3));
		}

        /// <summary>
        /// Returns the plane size for a stacked-row triangle grid.
        /// </summary>
		private static Vector2 GetGridPlaneSizeStackedRows(int gridWidth, int gridHeight, in Grid2DLayout layout)
		{
			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			float width = ((gridWidth - 1) * stepSide * 0.5f) + side;
			float height = ((gridHeight - 1) * stepH) + (side * (SQRT3 * 0.5f));

			return new Vector2(width, height);
		}

        /// <summary>
        /// Returns the plane size for a rhombus-lattice triangle grid.
        /// </summary>
		private static Vector2 GetGridPlaneSizeRhombusLattice(int gridWidth, int gridHeight, in Grid2DLayout layout)
		{
			float side = layout.CellSize;
			float spacing = layout.CellSpacing;

			float stepSide = side + spacing;
			float stepH = stepSide * (SQRT3 * 0.5f);

			int rhWidth = (gridWidth + 1) / 2;

			float uMax = (rhWidth * stepSide) + (gridHeight * stepSide * 0.5f);
			float vMax = gridHeight * stepH;

			return new Vector2(uMax, vMax);
		}

        /// <summary>
        /// Fills edge and vertex neighbors for a rhombus-lattice triangle cell.
        /// </summary>
		private static int FillVertexNeighborPositionsBufferRhombusLattice(int x, int y, Vector2Int[] neighborsBuffer)
		{
			int parity = x & 1;

			if (parity == 0)
			{
				neighborsBuffer[0] = new Vector2Int(x ^ 1, y);
				neighborsBuffer[1] = new Vector2Int(x - 1, y);
				neighborsBuffer[2] = new Vector2Int(x + 1, y - 1);

				neighborsBuffer[3] = new Vector2Int(x - 1, y - 1);
				neighborsBuffer[4] = new Vector2Int(x, y - 1);
				neighborsBuffer[5] = new Vector2Int(x + 2, y - 1);
				neighborsBuffer[6] = new Vector2Int(x + 3, y - 1);
				neighborsBuffer[7] = new Vector2Int(x - 2, y);
				neighborsBuffer[8] = new Vector2Int(x + 2, y);
				neighborsBuffer[9] = new Vector2Int(x - 2, y + 1);
				neighborsBuffer[10] = new Vector2Int(x - 1, y + 1);
				neighborsBuffer[11] = new Vector2Int(x, y + 1);

				return 12;
			}

			neighborsBuffer[0] = new Vector2Int(x ^ 1, y);
			neighborsBuffer[1] = new Vector2Int(x + 1, y);
			neighborsBuffer[2] = new Vector2Int(x - 1, y + 1);

			neighborsBuffer[3] = new Vector2Int(x, y - 1);
			neighborsBuffer[4] = new Vector2Int(x + 1, y - 1);
			neighborsBuffer[5] = new Vector2Int(x + 2, y - 1);
			neighborsBuffer[6] = new Vector2Int(x - 2, y);
			neighborsBuffer[7] = new Vector2Int(x + 2, y);
			neighborsBuffer[8] = new Vector2Int(x - 3, y + 1);
			neighborsBuffer[9] = new Vector2Int(x - 2, y + 1);
			neighborsBuffer[10] = new Vector2Int(x, y + 1);
			neighborsBuffer[11] = new Vector2Int(x + 1, y + 1);

			return 12;
		}

        /// <summary>
        /// Fills edge and vertex neighbors for a stacked-row triangle cell.
        /// </summary>
		private int FillVertexNeighborPositionsBufferStackedRows(int x, int y, Vector2Int[] neighborsBuffer)
		{
			if (IsUpwardTriangleStackedRows(x, y))
			{
				neighborsBuffer[0] = new Vector2Int(x - 1, y);
				neighborsBuffer[1] = new Vector2Int(x + 1, y);
				neighborsBuffer[2] = new Vector2Int(x, y - 1);

				neighborsBuffer[3] = new Vector2Int(x - 2, y - 1);
				neighborsBuffer[4] = new Vector2Int(x - 1, y - 1);
				neighborsBuffer[5] = new Vector2Int(x + 1, y - 1);
				neighborsBuffer[6] = new Vector2Int(x + 2, y - 1);
				neighborsBuffer[7] = new Vector2Int(x - 2, y);
				neighborsBuffer[8] = new Vector2Int(x + 2, y);
				neighborsBuffer[9] = new Vector2Int(x - 1, y + 1);
				neighborsBuffer[10] = new Vector2Int(x, y + 1);
				neighborsBuffer[11] = new Vector2Int(x + 1, y + 1);

				return 12;
			}

			neighborsBuffer[0] = new Vector2Int(x - 1, y);
			neighborsBuffer[1] = new Vector2Int(x + 1, y);
			neighborsBuffer[2] = new Vector2Int(x, y + 1);

			neighborsBuffer[3] = new Vector2Int(x - 1, y - 1);
			neighborsBuffer[4] = new Vector2Int(x, y - 1);
			neighborsBuffer[5] = new Vector2Int(x + 1, y - 1);
			neighborsBuffer[6] = new Vector2Int(x - 2, y);
			neighborsBuffer[7] = new Vector2Int(x + 2, y);
			neighborsBuffer[8] = new Vector2Int(x - 2, y + 1);
			neighborsBuffer[9] = new Vector2Int(x - 1, y + 1);
			neighborsBuffer[10] = new Vector2Int(x + 1, y + 1);
			neighborsBuffer[11] = new Vector2Int(x + 2, y + 1);

			return 12;
		}


	}

	public enum EquilateralTriangleLayout2D
	{
		RhombusLattice,
		StackedRows
	}

	public enum EquilateralTriangleStackedRowMode2D
	{
		EvenRowsFlipped,
		OddRowsFlipped
	}

	public enum EquilateralTriangleOrientation2D
	{
		Up,
		Down
	}
}
