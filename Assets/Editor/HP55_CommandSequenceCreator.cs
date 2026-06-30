using System;
using System.Collections.Generic;
using hp55games.Mobile.Core.CommandSequence;
using UnityEditor;
using UnityEngine;

namespace hp55games.Editor.Tools
{
    public class HP55_CommandSequenceCreator : EditorWindow
    {
        private int _seed = 42;
        private bool _loop = true;
        private string _assetName = "NewLevelSequence";
        private string _savePath = "Assets/Content/CommandSequences";
        private float _beatInterval = 2f;
        private const string PrefsKeyPrefix = "hp55games.CommandSequenceCreator.";
        private const string PrefsKeySavePath = PrefsKeyPrefix + "SavePath";

        [SerializeField] private bool _catalogRandom = false;
        [SerializeField] private int _catalogNumberOfBeats = 0;
        
        private List<ScriptableObject> _catalogs = new List<ScriptableObject>();
        private SerializedObject _serializedThis;

        private SerializedObject _serializedSequence;
        private CommandSequenceAsset _workingSequence;

        private Vector2 _scrollPosition;
        private bool _useManualMode = false;

        // Cache of concrete ISequenceCommand types — built once via TypeCache
        private static List<Type>   _commandTypes;
        private static string[]     _commandTypeNames;

        [MenuItem("hp55games Tools/Command Sequence Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<HP55_CommandSequenceCreator>("Command Sequence Creator");
            window.minSize = new Vector2(450, 600);
        }

        private void OnEnable()
        {
            LoadPrefs();
            
            _serializedThis = new SerializedObject(this);
            CreateWorkingSequence();
            RefreshCommandTypes();
        }
        
        private void OnDisable()
        {
            SavePrefs();
        }
        
        private void LoadPrefs()
        {
            // Se è la prima volta (chiave assente) resta il default già presente in _savePath
            if (EditorPrefs.HasKey(PrefsKeySavePath))
                _savePath = EditorPrefs.GetString(PrefsKeySavePath, _savePath);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefsKeySavePath, _savePath ?? string.Empty);
        }

        private static void RefreshCommandTypes()
        {
            _commandTypes = new List<Type>();
            var names     = new List<string> { "(None)" };

            foreach (var t in TypeCache.GetTypesDerivedFrom<ISequenceCommand>())
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;

                _commandTypes.Add(t);
                names.Add(t.Name);
            }

            _commandTypeNames = names.ToArray();
        }

        private void CreateWorkingSequence()
        {
            _workingSequence    = CommandSequenceBuilder.CreateEmpty(_seed, _loop);
            _serializedSequence = new SerializedObject(_workingSequence);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);
            DrawHelpBox();
            EditorGUILayout.Space(10);
            DrawConfiguration();
            EditorGUILayout.Space(10);
            DrawModeSwitch();
            EditorGUILayout.Space(10);

