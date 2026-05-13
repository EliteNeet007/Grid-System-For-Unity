using System.Collections;
using System.Collections.Generic;
using MerelyGames.Grids;
using UnityEngine;

namespace MerelyGames.TerritoryControl
{
    public sealed class TerritoryGameView : MonoBehaviour
    {
        [Header("Geometry")]
        [Tooltip("Selects which geometry settings are used when a new game starts.")]
        [SerializeField] private TerritoryGameSettingsPreset _selectedSettings = TerritoryGameSettingsPreset.Square;
        [SerializeField] private TerritoryGameSettings _squareSettings = CreateDefaultSettings(TerritoryGridKind.Square);
        [Tooltip("Grid and board values used by the hex preset.")]
        [SerializeField] private TerritoryGameSettings _hexSettings = CreateDefaultSettings(TerritoryGridKind.Hex);
        [Tooltip("Grid and board values used by the triangle preset.")]
        [SerializeField] private TerritoryGameSettings _triangleSettings = CreateDefaultSettings(TerritoryGridKind.Triangle);
        [Header("Difficulty")]
        [SerializeField] private TerritoryAIDifficulty _aiDifficulty = TerritoryAIDifficulty.Medium;
        [Tooltip("Uses the custom AI tuning values instead of the selected difficulty preset.")]
        [SerializeField] private bool _useCustomAISettings;
        [SerializeField] private TerritoryAISettings _customAISettings = new TerritoryAISettings();
        [Tooltip("Camera used for pointer-to-grid picking. Falls back to the main camera when unset.")]
        [SerializeField] private Camera _inputCamera;

        [Header("Rendering")]
        [SerializeField] private Material _cellMaterial;
        [SerializeField] private Sprite _squareCellSprite;
        [SerializeField] private Sprite _hexCellSprite;
        [SerializeField] private Sprite _triangleCellSprite;
        [Tooltip("Optional parent for generated cell objects. Uses this transform when unset.")]
        [SerializeField] private Transform _cellsRoot;
        [Tooltip("World-space Z offset used when placing and picking board cells.")]
        [SerializeField] private float _depthOffset;
        [Min(0.01f)]
        [Tooltip("Multiplies the calculated sprite scale after fitting each sprite to its cell bounds.")]
        [SerializeField] private float _spriteScaleMultiplier = 1f;
        [Tooltip("Frames an orthographic camera around the board whenever a new game starts.")]
        [SerializeField] private bool _frameCameraOnStart = true;
        [SerializeField] private Color _emptyColor = new Color(0.18f, 0.2f, 0.22f);
        [SerializeField] private Color _playerColor = new Color(0.1f, 0.55f, 0.95f);
        [SerializeField] private Color _aiColor = new Color(0.95f, 0.28f, 0.22f);
        [Tooltip("Color used for cells the player may legally choose on this turn.")]
        [SerializeField] private Color _legalMoveColor = new Color(0.18f, 0.75f, 0.38f);
        [Tooltip("Color used to preview a preselected AI start before the player chooses a start.")]
        [SerializeField] private Color _aiStartPreviewColor = new Color(1f, 0.64f, 0.2f);
        [Tooltip("Applies a hover color blend to the cell currently under the pointer.")]
        [SerializeField] private bool _showHoverHighlight = true;
        [Range(0f, 1f)]
        [SerializeField] private float _selectableHoverBlend = 0.4f;
        [Range(0f, 1f)]
        [SerializeField] private float _blockedHoverBlend = 0.28f;
        [SerializeField] private Color _selectableHoverColor = Color.white;
        [SerializeField] private Color _blockedHoverColor = new Color(1f, 0.42f, 0.32f);

        [Header("Auto Fill")]
        [Tooltip("Animates final territory fill resolution instead of applying it instantly.")]
        [SerializeField] private bool _animateAutoFill = true;
        [Tooltip("Fills all frontier cells in each animation step instead of one cell at a time.")]
        [SerializeField] private bool _autoFillByDistanceRing = true;
        [Min(0f)]
        [Tooltip("Delay before the automatic final fill animation begins.")]
        [SerializeField] private float _autoFillStartDelay = 0.35f;
        [Min(0f)]
        [Tooltip("Delay between automatic final fill animation steps.")]
        [SerializeField] private float _autoFillStepDelay = 0.08f;

