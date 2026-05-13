using System;
using System.Collections.Generic;
using UnityEngine;

namespace MerelyGames.Grids
{
    /// <summary>
    /// Defines the rotation style used by hexagonal cells.
    /// </summary>
    public enum HexOrientation { PointyTop, FlatTop }

    /// <summary>
    /// Defines which offset rows or columns are shifted in offset coordinates.
    /// </summary>
    public enum HexOffsetParity { Even, Odd }

    /// <summary>
    /// Geometry strategy for hexagonal grids using offset coordinates.
    /// </summary>
    public sealed class HexGridGeometry2D : ISpatialGridGeometry2D
    {
        /// <summary>
        /// Gets the number of corners in a hex cell.
        /// </summary>
        public int CellCornerCount => 6;

        /// <summary>
        /// Gets the geometry shape represented by this strategy.
        /// </summary>
        public GridGeometryType2D GeometryType2D => GridGeometryType2D.Hexagon;

        private readonly HexOrientation _orientation;
        private readonly HexOffsetParity _offsetParity;

        /// <summary>
        /// Creates a hex grid geometry strategy with orientation and offset parity settings.
        /// </summary>
        public HexGridGeometry2D(HexOrientation orientation = HexOrientation.PointyTop, HexOffsetParity offsetParity = HexOffsetParity.Odd)
        {
            _orientation = orientation;
            _offsetParity = offsetParity;
        }

        /// <summary>
        /// Converts hexagonal cell indices into the world-space center point.
        /// </summary>
        public Vector3 CellToWorldCenter(int x, int y, in Grid2DLayout layout, float depthOffset = 0f)
        {
            float spacingRadius = layout.CellSize + layout.CellSpacing;

			if (_orientation == HexOrientation.FlatTop)
			{
				OffsetToPlaneUV(x, y, spacingRadius, out float u, out float v);
				return layout.ToWorldOnPlane(u, v, depthOffset);
			}

			OffsetToAxial(x, y, out int axialQ, out int axialR);
			AxialToPlaneUV(axialQ, axialR, spacingRadius, out float axialU, out float axialV);

			return layout.ToWorldOnPlane(axialU, axialV, depthOffset);
        }

        /// <summary>
        /// Converts a world position into hexagonal grid indices.
        /// </summary>
        public void WorldToCell(Vector3 worldPosition, in Grid2DLayout layout, out int x, out int y)
        {
            layout.WorldToPlaneUV(worldPosition, out float u, out float v);

			float spacingRadius = layout.CellSize + layout.CellSpacing;

			if (_orientation == HexOrientation.FlatTop)
			{
				PlaneUVToOffset(u, v, spacingRadius, out x, out y);
				return;
			}

			PlaneUVToAxialFractional(u, v, spacingRadius, out float fractionalQ, out float fractionalR);

			AxialRound(fractionalQ, fractionalR, out int roundedQ, out int roundedR);

			AxialToOffset(roundedQ, roundedR, out x, out y);
        }

        /// <summary>
        /// Writes the world-space corner positions for a hex cell into the provided buffer.
        /// </summary>
        public void GetCellCornersWorld(int x, int y, in Grid2DLayout layout, Vector3[] cornersBuffer, float depthOffset = 0f)
        {
            // Corners should use the true radius (CellSize), not including spacing.
            Vector3 center = CellToWorldCenter(x, y, layout, depthOffset);

            layout.WorldToPlaneUV(center, out float cu, out float cv);

            float radius = layout.CellSize;
            float angleOffsetDeg = _orientation == HexOrientation.PointyTop ? 30f : 0f;

            for (int i = 0; i < 6; i++)
            {
                float angleDeg = angleOffsetDeg + 60f * i;
                float angleRadians = angleDeg * Mathf.Deg2Rad;

                float u = cu + radius * Mathf.Cos(angleRadians);
                float v = cv + radius * Mathf.Sin(angleRadians);

                cornersBuffer[i] = layout.ToWorldOnPlane(u, v, depthOffset);
            }
        }