            if (_useManualMode)
                DrawBeatsEditor();
            else
                DrawCatalogMode();

            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Label("Command Sequence Creator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Generic catalog-based sequence generation", EditorStyles.miniLabel);
        }

        private void DrawHelpBox()
        {
            EditorGUILayout.HelpBox(
                "CATALOG MODE (Recommended):\n" +
                "• Add ScriptableObject catalogs that implement ISequenceCommandCatalog\n" +
                "• Builder generates sequence automatically from catalogs\n" +
                "• Interval defines spacing between commands\n\n" +
                "MANUAL MODE:\n" +
                "• Add beats one by one manually\n" +
                "• Use the Command Type dropdown to pick a concrete command\n" +
                "• Fill in command fields once a type is selected",
                MessageType.Info);
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            
            EditorGUI.BeginChangeCheck();
            _savePath  = EditorGUILayout.TextField("Save Path", _savePath);
            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }

            EditorGUI.BeginChangeCheck();
            var newSeed = EditorGUILayout.IntField("Seed (Deterministic)", _seed);
            var newLoop = EditorGUILayout.Toggle("Loop", _loop);

            if (EditorGUI.EndChangeCheck())
            {
                _seed = newSeed;
                _loop = newLoop;
                if (_workingSequence != null)
                {
                    _workingSequence.Seed = _seed;
                    _workingSequence.Loop = _loop;
                    _serializedSequence?.Update();
                }
            }
        }

        private void DrawModeSwitch()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mode:", EditorStyles.boldLabel, GUILayout.Width(50));

            var catalogActive = GUILayout.Toggle(!_useManualMode, "Catalog", "Button", GUILayout.Width(100));
            if (catalogActive != !_useManualMode)
                _useManualMode = !catalogActive;

            var manualActive = GUILayout.Toggle(_useManualMode, "Manual", "Button", GUILayout.Width(100));
            if (manualActive != _useManualMode)
                _useManualMode = manualActive;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCatalogMode()
        {
            EditorGUILayout.LabelField("Catalog Setup", EditorStyles.boldLabel);

            _beatInterval = EditorGUILayout.FloatField("Beat Interval (seconds)", _beatInterval);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Catalogs (ISequenceCommandCatalog)", EditorStyles.boldLabel);

            _serializedThis.Update();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Count: {_catalogs.Count}", GUILayout.Width(100));
            if (GUILayout.Button("Add Catalog", GUILayout.Width(120)))
                _catalogs.Add(null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            for (int i = 0; i < _catalogs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var current = _catalogs[i];
                var picked = (ScriptableObject)EditorGUILayout.ObjectField(
                    $"Catalog {i}", current, typeof(ScriptableObject), false);

                if (picked != null && picked is not ISequenceCommandCatalog)
                {
                    EditorUtility.DisplayDialog(
                        "Catalog non valido",
                        $"'{picked.name}' non implementa ISequenceCommandCatalog.\n\nSeleziona uno ScriptableObject che implementi l'interfaccia.",
                        "OK");
                    picked = current;
                }

                _catalogs[i] = picked;

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _catalogs.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

            _catalogRandom = EditorGUILayout.ToggleLeft("Random (shuffle beats order)", _catalogRandom);

            _catalogNumberOfBeats = EditorGUILayout.IntField("Number of Beats (0 = all)", _catalogNumberOfBeats);
            if (_catalogNumberOfBeats < 0) _catalogNumberOfBeats = 0;

            _serializedThis.ApplyModifiedProperties();

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(_catalogs.Count == 0))
            {
                if (GUILayout.Button("Generate from Catalogs", GUILayout.Height(30)))
                    GenerateFromCatalogs();
            }
        }

        private void GenerateFromCatalogs()
        {
            var catalogs = new List<ISequenceCommandCatalog>();

            foreach (var so in _catalogs)
            {
                if (so == null) continue;

                if (so is ISequenceCommandCatalog c)
                    catalogs.Add(c);
                else
                    Debug.LogWarning($"Catalog {so.name} does not implement ISequenceCommandCatalog. Skipping.");
            }

            if (catalogs.Count == 0)
            {
                EditorUtility.DisplayDialog("Error",
                    "No valid catalogs found. Catalogs must implement ISequenceCommandCatalog.", "OK");
                return;
            }

            if (_beatInterval <= 0f)
            {
                EditorUtility.DisplayDialog("Error",
                    "Beat Interval must be > 0.", "OK");
                return;
            }

            var random = new System.Random(_seed);
            var context = new SequenceContext(null, random, 0f, 0);

            // 1) Collect all commands from catalogs
            var allCommands = new List<ISequenceCommand>();
            foreach (var catalog in catalogs)
            {
                if (catalog == null) continue;

                var commands = catalog.GetCommands(context);
                if (commands == null) continue;

                foreach (var cmd in commands)
                {
                    if (cmd != null)
                        allCommands.Add(cmd);
                }
            }

            if (allCommands.Count == 0)
            {
                EditorUtility.DisplayDialog("Error",
                    "Catalogs produced no commands. Cannot generate beats.", "OK");
                return;
            }

            // 2) Optional shuffle
            if (_catalogRandom)
            {
                for (int i = allCommands.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (allCommands[i], allCommands[j]) = (allCommands[j], allCommands[i]);
                }
            }

            // 3) Apply Number of Beats rule
            int targetCount = _catalogNumberOfBeats <= 0 ? allCommands.Count : _catalogNumberOfBeats;

            if (allCommands.Count > targetCount)
            {
                allCommands.RemoveRange(targetCount, allCommands.Count - targetCount);
            }
            else if (allCommands.Count < targetCount)
            {
                // Fill missing beats by picking random commands from the already generated pool
                int poolCount = allCommands.Count;
                while (allCommands.Count < targetCount)
                {
                    var picked = allCommands[random.Next(poolCount)];
                    allCommands.Add(picked);
                }

                // Extra shuffle if Random is enabled (so the appended items don't bias the tail)
                if (_catalogRandom)
                {
                    for (int i = allCommands.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (allCommands[i], allCommands[j]) = (allCommands[j], allCommands[i]);
                    }
                }
            }

            // 4) Build sequence with fixed beat interval times
            var sequence = CommandSequenceBuilder.CreateEmpty(_seed, _loop);
            float time = 0f;

            foreach (var cmd in allCommands)
            {
                CommandSequenceBuilder.AddBeat(sequence, time, cmd);
                time += _beatInterval;
            }

            _workingSequence = sequence;

            if (_workingSequence != null)
            {
                _serializedSequence = new SerializedObject(_workingSequence);
                _useManualMode = true;
                Debug.Log($"Generated {_workingSequence.Beats.Count} beats from {catalogs.Count} catalogs (Random={_catalogRandom}, TargetBeats={targetCount}).");
            }
        }

        

        private void DrawBeatsEditor()
        {
            EditorGUILayout.LabelField("Sequence Beats", EditorStyles.boldLabel);

            if (_serializedSequence == null || _workingSequence == null)
                CreateWorkingSequence();

            _serializedSequence.Update();

            var beatsProp = _serializedSequence.FindProperty("Beats");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Beats: {beatsProp.arraySize}", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Beat", GUILayout.Width(100)))
            {
                beatsProp.InsertArrayElementAtIndex(beatsProp.arraySize);
                var newBeat = beatsProp.GetArrayElementAtIndex(beatsProp.arraySize - 1);
                newBeat.FindPropertyRelative("Time").floatValue              = 0f;
                newBeat.FindPropertyRelative("Command").managedReferenceValue = null;
            }

            if (GUILayout.Button("Sort by Time", GUILayout.Width(100)))
            {
                CommandSequenceBuilder.SortBeatsByTime(_workingSequence);
                _serializedSequence.Update();
            }

            if (GUILayout.Button("Clear All", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Clear All Beats",
                    "Are you sure you want to remove all beats?", "Yes", "Cancel"))
                {
                    CommandSequenceBuilder.ClearBeats(_workingSequence);
                    _serializedSequence.Update();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            for (int i = 0; i < beatsProp.arraySize; i++)
            {
                if (i >= beatsProp.arraySize) break;
                DrawBeatElement(beatsProp, i);
            }

            _serializedSequence.ApplyModifiedProperties();
        }

        private void DrawBeatElement(SerializedProperty beatsProp, int index)
        {
            var beatProp    = beatsProp.GetArrayElementAtIndex(index);
            var timeProp    = beatProp.FindPropertyRelative("Time");
            var commandProp = beatProp.FindPropertyRelative("Command");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Header ────────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Beat {index}", EditorStyles.boldLabel, GUILayout.Width(55));

            if (GUILayout.Button("▲", GUILayout.Width(22)) && index > 0)
                beatsProp.MoveArrayElement(index, index - 1);

            if (GUILayout.Button("▼", GUILayout.Width(22)) && index < beatsProp.arraySize - 1)
                beatsProp.MoveArrayElement(index, index + 1);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("❐", GUILayout.Width(22)))
            {
                beatsProp.InsertArrayElementAtIndex(index + 1);
                var src  = beatsProp.GetArrayElementAtIndex(index);
                var copy = beatsProp.GetArrayElementAtIndex(index + 1);
                copy.FindPropertyRelative("Time").floatValue              = src.FindPropertyRelative("Time").floatValue;
                copy.FindPropertyRelative("Command").managedReferenceValue =
                    src.FindPropertyRelative("Command").managedReferenceValue;
            }

            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                beatsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
                return;
            }
            EditorGUILayout.EndHorizontal();

            // ── Time ──────────────────────────────────────────────────────────
            EditorGUILayout.PropertyField(timeProp, new GUIContent("Time (s)"));

            // ── Command Type Picker ───────────────────────────────────────────
            DrawCommandTypePicker(commandProp);

            // ── Command Fields ────────────────────────────────────────────────
            if (commandProp.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(commandProp, GUIContent.none, includeChildren: true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        private void DrawCommandTypePicker(SerializedProperty commandProp)
        {
            if (_commandTypes == null)
                RefreshCommandTypes();

            var currentType  = commandProp.managedReferenceValue?.GetType();
            int currentIndex = 0;

            if (currentType != null)
            {
                int found = _commandTypes.IndexOf(currentType);
                if (found >= 0) currentIndex = found + 1;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Command Type", GUILayout.Width(105));

            int newIndex = EditorGUILayout.Popup(currentIndex, _commandTypeNames);

            if (newIndex != currentIndex)
            {
                commandProp.managedReferenceValue = newIndex == 0
                    ? null
                    : Activator.CreateInstance(_commandTypes[newIndex - 1]);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(!CanSave());
            if (GUILayout.Button("Save Asset", GUILayout.Height(35)))
                SaveAsset();
            EditorGUI.EndDisabledGroup();

            if (!CanSave())
                EditorGUILayout.HelpBox(GetValidationMessage(), MessageType.Warning);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Reset Sequence", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Reset Sequence",
                    "This will clear all beats and reset configuration. Continue?", "Yes", "Cancel"))
                {
                    CreateWorkingSequence();
                }
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(_assetName)
                && !string.IsNullOrWhiteSpace(_savePath)
                && _workingSequence != null
                && _workingSequence.Beats.Count > 0;
        }

        private string GetValidationMessage()
        {
            if (string.IsNullOrWhiteSpace(_assetName))
                return "Asset Name is required.";
            if (string.IsNullOrWhiteSpace(_savePath))
                return "Save Path is required.";
            if (_workingSequence == null || _workingSequence.Beats.Count == 0)
                return "Sequence has no beats. Generate from catalogs or add beats manually.";
            return string.Empty;
        }

        private void SaveAsset()
        {
            _serializedSequence.ApplyModifiedProperties();

            var finalSequence = CommandSequenceBuilder.CreateEmpty(_seed, _loop);
            CommandSequenceBuilder.AddBeats(finalSequence, _workingSequence.Beats.ToArray());

            var fullPath = $"{_savePath}/{_assetName}.asset";
            System.IO.Directory.CreateDirectory(_savePath);
            AssetDatabase.CreateAsset(finalSequence, fullPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = finalSequence;

            Debug.Log($"Command Sequence asset created at {fullPath}");
            EditorUtility.DisplayDialog("Success",
                $"CommandSequenceAsset saved to:\n{fullPath}\n\nBeats: {finalSequence.Beats.Count}", "OK");
        }
    }
}