        [Header("HUD")]
        [SerializeField] private bool _showHud = true;
        [Tooltip("Screen-space top-left position for the immediate-mode HUD.")]
        [SerializeField] private Vector2 _hudPosition = new Vector2(16f, 16f);
        [Tooltip("Screen-space size of the immediate-mode HUD.")]
        [SerializeField] private Vector2 _hudSize = new Vector2(260f, 200f);
        [Tooltip("How long an illegal selection warning remains visible in the HUD.")]
        [SerializeField] private float _illegalSelectionMessageSeconds = 1.25f;

        private readonly Dictionary<Vector2Int, TerritoryCellView> _cellViews = new Dictionary<Vector2Int, TerritoryCellView>();
        private TerritoryGame _game;
        private HashSet<Vector2Int> _legalPlayerMoves = new HashSet<Vector2Int>();
        private Vector2Int? _hoveredCell;
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudTextStyle;
        private GUIStyle _hudWarningStyle;
        private string _selectionMessage;
        private float _selectionMessageUntil;
        private Coroutine _autoFillCoroutine;

        public TerritoryGame Game => _game;

        /// <summary>
        /// Starts the first territory game when the scene begins.
        /// </summary>
        private void Start()
        {
            StartNewGame();
        }

        /// <summary>
        /// Updates hover state and handles player cell selection input.
        /// </summary>
        private void Update()
        {
            if (_game == null)
                return;

            UpdateHoveredCell();

            // Only a fresh left-click on a valid cell should attempt a game action.
            if (!Input.GetMouseButtonDown(0) || !_hoveredCell.HasValue)
                return;

            Vector2Int position = _hoveredCell.Value;
            bool changed = _game.Phase == TerritoryGamePhase.Setup
                ? _game.TrySelectPlayerStart(position)
                : _game.TryPlayerExpand(position);

            if (changed)
            {
                // A valid selection can change legal moves, ownership, and pending fill state.
                ClearSelectionMessage();
                RefreshCells();
                StartAutoFillAnimationIfNeeded();
            }
            else
            {
                ShowSelectionMessage(GetIllegalSelectionMessage(position));
            }
        }

        /// <summary>
        /// Draws the score and phase HUD when enabled.
        /// </summary>
        private void OnGUI()
        {
            if (!_showHud || _game == null)
                return;

            EnsureHudStyles();

            Rect hudRect = new Rect(_hudPosition.x, _hudPosition.y, _hudSize.x, _hudSize.y);
            GUILayout.BeginArea(hudRect, _hudBoxStyle);
            GUILayout.Label("Territory Control", _hudTitleStyle);
            GUILayout.Space(4f);

            TerritoryScore score = _game.GetCurrentScore();
            GUILayout.Label($"Phase: {GetPhaseLabel(_game.Phase)}", _hudTextStyle);
            GUILayout.Label($"Player: {score.PlayerTiles}", _hudTextStyle);
            GUILayout.Label($"AI: {score.AITiles}", _hudTextStyle);
            GUILayout.Label($"Empty: {score.EmptyTiles}", _hudTextStyle);

            if (_game.Phase == TerritoryGamePhase.Complete)
                GUILayout.Label(GetWinnerLabel(_game.FinalScore.Winner), _hudTitleStyle);
            else if (!string.IsNullOrEmpty(_selectionMessage) && Time.time < _selectionMessageUntil)
                GUILayout.Label(_selectionMessage, _hudWarningStyle);

            GUILayout.EndArea();
        }

        /// <summary>
        /// Clears the current board view and starts a new territory game with the selected settings.
        /// </summary>
        [ContextMenu("Start New Territory Game")]
        public void StartNewGame()
        {
            if (_autoFillCoroutine != null)
            {
                StopCoroutine(_autoFillCoroutine);
                _autoFillCoroutine = null;
            }

            ClearCells();
            _game = new TerritoryGame(GetSelectedSettings(), GetAISettings());
            _game.DeferAutoFillResolution = _animateAutoFill;
            _hoveredCell = null;
            ClearSelectionMessage();
            BuildCells();
            FrameCamera();
            RefreshCells();
            StartAutoFillAnimationIfNeeded();
        }