        /// <summary>
        /// Appends debug line segments for hex cell outlines.
        /// </summary>
        public void AppendDebugLineSegments(int gridWidth, int gridHeight, in Grid2DLayout layout, in GridDebugSettings settings, List<GridLineSegment> segments)
        {
            if (!settings.DrawCellOutlines)
                return;

            // Robust, geometry-correct visualization: outline every cell polygon.
            // (Optional dedupe later.)
            Vector3[] corners = new Vector3[6];

            for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                GetCellCornersWorld(x, y, layout, corners, settings.DepthOffset);

                for (int i = 0; i < 6; i++)
                {
                    Vector3 segmentStart = corners[i];
                    Vector3 segmentEnd = corners[(i + 1) % 6];
                    segments.Add(new GridLineSegment(segmentStart, segmentEnd));
                }
            }
        }

        #region Offset/Axial

        /// <summary>
        /// Converts offset coordinates to axial coordinates for pointy-top mapping.
        /// </summary>
        private void OffsetToAxial(int column, int row, out int axialQ, out int axialR)
		{
			axialR = row;

			if (_offsetParity == HexOffsetParity.Odd)
				axialQ = column - ((row - (row & 1)) / 2);
			else
				axialQ = column - ((row + (row & 1)) / 2);
		}

        /// <summary>
        /// Converts axial coordinates back to offset coordinates.
        /// </summary>
        private void AxialToOffset(int axialQ, int axialR, out int column, out int row)
		{
			row = axialR;

			if (_offsetParity == HexOffsetParity.Odd)
				column = axialQ + ((axialR - (axialR & 1)) / 2);
			else
				column = axialQ + ((axialR + (axialR & 1)) / 2);
		}

        /// <summary>
        /// Converts axial coordinates into layout plane coordinates.
        /// </summary>
        private void AxialToPlaneUV(int axialQ, int axialR, float size, out float u, out float v)
        {
            if (_orientation == HexOrientation.PointyTop)
            {
                u = size * Mathf.Sqrt(3f) * (axialQ + axialR * 0.5f);
                v = size * 1.5f * axialR;
            }
            else
            {
                u = size * 1.5f * axialQ;
                v = size * Mathf.Sqrt(3f) * (axialR + axialQ * 0.5f);
            }
        }

        /// <summary>
        /// Converts layout plane coordinates into fractional axial coordinates before rounding.
        /// </summary>
        private void PlaneUVToAxialFractional(float u, float v, float size, out float axialQ, out float axialR)
        {
            float inverseSize = 1f / size;

            if (_orientation == HexOrientation.PointyTop)
            {
                axialR = (2f / 3f) * v * inverseSize;
                axialQ = (u * inverseSize) / Mathf.Sqrt(3f) - (axialR * 0.5f);
            }
            else
            {
                axialQ = (2f / 3f) * u * inverseSize;
                axialR = (v * inverseSize) / Mathf.Sqrt(3f) - (axialQ * 0.5f);
            }
        }

        /// <summary>
        /// Rounds fractional axial coordinates to the nearest valid hex coordinate.
        /// </summary>
        private static void AxialRound(float axialQ, float axialR, out int roundedQ, out int roundedR)
        {
            float cubeX = axialQ;
            float cubeZ = axialR;
            float cubeY = -cubeX - cubeZ;

            int roundedX = Mathf.RoundToInt(cubeX);
            int roundedY = Mathf.RoundToInt(cubeY);
            int roundedZ = Mathf.RoundToInt(cubeZ);

            float xDiff = Mathf.Abs(roundedX - cubeX);
            float yDiff = Mathf.Abs(roundedY - cubeY);
            float zDiff = Mathf.Abs(roundedZ - cubeZ);

            // Preserve the cube-coordinate invariant x + y + z = 0 after rounding.
            if (xDiff > yDiff && xDiff > zDiff)
                roundedX = -roundedY - roundedZ;
            else if (yDiff > zDiff)
                roundedY = -roundedX - roundedZ;
            else
                roundedZ = -roundedX - roundedY;

            roundedQ = roundedX;
            roundedR = roundedZ;
        }

