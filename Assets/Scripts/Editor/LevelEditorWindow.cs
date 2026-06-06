#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BlockJam.Data;
using BlockJam.Gameplay;

namespace BlockJam.EditorTools
{
    /// <summary>
    /// Custom in-editor level designer (the case asks for an in-editor tool that can create
    /// new levels AND edit existing ones, producing 5+ unique levels).
    ///
    /// Features:
    ///  - Pick any existing LevelData asset (or create a new one) and paint its grid visually.
    ///  - Resize the grid; cells are preserved where possible.
    ///  - Paint modes: set color, toggle empty (hole), toggle box blocker.
    ///  - Live validation: warns if any color count isn't a multiple of 3 (unsolvable),
    ///    and shows total unit count - so you can guarantee at least one level supports
    ///    BOTH a win path and a fail path (enough units to overflow 7 slots if mis-played).
    ///
    /// Open via: Tools > BlockJam > Level Editor
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        private LevelData _level;
        private ColorPalette _palette;
        private GameConfig _config;

        private enum PaintMode { SetColor, ToggleEmpty, ToggleBox }
        private PaintMode _mode = PaintMode.SetColor;
        private UnitColor _brushColor = UnitColor.Red;

        private Vector2 _scroll;
        private const float CellSize = 34f;

        [MenuItem("Tools/BlockJam/Level Editor")]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("BlockJam Level Editor");
            w.minSize = new Vector2(420, 480);
            w.Show();
        }

        private void OnGUI()
        {
            DrawAssetPickers();
            if (_level == null)
            {
                EditorGUILayout.HelpBox("Select or create a LevelData asset to start editing.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            DrawSizeControls();
            EditorGUILayout.Space();
            DrawPaintControls();
            EditorGUILayout.Space();
            DrawGrid();
            EditorGUILayout.Space();
            DrawValidation();
        }

        private void DrawAssetPickers()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _level = (LevelData)EditorGUILayout.ObjectField("Level Data", _level, typeof(LevelData), false);
            _palette = (ColorPalette)EditorGUILayout.ObjectField("Color Palette", _palette, typeof(ColorPalette), false);
            _config = (GameConfig)EditorGUILayout.ObjectField("Game Config", _config, typeof(GameConfig), false);
            if (EditorGUI.EndChangeCheck() && _level != null) _level.EnsureSize();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Level Asset..."))
                    CreateNewLevel();

                // Adds the level currently being edited to the GameConfig's level list, so a
                // freshly created level is immediately playable without manual dragging.
                using (new EditorGUI.DisabledScope(_config == null || _level == null))
                {
                    if (GUILayout.Button("Add Current Level to Config"))
                        AddLevelToConfig(_level);
                }
            }

            if (_config == null)
                EditorGUILayout.HelpBox(
                    "Assign a Game Config to enable one-click 'create & add', so new levels become " +
                    "playable immediately (otherwise add the level to GameConfig.Levels manually).",
                    MessageType.None);
        }

        private void CreateNewLevel()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Level", "Level_New", "asset", "Choose where to save the new level");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<LevelData>();
            asset.EnsureSize();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _level = asset;
            Selection.activeObject = asset;

            // If a config is assigned, append the new level right away so it's playable at once.
            if (_config != null) AddLevelToConfig(asset);
        }

        /// <summary>Appends a level to the GameConfig's Levels array (if not already present) and
        /// persists the change, so the level designer can create a level and play it immediately.</summary>
        private void AddLevelToConfig(LevelData level)
        {
            if (_config == null || level == null) return;

            var list = new System.Collections.Generic.List<LevelData>();
            if (_config.Levels != null) list.AddRange(_config.Levels);
            if (list.Contains(level))
            {
                EditorUtility.DisplayDialog("Already Added",
                    $"'{level.name}' is already in the Game Config.", "OK");
                return;
            }

            Undo.RecordObject(_config, "Add Level to Config");
            list.Add(level);
            _config.Levels = list.ToArray();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelEditor] Added '{level.name}' to {_config.name} (now {list.Count} levels).");
        }

        private void DrawSizeControls()
        {
            EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int w = EditorGUILayout.IntSlider("Width", _level.Width, 1, 12);
            int h = EditorGUILayout.IntSlider("Height", _level.Height, 1, 12);
            int num = EditorGUILayout.IntField("Level Number", _level.LevelNumber);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_level, "Edit Level Size");
                _level.Width = Mathf.Max(1, w);
                _level.Height = Mathf.Max(1, h);
                _level.LevelNumber = Mathf.Max(1, num);
                _level.EnsureSize();
                EditorUtility.SetDirty(_level);
            }
        }

        private void DrawPaintControls()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            _mode = (PaintMode)EditorGUILayout.EnumPopup("Mode", _mode);
            if (_mode == PaintMode.SetColor)
                _brushColor = (UnitColor)EditorGUILayout.EnumPopup("Color", _brushColor);

            EditorGUILayout.HelpBox(
                "Click a cell to apply the brush.\n" +
                "SetColor: makes the cell an occupied unit of the chosen color.\n" +
                "ToggleEmpty: turns a cell into a hole (no unit) or back.\n" +
                "ToggleBox: wraps/unwraps the unit in a Box blocker.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill All (brush color)")) FillAll();
                if (GUILayout.Button("Clear All (holes)")) ClearAll();
            }
        }

        private void DrawGrid()
        {
            EditorGUILayout.LabelField("Grid (row 0 = bottom)", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(CellSize * Mathf.Min(_level.Height, 12) + 20));

            // Draw top row first so it reads like the in-game board (y increases upward).
            for (int y = _level.Height - 1; y >= 0; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int x = 0; x < _level.Width; x++)
                        DrawCellButton(x, y);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCellButton(int x, int y)
        {
            CellData cell = _level.GetCell(x, y);
            Color prev = GUI.backgroundColor;

            if (!cell.Occupied) GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            else GUI.backgroundColor = _palette != null ? _palette.Get(cell.Color) : Color.white;

            string label = !cell.Occupied ? "" : (cell.InBox ? "□" : "●");
            if (GUILayout.Button(label, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                ApplyBrush(x, y);

            GUI.backgroundColor = prev;
        }

        private void ApplyBrush(int x, int y)
        {
            Undo.RecordObject(_level, "Paint Cell");
            CellData cell = _level.GetCell(x, y);

            switch (_mode)
            {
                case PaintMode.SetColor:
                    cell = CellData.Of(_brushColor, cell.InBox);
                    break;
                case PaintMode.ToggleEmpty:
                    cell = cell.Occupied ? CellData.Empty : CellData.Of(_brushColor);
                    break;
                case PaintMode.ToggleBox:
                    if (cell.Occupied) cell.InBox = !cell.InBox;
                    break;
            }

            _level.SetCell(x, y, cell);
            EditorUtility.SetDirty(_level);
        }

        private void FillAll()
        {
            Undo.RecordObject(_level, "Fill All");
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                _level.SetCell(x, y, CellData.Of(_brushColor));
            EditorUtility.SetDirty(_level);
        }

        private void ClearAll()
        {
            Undo.RecordObject(_level, "Clear All");
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                _level.SetCell(x, y, CellData.Empty);
            EditorUtility.SetDirty(_level);
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            int units = _level.CountUnits();
            EditorGUILayout.LabelField($"Total units: {units}");

            if (_level.ValidateColorCounts(out string error))
                EditorGUILayout.HelpBox("Solvable: every color count is a multiple of 3.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(error, MessageType.Error);

            if (units > 7)
                EditorGUILayout.HelpBox(
                    "This level has >7 units, so a careless play order can overflow the 7-slot bar: " +
                    "good for demonstrating BOTH win and fail cases.", MessageType.None);

            if (GUILayout.Button("Save Asset")) AssetDatabase.SaveAssets();
        }
    }
}
#endif