        /// <summary>
        /// Gets the board settings for the currently selected geometry preset.
        /// </summary>
        private TerritoryGameSettings GetSelectedSettings()
        {
            return _selectedSettings switch
            {
                TerritoryGameSettingsPreset.Hex => _hexSettings ?? CreateDefaultSettings(TerritoryGridKind.Hex),
                TerritoryGameSettingsPreset.Triangle => _triangleSettings ?? CreateDefaultSettings(TerritoryGridKind.Triangle),
                _ => _squareSettings ?? CreateDefaultSettings(TerritoryGridKind.Square),
            };
        }

        /// <summary>
        /// Gets the AI settings from either the custom data or the difficulty preset.
        /// </summary>
        private TerritoryAISettings GetAISettings()
        {
            return _useCustomAISettings
                ? _customAISettings
                : TerritoryAISettings.CreatePreset(_aiDifficulty);
        }

        /// <summary>
        /// Creates default board settings for a geometry kind.
        /// </summary>
        private static TerritoryGameSettings CreateDefaultSettings(TerritoryGridKind gridKind)
        {
            return new TerritoryGameSettings
            {
                CellSize = gridKind == TerritoryGridKind.Hex ? 0.5f : 1f,
                GridKind = gridKind,
            };
        }

        /// <summary>
        /// Creates a visual cell object for every board position.
        /// </summary>
        private void BuildCells()
        {
            foreach (Vector2Int position in _game.Board.AllPositions())
            {
                TerritoryCellView cellView = CreateCellView(position);
                _cellViews.Add(position, cellView);
            }
        }

        /// <summary>
        /// Creates and initializes a single visual cell for the given board position.
        /// </summary>
        private TerritoryCellView CreateCellView(Vector2Int position)
        {
            GameObject cellObject = new GameObject($"Cell {position.x},{position.y}");
            cellObject.transform.SetParent(_cellsRoot != null ? _cellsRoot : transform, false);
            cellObject.transform.position = _game.Board.Grid.GetCellCenterWorldPosition(position, _depthOffset);
            cellObject.transform.rotation = GetCellRotation(position);
            
            TerritoryCellView cellView = cellObject.AddComponent<TerritoryCellView>();
            cellView.Initialize(position, GetCellSprite(), _cellMaterial);
            return cellView;
        }

        /// <summary>
        /// Gets the sprite that matches the active grid geometry.
        /// </summary>
        private Sprite GetCellSprite()
        {
            return _game.Board.GridKind switch
            {
                TerritoryGridKind.Hex => _hexCellSprite,
                TerritoryGridKind.Triangle => _triangleCellSprite,
                _ => _squareCellSprite,
            };
        }

        /// <summary>
        /// Gets any geometry-specific sprite rotation for a board position.
        /// </summary>
        private Quaternion GetCellRotation(Vector2Int position)
        {
            if (_game.Board.GridKind == TerritoryGridKind.Triangle)
            {
                if (_game.Board.Grid.TryGetTriangleOrientation(position, out EquilateralTriangleOrientation2D orientation))
                {
                    if (orientation == EquilateralTriangleOrientation2D.Down)
                        return Quaternion.Euler(0f, 0f, 180f);
                }
            }

            return Quaternion.identity;
        }

        /// <summary>
        /// Rebuilds legal move highlights and applies each cell's current color.
        /// </summary>
        private void RefreshCells()
        {
            _legalPlayerMoves = _game.Phase == TerritoryGamePhase.PlayerTurn
                ? new HashSet<Vector2Int>(_game.GetLegalPlayerMoves())
                : new HashSet<Vector2Int>();

            foreach (KeyValuePair<Vector2Int, TerritoryCellView> entry in _cellViews)
                entry.Value.SetColor(GetCellColor(entry.Key));
        }