        /// <summary>
        /// Computes bounds that contain all hex cells in the grid.
        /// </summary>
        public void GetGridWorldBounds(int gridWidth, int gridHeight, in Grid2DLayout layout, out Vector3 minBounds, out Vector3 maxBounds, float depthOffset = 0)
        {
			// Work on locals so nested helpers don't capture in/out parameters.
			Grid2DLayout layoutLocal = layout;

			Vector3 minLocal = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			Vector3 maxLocal = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

			Vector3[] corners = new Vector3[CellCornerCount];

			void EncapsulateCell(int x, int y)
			{
				GetCellCornersWorld(x, y, layoutLocal, corners, depthOffset);

				for (int i = 0; i < corners.Length; i++)
				{
					Vector3 corner = corners[i];
					minLocal = Vector3.Min(minLocal, corner);
					maxLocal = Vector3.Max(maxLocal, corner);
				}
			}

			if (gridWidth <= 0 || gridHeight <= 0)
			{
				// Define an empty-ish AABB at origin for degenerate grids.
				Vector3 origin = layoutLocal.OriginPosition; // Or however your layout exposes origin.
				minBounds = origin;
				maxBounds = origin;
				return;
			}

			int maxX = gridWidth - 1;
			int maxY = gridHeight - 1;

			// Left & right edges
			for (int y = 0; y < gridHeight; y++)
			{
				EncapsulateCell(0, y);
				EncapsulateCell(maxX, y);
			}

			// Bottom & top edges
			for (int x = 0; x < gridWidth; x++)
			{
				EncapsulateCell(x, 0);
				EncapsulateCell(x, maxY);
			}

			minBounds = minLocal;
			maxBounds = maxLocal;
		}

        /// <summary>
        /// Returns the plane size of the hex grid.
        /// </summary>
        public Vector2 GetGridPlaneSize(int gridWidth, int gridHeight, in Grid2DLayout layout)
		{
			if (gridWidth <= 0 || gridHeight <= 0)
				return Vector2.zero;

			// Avoid capturing the 'in' parameter inside a nested method.
			Grid2DLayout layoutLocal = layout;

			float minU = float.PositiveInfinity;
			float maxU = float.NegativeInfinity;
			float minV = float.PositiveInfinity;
			float maxV = float.NegativeInfinity;

			Vector3[] corners = new Vector3[CellCornerCount];

			void EncapsulateCell(int x, int y)
			{
				GetCellCornersWorld(x, y, layoutLocal, corners);

				for (int i = 0; i < corners.Length; i++)
				{
					// If you already have this helper on the layout, use it:
					layoutLocal.WorldToPlaneUV(corners[i], out float u, out float v);

					if (u < minU) minU = u;
					if (u > maxU) maxU = u;

					if (v < minV) minV = v;
					if (v > maxV) maxV = v;
				}
			}

			int maxX = gridWidth - 1;
			int maxY = gridHeight - 1;

			for (int y = 0; y < gridHeight; y++)
			{
				EncapsulateCell(0, y);
				EncapsulateCell(maxX, y);
			}

			for (int x = 0; x < gridWidth; x++)
			{
				EncapsulateCell(x, 0);
				EncapsulateCell(x, maxY);
			}

			return new Vector2(maxU - minU, maxV - minV);
		}

        /// <summary>
        /// Fills the neighbor buffer with adjacent hex cell positions.
        /// </summary>
        public int FillNeighborPositionsBuffer(int x, int y, GridAdjacencyMode2D mode, Vector2Int[] neighborsBuffer)
        {
            if (neighborsBuffer == null) throw new ArgumentNullException(nameof(neighborsBuffer));
			if (neighborsBuffer.Length < 6) throw new ArgumentException("neighborsBuffer length must be >= 6.");

			if (_orientation == HexOrientation.FlatTop)
			{
				bool isOffsetColumn = IsOffsetColumn(x);

				neighborsBuffer[0] = new Vector2Int(x, y + 1); // top

				if (isOffsetColumn)
				{
					neighborsBuffer[1] = new Vector2Int(x + 1, y + 1); // top-right
					neighborsBuffer[2] = new Vector2Int(x + 1, y);     // bottom-right
					neighborsBuffer[3] = new Vector2Int(x, y - 1);     // bottom
					neighborsBuffer[4] = new Vector2Int(x - 1, y);     // bottom-left
					neighborsBuffer[5] = new Vector2Int(x - 1, y + 1); // top-left
				}
				else
				{
					neighborsBuffer[1] = new Vector2Int(x + 1, y);     // top-right
					neighborsBuffer[2] = new Vector2Int(x + 1, y - 1); // bottom-right
					neighborsBuffer[3] = new Vector2Int(x, y - 1);     // bottom
					neighborsBuffer[4] = new Vector2Int(x - 1, y - 1); // bottom-left
					neighborsBuffer[5] = new Vector2Int(x - 1, y);     // top-left
				}

				return 6;
			}

			bool isOffsetRow = IsOffsetRow(y);

			neighborsBuffer[0] = new Vector2Int(x, y + 1); // top

			if (isOffsetRow)
			{
				neighborsBuffer[1] = new Vector2Int(x + 1, y + 1); // top-right
				neighborsBuffer[2] = new Vector2Int(x + 1, y);     // bottom-right
				neighborsBuffer[3] = new Vector2Int(x + 1, y - 1); // bottom
				neighborsBuffer[4] = new Vector2Int(x, y - 1);     // bottom-left
				neighborsBuffer[5] = new Vector2Int(x - 1, y);     // top-left
			}
			else
			{
				neighborsBuffer[1] = new Vector2Int(x + 1, y);     // top-right
				neighborsBuffer[2] = new Vector2Int(x, y - 1);     // bottom-right
				neighborsBuffer[3] = new Vector2Int(x - 1, y - 1); // bottom
				neighborsBuffer[4] = new Vector2Int(x - 1, y);     // bottom-left
				neighborsBuffer[5] = new Vector2Int(x - 1, y + 1); // top-left
			}

			return 6;
		}
		
