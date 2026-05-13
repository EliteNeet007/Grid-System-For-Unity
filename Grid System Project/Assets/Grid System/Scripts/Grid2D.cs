using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MerelyGames.Grids
{
    /// <summary>
    /// Grid2D is a container:
    /// - Owns storage, bounds, events, and layout parameters
    /// - Delegates all spatial mapping to an ISpatialGridGeometry2D strategy
    ///
    /// Canonical geometry API:
    /// - GetCellCenterWorldPosition
    /// - GetCellCornersWorldPosition
    /// - GetInts (WorldToCell)
    /// </summary>
    public class Grid2D<TGridObject>
    {
        #region Events

        /// <summary>
        /// Raised when a cell's stored grid object changes.
        /// </summary>
        public event EventHandler<OnGridObjectChangedEventArgs> OnGridObjectChanged;

        /// <summary>
        /// Event data for a changed grid object cell.
        /// </summary>
        public class OnGridObjectChangedEventArgs : EventArgs
        {
            /// <summary>
            /// The changed cell's X index.
            /// </summary>
            public int GridWidth;

            /// <summary>
            /// The changed cell's Y index.
            /// </summary>
            public int GridHeight;
        }

        /// <summary>
        /// Raises the grid object changed event for a specific cell.
        /// </summary>
        /// <param name="x">The X index of the changed cell.</param>
        /// <param name="y">The Y index of the changed cell.</param>
        public void TriggerGridObjectChanged(int x, int y)
        {
            OnGridObjectChanged?.Invoke(this, new OnGridObjectChangedEventArgs { GridWidth = x, GridHeight = y });
        }

        /// <summary>
        /// Raises the grid object changed event for a cell position.
        /// </summary>
        /// <param name="pos">The grid position of the changed cell.</param>
        public void TriggerGridObjectChanged(Vector2Int pos) => TriggerGridObjectChanged(pos.x, pos.y);

        /// <summary>
        /// Raises the grid object changed event for a world position by mapping it to the grid.
        /// </summary>
        /// <param name="worldPosition">The world-space point to map to the grid.</param>
        public void TriggerGridObjectChanged(Vector3 worldPosition)
        {
            GetInts(worldPosition, out int x, out int y);
            TriggerGridObjectChanged(x, y);
        }

		#endregion

		#region Data

		private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly float _cellSize;
        private readonly float _cellSpacing;
        private readonly Vector3 _originPosition;
        private readonly GridLayoutType2D _gridLayoutType;

        private readonly TGridObject[,] _gridArray;

        private ISpatialGridGeometry2D _geometry;

        #endregion

        #region Getters

        private Grid2DLayout Layout => new Grid2DLayout(_cellSize, _cellSpacing, _originPosition, _gridLayoutType);

        /// <summary>
        /// Gets the number of columns in the grid.
        /// </summary>
        public int GridWidth => _gridWidth;

        /// <summary>
        /// Gets the number of rows in the grid.
        /// </summary>
        public int GridHeight => _gridHeight;

        /// <summary>
        /// Gets the size of each grid cell.
        /// </summary>
        public float CellSize => _cellSize;

        /// <summary>
        /// Gets the spacing between cells.
        /// </summary>
        public float CellSpacing => _cellSpacing;

        /// <summary>
        /// Gets the world-space origin used by the grid layout.
        /// </summary>
        public Vector3 OriginPosition => _originPosition;

        /// <summary>
        /// Gets the plane layout used to convert between grid and world space.
        /// </summary>
        public GridLayoutType2D GridLayoutType => _gridLayoutType;

        /// <summary>
        /// The geometry strategy used for world/cell mapping and debug geometry.
        /// Defaults to square geometry if not set.
        /// </summary>
        public ISpatialGridGeometry2D Geometry
        {
            get => _geometry;
            private set => _geometry = value ?? new SquareGridGeometry2D();
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a grid with default values in every cell.
        /// </summary>
        public Grid2D(int gridWidth, int gridHeight, Vector3 originPosition, float cellSize = 1f, float cellSpacing = 0f,
            GridLayoutType2D gridLayoutType = default, ISpatialGridGeometry2D geometryType = null, bool showDebug = false)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _cellSize = cellSize;
            _cellSpacing = cellSpacing;
            _originPosition = originPosition;
            _gridLayoutType = gridLayoutType;

            _geometry = geometryType ?? new SquareGridGeometry2D();
            _gridArray = new TGridObject[gridWidth, gridHeight];

            // Initialize each cell explicitly so constructor behavior is consistent across grid object types.
            for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                _gridArray[x, y] = default;

            // Runtime debug lines are optional because they are transient and can be noisy in large grids.
            if (showDebug)
                DrawDebugLinesRuntime(Color.white, 100f, new GridDebugSettings(depthOffset: 0f, drawCellOutlines: true));
        }

        /// <summary>
        /// Creates a grid and uses a factory to populate each cell.
        /// </summary>
        public Grid2D(int gridWidth, int gridHeight, Vector3 originPosition, Func<TGridObject> CreateGridObject, float cellSize = 1f,
            float cellSpacing = 0f, GridLayoutType2D gridLayoutType = default, ISpatialGridGeometry2D geometryType = null, bool showDebug = false)
            : this(gridWidth, gridHeight, originPosition, cellSize, cellSpacing, gridLayoutType, geometryType, showDebug)
        {
            for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                _gridArray[x, y] = CreateGridObject();

            if (showDebug)
                DrawDebugLinesRuntime(Color.white, 100f, new GridDebugSettings(0f, drawCellOutlines: true));
        }

        /// <summary>
        /// Creates a grid and uses a coordinate-aware factory to populate each cell.
        /// </summary>
        public Grid2D(int gridWidth, int gridHeight, Vector3 originPosition, Func<int, int, TGridObject> CreateGridObject, float cellSize = 1f,
            float cellSpacing = 0f, GridLayoutType2D gridLayoutType = default, ISpatialGridGeometry2D geometryType = null, bool showDebug = false)
            : this(gridWidth, gridHeight, originPosition, cellSize, cellSpacing, gridLayoutType, geometryType, showDebug)
        {
            for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                _gridArray[x, y] = CreateGridObject(x, y);

            if (showDebug)
                DrawDebugLinesRuntime(Color.white, 100f, new GridDebugSettings(0f, drawCellOutlines: true));
        }

        /// <summary>
        /// Creates a grid and uses a factory that can inspect the grid while populating each cell.
        /// </summary>
        public Grid2D(int gridWidth, int gridHeight, Vector3 originPosition, Func<Grid2D<TGridObject>, int, int, TGridObject> CreateGridObject, float cellSize = 1f,
            float cellSpacing = 0f, GridLayoutType2D gridLayoutType = default, ISpatialGridGeometry2D geometryType = null, bool showDebug = false)
            : this(gridWidth, gridHeight, originPosition, cellSize, cellSpacing, gridLayoutType, geometryType, showDebug)
        {
            for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                _gridArray[x, y] = CreateGridObject(this, x, y);

            if (showDebug)
                DrawDebugLinesRuntime(Color.white, 100f, new GridDebugSettings(0f, drawCellOutlines: true));
        }

        #endregion

        #region Validation

        /// <summary>
        /// Returns true if the provided grid indices are within the grid bounds.
        /// </summary>
        public bool IsValidGridPosition(int x, int y)
        {
            return (x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight);
        }

        /// <summary>
        /// Returns true if the provided grid position is within the grid bounds.
        /// </summary>
        public bool IsValidGridPosition(Vector2Int pos) => IsValidGridPosition(pos.x, pos.y);

        /// <summary>
        /// Returns true if the world position maps to a cell inside the grid bounds.
        /// </summary>
        public bool IsValidGridPosition(Vector3 worldPosition)
        {
            GetInts(worldPosition, out int x, out int y);
            return IsValidGridPosition(x, y);
        }

        /// <summary>
        /// Clamps indices to the valid grid range.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the grid has non-positive dimensions.</exception>
        public Vector2Int ClampToGrid(int x, int y)
        {
            if (_gridWidth <= 0 || _gridHeight <= 0)
                throw new InvalidOperationException("Cannot clamp to a grid with non-positive dimensions.");

            // Clamp after validating dimensions so the max bounds never become negative.
            x = Mathf.Clamp(x, 0, _gridWidth - 1);
            y = Mathf.Clamp(y, 0, _gridHeight - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Clamps the provided grid position to the valid grid range.
        /// </summary>
        public Vector2Int ClampToGrid(Vector2Int position) => ClampToGrid(position.x, position.y);

        /// <summary>
        /// Converts a world point to grid coordinates and clamps the result into the valid range.
        /// </summary>
        public Vector2Int ClampToGrid(Vector3 worldPosition)
        {
            GetInts(worldPosition, out int x, out int y);
            return ClampToGrid(x, y);
        }

		#endregion

		#region Storage

        /// <summary>
        /// Stores the value at the given cell and notifies listeners if the position is valid.
        /// </summary>
        public bool SetGridObject(int x, int y, TGridObject value)
        {
            if (!IsValidGridPosition(x, y))
                return false;

            // Notify after storage changes so listeners can immediately read the new value.
            _gridArray[x, y] = value;
            TriggerGridObjectChanged(x, y);
            return true;
        }

        /// <summary>
        /// Stores the value at the given grid position.
        /// </summary>
        public bool SetGridObject(Vector2Int pos, TGridObject value) => SetGridObject(pos.x, pos.y, value);

        /// <summary>
        /// Stores a value at the cell mapped from a world position.
        /// </summary>
        public bool SetGridObject(Vector3 worldPosition, TGridObject value)
        {
            GetInts(worldPosition, out int x, out int y);
            return SetGridObject(x, y, value);
        }

        /// <summary>
        /// Reads a grid object or returns the default value when the position is invalid.
        /// </summary>
        [Obsolete("Use TryGetCell / ensure valid grid position beforehand to avoid default return value.")]
        public TGridObject GetGridObject(int x, int y)
        {
            return IsValidGridPosition(x, y) ? _gridArray[x, y] : default;
        }

        /// <summary>
        /// Reads a grid object by grid position or returns the default value when invalid.
        /// </summary>
        [Obsolete("Use TryGetCell / ensure valid grid position beforehand to avoid default return value.")]
        public TGridObject GetGridObject(Vector2Int pos) => GetGridObject(pos.x, pos.y);

        /// <summary>
        /// Reads a grid object by world position or returns the default value when invalid.
        /// </summary>
		[Obsolete("Use TryGetCell / ensure valid grid position beforehand to avoid default return value.")]
		public TGridObject GetGridObject(Vector3 worldPosition)
        {
            GetInts(worldPosition, out int x, out int y);
            return GetGridObject(x, y);
        }

        /// <summary>
        /// Attempts to read the grid object at the given coordinates.
        /// </summary>
        public bool TryGetGridObject(int x, int y, out TGridObject gridObject)
        {
            if (!IsValidGridPosition(x, y))
            {
                gridObject = default;
                return false;
            }

            gridObject = _gridArray[x, y];
            return true;
        }

        /// <summary>
        /// Attempts to read the grid object at the given grid position.
        /// </summary>
        public bool TryGetGridObject(Vector2Int position, out TGridObject gridObject) => TryGetGridObject(position.x, position.y, out gridObject);

        /// <summary>
        /// Attempts to read the grid object under the provided world position.
        /// </summary>
        public bool TryGetGridObject(Vector3 worldPosition, out TGridObject gridObject)
        {
            GetInts(worldPosition, out int x, out int y);
            return TryGetGridObject(x, y, out gridObject);
        }

        #endregion

        #region World <-> Cell conversions (strategy)

        /// <summary>
        /// Converts a world-space point into grid indices using the current geometry strategy.
        /// </summary>
        public void GetInts(Vector3 worldPosition, out int x, out int y)
        {
            _geometry.WorldToCell(worldPosition, Layout, out x, out y);
        }

        /// <summary>
        /// Converts a world-space point into grid indices and returns them as a Vector2Int.
        /// </summary>
        public Vector2Int GetVectorInts(Vector3 worldPosition)
        {
            GetInts(worldPosition, out int x, out int y);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Returns a random valid grid position.
        /// </summary>
        public Vector2Int GetRandomCellInts()
        {
            return new Vector2Int(Random.Range(0, _gridWidth), Random.Range(0, _gridHeight));
        }

        /// <summary>
        /// Returns the world-space center of the specified grid cell.
        /// </summary>
        public Vector3 GetCellCenterWorldPosition(int x, int y, float depthOffset = 0f)
        {
            return _geometry.CellToWorldCenter(x, y, Layout, depthOffset);
        }

        /// <summary>
        /// Returns the world-space center of the specified grid cell.
        /// </summary>
        public Vector3 GetCellCenterWorldPosition(Vector2Int pos, float depthOffset = 0f)
            => GetCellCenterWorldPosition(pos.x, pos.y, depthOffset);

        public Vector3 GetCellCenterWorldPosition(Vector3 worldPosition, float depthOffset = 0f)
        {
            GetInts(worldPosition, out int x, out int y);
            return GetCellCenterWorldPosition(x, y, depthOffset);
        }

        /// <summary>
        /// Writes corners to buffer (len >= Geometry.CellCornerCount).
        /// </summary>
        public void GetCellCornersWorldPosition(int x, int y, Vector3[] cornersBuffer, float depthOffset = 0f)
        {
            if (cornersBuffer == null) throw new ArgumentNullException(nameof(cornersBuffer));
            if (cornersBuffer.Length < _geometry.CellCornerCount)
                throw new ArgumentException($"cornersBuffer length must be >= {_geometry.CellCornerCount} for {_geometry.GetType().Name}");

            _geometry.GetCellCornersWorld(x, y, Layout, cornersBuffer, depthOffset);
        }

        /// <summary>
        /// Returns the world-space center of the full grid bounds.
        /// </summary>
        public Vector3 GetGridCenterWorldPosition(float depthOffset = 0f)
        {
            _geometry.GetGridWorldBounds(_gridWidth, _gridHeight, Layout, out Vector3 minBounds, out Vector3 maxBounds, depthOffset);
            return (minBounds + maxBounds) * 0.5f;
        }

        /// <summary>
        /// Returns the grid size in layout plane coordinates.
        /// </summary>
        public Vector2 GetGridPlaneSize()
        {
            return _geometry.GetGridPlaneSize(_gridWidth, _gridHeight, Layout);
        }

		#endregion

		#region Actions

		/// <summary>
		/// Executes an input action for every cell in the grid.
		/// </summary>
		/// <param name="action"></param>
		public void ForEachCell(Action<int, int, TGridObject> action)
		{
			for (int x = 0; x < _gridWidth; x++)
			{
				for (int y = 0; y < _gridHeight; y++)
				{
					action(x, y, _gridArray[x, y]);
				}
			}
		}

		#endregion

		#region Neighbors

        /// <summary>
        /// Fills a buffer with neighboring grid positions for a cell.
        /// </summary>
		public int FillNeighborsBuffer(int x, int y, Vector2Int[] neighborsBuffer,
            GridAdjacencyMode2D mode = GridAdjacencyMode2D.EdgeNeighborsOnly, bool includeInvalid = false)
        {
			if (neighborsBuffer == null) throw new ArgumentNullException(nameof(neighborsBuffer));

			int count = _geometry.FillNeighborPositionsBuffer(x, y, mode, neighborsBuffer);

            // Some callers need raw geometry neighbors even when they are outside the configured grid bounds.
			if (includeInvalid)
				return count;

			int writeIndex = 0;

			for (int i = 0; i < count; i++)
			{
				Vector2Int neighbor = neighborsBuffer[i];
				if (IsValidGridPosition(neighbor))
				{
					neighborsBuffer[writeIndex] = neighbor;
					writeIndex++;
				}
			}

			return writeIndex;
		}

        /// <summary>
        /// Fills a buffer with neighboring grid positions for a grid position.
        /// </summary>
        public int FillNeighborsBuffer(Vector2Int position, Vector2Int[] neighborsBuffer,
			GridAdjacencyMode2D mode = GridAdjacencyMode2D.EdgeNeighborsOnly, bool includeInvalid = false)
            => FillNeighborsBuffer(position.x, position.y, neighborsBuffer, mode, includeInvalid);

        /// <summary>
        /// Fills a buffer with neighboring grid positions for the cell under a world position.
        /// </summary>
        public int FillNeighborsBuffer(Vector3 worldPosition, Vector2Int[] neighborsBuffer,
			GridAdjacencyMode2D mode = GridAdjacencyMode2D.EdgeNeighborsOnly, bool includeInvalid = false)
        {
            GetInts(worldPosition, out int x, out int y);
            return FillNeighborsBuffer(x, y, neighborsBuffer, mode, includeInvalid);
        }

        /// <summary>
        /// Fills a buffer with edge-adjacent neighbors for a cell.
        /// </summary>
        public int FillEdgeNeighborsBuffer(int x, int y, Vector2Int[] neighborsBuffer, bool includeInvalid = false)
        {
            return FillNeighborsBuffer(x, y, neighborsBuffer, GridAdjacencyMode2D.EdgeNeighborsOnly, includeInvalid);
        }

        /// <summary>
        /// Fills a buffer with edge-adjacent neighbors for a grid position.
        /// </summary>
        public int FillEdgeNeighborsBuffer(Vector2Int position, Vector2Int[] neighborsBuffer, bool includeInvalid = false)
            => FillEdgeNeighborsBuffer(position.x, position.y, neighborsBuffer, includeInvalid);

        /// <summary>
        /// Fills a buffer with edge-adjacent neighbors for the cell under a world position.
        /// </summary>
        public int FillEdgeNeighborsBuffer(Vector3 worldPosition, Vector2Int[] neighborsBuffer, bool includeInvalid = false)
        {
            GetInts(worldPosition, out int x, out int y);
            return FillEdgeNeighborsBuffer(x, y, neighborsBuffer, includeInvalid);
        }

        /// <summary>
        /// Fills a buffer with vertex-only neighbors for a cell.
        /// </summary>
        public int FillVertexNeighborsBuffer(int x, int y, Vector2Int[] neighborsBuffer, bool includeInvalid = false)
        {
            if (neighborsBuffer == null)
                throw new ArgumentNullException(nameof(neighborsBuffer));

            if (_geometry.GeometryType2D == GridGeometryType2D.Hexagon)
                throw new NotSupportedException("Hexagon grids do not currently define separate vertex-neighbor behavior.");

            // Compare all neighbors against edge neighbors so only corner-touching cells remain.
            Vector2Int[] edgeNeighborsBuffer = new Vector2Int[4];
            Vector2Int[] allNeighborsBuffer = new Vector2Int[12];

            int edgeNeighborCount = FillNeighborsBuffer(x, y, edgeNeighborsBuffer, GridAdjacencyMode2D.EdgeNeighborsOnly, includeInvalid);
            int allNeighborCount = FillNeighborsBuffer(x, y, allNeighborsBuffer, GridAdjacencyMode2D.IncludeVertexNeighbors, includeInvalid);

            int writeIndex = 0;

            for (int i = 0; i < allNeighborCount; i++)
            {
                Vector2Int candidateNeighbor = allNeighborsBuffer[i];
                bool isEdgeNeighbor = false;

                for (int j = 0; j < edgeNeighborCount; j++)
                {
                    if (edgeNeighborsBuffer[j] == candidateNeighbor)
                    {
                        isEdgeNeighbor = true;
                        break;
                    }
                }

                if (isEdgeNeighbor)
                    continue;

                if (writeIndex >= neighborsBuffer.Length)
                    throw new ArgumentException("neighborsBuffer is too small for the vertex neighbors.");

                neighborsBuffer[writeIndex] = candidateNeighbor;
                writeIndex++;
            }

            return writeIndex;
        }

        /// <summary>
        /// Fills a buffer with vertex-only neighbors for a grid position.
        /// </summary>
        public int FillVertexNeighborsBuffer(Vector2Int position, Vector2Int[] neighborsBuffer, bool includeInvalid = false)
            => FillVertexNeighborsBuffer(position.x, position.y, neighborsBuffer, includeInvalid);

        /// <summary>
        /// Fills a buffer with vertex-only neighbors for the cell under a world position.
        /// </summary>
        public int FillVertexNeighborsBuffer(Vector3 worldPosition, Vector2Int[] neighborsBuffer, bool includeInvalid = false)
        {
            GetInts(worldPosition, out int x, out int y);
            return FillVertexNeighborsBuffer(x, y, neighborsBuffer, includeInvalid);
        }

		#endregion

		#region Rotations

        /// <summary>
        /// Returns the rotation that faces the grid plane.
        /// </summary>
		public Quaternion GetFacingRotation() => Layout.GetFacingRotation();

        /// <summary>
        /// Returns the inverse of the rotation that faces the grid plane.
        /// </summary>
        public Quaternion GetInvertedFacingRotation() => Layout.GetInvertedFacingRotation();

        #endregion

        #region Debug

        /// <summary>
        /// Writes debug line segments for the grid into a provided list.
        /// </summary>
        public void GetDebugLineSegmentsNonAlloc(List<GridLineSegment> segments, GridDebugSettings settings)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            segments.Clear();
            _geometry.AppendDebugLineSegments(_gridWidth, _gridHeight, Layout, settings, segments);
        }

        /// <summary>
        /// Draws debug line segments through Unity's runtime debug drawing API.
        /// </summary>
        public void DrawDebugLinesRuntime(Color color, float durationSeconds, GridDebugSettings settings)
        {
            // NOTE: For editor-persistent drawing, prefer Gizmos with GetDebugLineSegmentsNonAlloc.
            List<GridLineSegment> segments = new List<GridLineSegment>(2048);
            GetDebugLineSegmentsNonAlloc(segments, settings);

            for (int i = 0; i < segments.Count; i++)
            {
                GridLineSegment segment = segments[i];
                Debug.DrawLine(segment.A, segment.B, color, durationSeconds);
            }
        }

        #endregion

        #region Grid-Specific Methods

        /// <summary>
        /// Attempts to get the orientation for a triangle grid cell.
        /// </summary>
        public bool TryGetTriangleOrientation(int x, int y, out EquilateralTriangleOrientation2D orientation)
        {
            // Triangle orientation is only meaningful for triangle geometry.
            if (_geometry is EquilateralTriangleGridGeometry2D triangleGeometry)
            {
                orientation = triangleGeometry.GetTriangleOrientation(x, y, Layout);
                return true;
            }

            orientation = default;
            return false;
        }

        /// <summary>
        /// Attempts to get the orientation for a triangle grid position.
        /// </summary>
        public bool TryGetTriangleOrientation(Vector2Int position, out EquilateralTriangleOrientation2D orientation)
            => TryGetTriangleOrientation(position.x, position.y, out orientation);

        /// <summary>
        /// Attempts to get the orientation for the triangle cell under a world position.
        /// </summary>
        public bool TryGetTriangleOrientation(Vector3 worldPosition, out EquilateralTriangleOrientation2D orientation)
        {
            return TryGetTriangleOrientation(GetVectorInts(worldPosition), out orientation);
        }

        #endregion

    }

    public enum GridLayoutType2D
    {
        /// <summary>
        /// Layout placing the grid along the X,Y plane (X -> width, Y -> height).
        /// </summary>
        Vertical,
        /// <summary>
        /// Layout placing the grid along the X,Z plane (X -> width, Z -> height).
        /// </summary>
        Horizontal,
        /// <summary>
        /// Layout placing the grid along the Z,Y plane (Z -> width, Y -> height).
        /// </summary>
        VerticalDepth,
    }

    public enum GridAdjacencyMode2D
    {
        EdgeNeighborsOnly,
        IncludeVertexNeighbors,
    }
}