        /// <summary>
        /// Gets the displayed color for a cell, including hover feedback.
        /// </summary>
        private Color GetCellColor(Vector2Int position)
        {
            Color color = GetBaseCellColor(position);
            if (!_showHoverHighlight || !_hoveredCell.HasValue || _hoveredCell.Value != position)
                return color;

            return IsSelectableCell(position)
                ? Color.Lerp(color, _selectableHoverColor, _selectableHoverBlend)
                : Color.Lerp(color, _blockedHoverColor, _blockedHoverBlend);
        }

        /// <summary>
        /// Gets the ownership or legal-move color for a cell before hover blending.
        /// </summary>
        private Color GetBaseCellColor(Vector2Int position)
        {
            if (_game.Phase == TerritoryGamePhase.Setup && _game.HasAIStartPlacement && position == _game.AIStartPlacement.AIStart)
                return _aiStartPreviewColor;

            TerritoryOwnership ownership = _game.Board.GetOwnership(position);
            if (ownership == TerritoryOwnership.Player)
                return _playerColor;
            if (ownership == TerritoryOwnership.AI)
                return _aiColor;
            if (_legalPlayerMoves.Contains(position))
                return _legalMoveColor;

            return _emptyColor;
        }

        /// <summary>
        /// Updates the currently hovered board cell and refreshes visuals when it changes.
        /// </summary>
        private void UpdateHoveredCell()
        {
            Vector2Int? nextHoveredCell = TryGetPointerCell(out Vector2Int position)
                ? position
                : null;

            if (_hoveredCell == nextHoveredCell)
                return;

            _hoveredCell = nextHoveredCell;
            RefreshCells();
        }

        /// <summary>
        /// Checks whether the player can choose the given cell in the current phase.
        /// </summary>
        private bool IsSelectableCell(Vector2Int position)
        {
            if (_game.Phase == TerritoryGamePhase.Setup)
            {
                if (_game.HasAIStartPlacement)
                    return _game.Board.IsLegalStartingCell(position, _game.AIStartPlacement.AIStart);

                return _game.Board.IsValidPosition(position) && _game.Board.GetOwnership(position) == TerritoryOwnership.Empty;
            }

            if (_game.Phase == TerritoryGamePhase.PlayerTurn)
                return _legalPlayerMoves.Contains(position);

            return false;
        }

        /// <summary>
        /// Converts the current pointer position into a valid board cell.
        /// </summary>
        private bool TryGetPointerCell(out Vector2Int position)
        {
            position = default;
            Camera cameraToUse = _inputCamera != null ? _inputCamera : Camera.main;
            if (cameraToUse == null)
                return false;

            Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
            Plane boardPlane = new Plane(Vector3.forward, _game.Board.Grid.OriginPosition);

            if (!boardPlane.Raycast(ray, out float distance))
                return false;

            Vector3 worldPosition = ray.GetPoint(distance);
            position = _game.Board.Grid.GetVectorInts(worldPosition);
            return _game.Board.IsValidPosition(position);
        }

        /// <summary>
        /// Frames the active orthographic camera around the current board.
        /// </summary>
        private void FrameCamera()
        {
            if (!_frameCameraOnStart)
                return;

            Camera cameraToUse = _inputCamera != null ? _inputCamera : Camera.main;
            if (cameraToUse == null || !cameraToUse.orthographic)
                return;

            Vector3 center = _game.Board.Grid.GetGridCenterWorldPosition(_depthOffset);
            cameraToUse.transform.position = new Vector3(center.x, center.y, cameraToUse.transform.position.z);

            Vector2 size = _game.Board.Grid.GetGridPlaneSize();
            float verticalSize = size.y * 0.5f;
            float horizontalSize = size.x / Mathf.Max(0.01f, cameraToUse.aspect) * 0.5f;
            cameraToUse.orthographicSize = Mathf.Max(verticalSize, horizontalSize) * 1.12f;
        }