        /// <summary>
        /// Converts flat-top offset coordinates into layout plane coordinates.
        /// </summary>
		private void OffsetToPlaneUV(int column, int row, float size, out float u, out float v)
		{
			float horizontalSpacing = size * 1.5f;
			float verticalSpacing = size * Mathf.Sqrt(3f);

			float columnOffset = IsOffsetColumn(column) ? verticalSpacing * 0.5f : 0f;

			u = column * horizontalSpacing;
			v = row * verticalSpacing + columnOffset;
		}

        /// <summary>
        /// Converts layout plane coordinates into the nearest flat-top offset cell.
        /// </summary>
		private void PlaneUVToOffset(float u, float v, float size, out int column, out int row)
		{
			float horizontalSpacing = size * 1.5f;
			float verticalSpacing = size * Mathf.Sqrt(3f);

			int estimatedCol = Mathf.RoundToInt(u / horizontalSpacing);
			float columnOffset = IsOffsetColumn(estimatedCol) ? verticalSpacing * 0.5f : 0f;
			int estimatedRow = Mathf.RoundToInt((v - columnOffset) / verticalSpacing);

			FindClosestOffsetCell(u, v, size, estimatedCol, estimatedRow, out column, out row);
		}

        /// <summary>
        /// Searches near an estimated offset coordinate to find the closest hex center.
        /// </summary>
		private void FindClosestOffsetCell(float u, float v, float size, int centerColumn, int centerRow, out int closestColumn, out int closestRow)
		{
			closestColumn = centerColumn;
			closestRow = centerRow;

			float closestSqrDistance = float.PositiveInfinity;

			for (int colOffset = -1; colOffset <= 1; colOffset++)
			{
				for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
				{
					int candidateCol = centerColumn + colOffset;
					int candidateRow = centerRow + rowOffset;

					OffsetToPlaneUV(candidateCol, candidateRow, size, out float candidateU, out float candidateV);

					float deltaU = u - candidateU;
					float deltaV = v - candidateV;
					float sqrDistance = deltaU * deltaU + deltaV * deltaV;

					if (sqrDistance < closestSqrDistance)
					{
						closestSqrDistance = sqrDistance;
						closestColumn = candidateCol;
						closestRow = candidateRow;
					}
				}
			}
		}

        /// <summary>
        /// Returns true when a flat-top offset column should be shifted.
        /// </summary>
		private bool IsOffsetColumn(int column)
		{
			bool isOddColumn = (column & 1) == 1;

			if (_offsetParity == HexOffsetParity.Odd)
				return isOddColumn;

			return !isOddColumn;
		}

        /// <summary>
        /// Returns true when a pointy-top offset row should be shifted.
        /// </summary>
		private bool IsOffsetRow(int row)
		{
			bool isOddRow = (row & 1) == 1;

			if (_offsetParity == HexOffsetParity.Odd)
				return isOddRow;

			return !isOddRow;
		}

		#endregion

	}

}