        /// <summary>
        /// Destroys all generated cell view objects and clears the lookup table.
        /// </summary>
        private void ClearCells()
        {
            foreach (TerritoryCellView cellView in _cellViews.Values)
            {
                if (cellView == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(cellView.gameObject);
                else
                    DestroyImmediate(cellView.gameObject);
            }

            _cellViews.Clear();
        }

        /// <summary>
        /// Starts the deferred final-fill animation when the game has pending cells.
        /// </summary>
        private void StartAutoFillAnimationIfNeeded()
        {
            if (!_animateAutoFill || _game == null || !_game.HasPendingAutoFill || _autoFillCoroutine != null)
                return;

            _autoFillCoroutine = StartCoroutine(AnimatePendingAutoFill());
        }

        /// <summary>
        /// Animates pending final-fill cells until the game completes.
        /// </summary>
        private IEnumerator AnimatePendingAutoFill()
        {
            _hoveredCell = null;

            if (_autoFillStartDelay > 0f)
                yield return new WaitForSeconds(_autoFillStartDelay);

            List<List<Vector2Int>> fillGroups = BuildAutoFillGroups(_game.PendingAutoFillCells, _game.PendingAutoFillOwnership);

            for (int groupIndex = 0; groupIndex < fillGroups.Count; groupIndex++)
            {
                if (_game == null || !_game.HasPendingAutoFill)
                    break;

                _game.ApplyPendingAutoFillCells(fillGroups[groupIndex]);
                RefreshCells();

                if (_autoFillStepDelay > 0f)
                    yield return new WaitForSeconds(_autoFillStepDelay);
            }

            RefreshCells();
            _autoFillCoroutine = null;
        }

        /// <summary>
        /// Builds the ordered groups used by the final-fill animation.
        /// </summary>
        private List<List<Vector2Int>> BuildAutoFillGroups(IReadOnlyList<Vector2Int> cells, TerritoryOwnership fillOwnership)
        {
            return BuildAutoFillFrontierGroups(cells, fillOwnership);
        }

        /// <summary>
        /// Builds frontier-based fill groups expanding from already owned territory.
        /// </summary>
        private List<List<Vector2Int>> BuildAutoFillFrontierGroups(IReadOnlyList<Vector2Int> cells, TerritoryOwnership fillOwnership)
        {
            List<List<Vector2Int>> groups = new List<List<Vector2Int>>();
            HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(cells);
            HashSet<Vector2Int> virtualOwned = GetOwnedCellsSet(fillOwnership);
            List<Vector2Int> enemyCells = GetOwnedCells(GetOpponentOwnership(fillOwnership));

            while (remaining.Count > 0)
            {
                List<Vector2Int> frontier = GetAutoFillFrontier(remaining, virtualOwned, enemyCells);
                if (frontier.Count == 0)
                    break;

                List<Vector2Int> group = _autoFillByDistanceRing
                    ? frontier
                    : new List<Vector2Int> { frontier[0] };

                groups.Add(group);

                for (int groupCellIndex = 0; groupCellIndex < group.Count; groupCellIndex++)
                {
                    remaining.Remove(group[groupCellIndex]);
                    virtualOwned.Add(group[groupCellIndex]);
                }
            }

            return groups;
        }

        /// <summary>
        /// Finds the pending fill cells adjacent to the virtual owned territory.
        /// </summary>
        private List<Vector2Int> GetAutoFillFrontier(HashSet<Vector2Int> remaining, HashSet<Vector2Int> virtualOwned, List<Vector2Int> enemyCells)
        {
            List<Vector2Int> frontier = new List<Vector2Int>();

            foreach (Vector2Int cell in remaining)
            {
                List<Vector2Int> neighbors = _game.Board.GetNeighbors(cell);
                bool adjacentToOwned = false;

                for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
                {
                    if (!virtualOwned.Contains(neighbors[neighborIndex]))
                        continue;

                    adjacentToOwned = true;
                    break;
                }

                if (!adjacentToOwned)
                    continue;

                frontier.Add(cell);
            }

            SortCellsByEnemyProximity(frontier, enemyCells);
            return frontier;
        }

        /// <summary>
        /// Gets all board positions currently owned by the specified owner.
        /// </summary>
        private List<Vector2Int> GetOwnedCells(TerritoryOwnership ownership)
        {
            List<Vector2Int> cells = new List<Vector2Int>();

            foreach (Vector2Int position in _game.Board.AllPositions())
            {
                if (_game.Board.GetOwnership(position) == ownership)
                    cells.Add(position);
            }

            return cells;
        }

        /// <summary>
        /// Gets all board positions owned by the specified owner as a set.
        /// </summary>
        private HashSet<Vector2Int> GetOwnedCellsSet(TerritoryOwnership ownership)
        {
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();

            foreach (Vector2Int position in _game.Board.AllPositions())
            {
                if (_game.Board.GetOwnership(position) == ownership)
                    cells.Add(position);
            }

            return cells;
        }

        /// <summary>
        /// Sorts cells so final-fill animation favors cells nearest to enemy territory first.
        /// </summary>
        private static void SortCellsByEnemyProximity(List<Vector2Int> cells, List<Vector2Int> enemyCells)
        {
            cells.Sort((a, b) =>
            {
                int distanceComparison = GetClosestDistanceToAnyCell(a, enemyCells)
                    .CompareTo(GetClosestDistanceToAnyCell(b, enemyCells));

                if (distanceComparison != 0)
                    return distanceComparison;

                int xComparison = a.x.CompareTo(b.x);
                return xComparison != 0 ? xComparison : a.y.CompareTo(b.y);
            });
        }

        /// <summary>
        /// Gets the Manhattan distance from one cell to the closest target cell.
        /// </summary>
        private static int GetClosestDistanceToAnyCell(Vector2Int cell, List<Vector2Int> targets)
        {
            if (targets.Count == 0)
                return 0;

            int closestDistance = int.MaxValue;

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                int distance = Mathf.Abs(cell.x - targets[targetIndex].x) + Mathf.Abs(cell.y - targets[targetIndex].y);
                if (distance < closestDistance)
                    closestDistance = distance;
            }

            return closestDistance;
        }

        /// <summary>
        /// Gets the opposing territory owner.
        /// </summary>
        private static TerritoryOwnership GetOpponentOwnership(TerritoryOwnership ownership)
        {
            return ownership == TerritoryOwnership.Player ? TerritoryOwnership.AI : TerritoryOwnership.Player;
        }

        /// <summary>
        /// Lazily creates the GUI styles used by the HUD.
        /// </summary>
        private void EnsureHudStyles()
        {
            if (_hudBoxStyle != null)
                return;

            _hudBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10)
            };

            _hudTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _hudTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            _hudWarningStyle = new GUIStyle(_hudTextStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.72f, 0.24f) }
            };
        }

        /// <summary>
        /// Shows a temporary HUD warning for an invalid selection.
        /// </summary>
        private void ShowSelectionMessage(string message)
        {
            _selectionMessage = message;
            _selectionMessageUntil = Time.time + _illegalSelectionMessageSeconds;
        }

        /// <summary>
        /// Clears the current temporary selection warning.
        /// </summary>
        private void ClearSelectionMessage()
        {
            _selectionMessage = null;
            _selectionMessageUntil = 0f;
        }

        /// <summary>
        /// Gets the warning text for an invalid selection in the current phase.
        /// </summary>
        private string GetIllegalSelectionMessage(Vector2Int position)
        {
            if (_game.Phase == TerritoryGamePhase.Setup)
            {
                if (_game.HasAIStartPlacement && position == _game.AIStartPlacement.AIStart)
                    return "AI start is unavailable.";

                return "Choose an empty starting tile.";
            }

            if (_game.Phase == TerritoryGamePhase.Complete)
                return "Game complete.";

            return "Choose a highlighted legal move.";
        }

        /// <summary>
        /// Gets the HUD label for a game phase.
        /// </summary>
        private static string GetPhaseLabel(TerritoryGamePhase phase)
        {
            return phase switch
            {
                TerritoryGamePhase.Setup => "Choose start",
                TerritoryGamePhase.PlayerTurn => "Player turn",
                TerritoryGamePhase.AITurn => "AI turn",
                TerritoryGamePhase.Resolving => "Resolving",
                TerritoryGamePhase.Complete => "Complete",
                _ => phase.ToString(),
            };
        }

        /// <summary>
        /// Gets the HUD label for the final winner.
        /// </summary>
        private static string GetWinnerLabel(TerritoryWinner winner)
        {
            return winner switch
            {
                TerritoryWinner.Player => "Player wins",
                TerritoryWinner.AI => "AI wins",
                TerritoryWinner.Tie => "Tie game",
                _ => "No winner",
            };
        }

        
    }
}
