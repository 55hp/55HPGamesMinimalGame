using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace hp55games.Mobile.Core.CommandSequence.Editor
{
    [CustomEditor(typeof(CommandSequenceAsset))]
    public class CommandSequenceAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _seedProp;
        private SerializedProperty _startingDelayProp;
        private SerializedProperty _loopProp;
        private SerializedProperty _loopDelayProp;
        private SerializedProperty _beatsProp;

        // Cache of all concrete ISequenceCommand types found via TypeCache
        private static List<Type> _commandTypes;
        private static string[] _commandTypeNames;

        private void OnEnable()
        {
            _seedProp          = serializedObject.FindProperty("Seed");
            _startingDelayProp = serializedObject.FindProperty("StartingDelay");
            _loopProp          = serializedObject.FindProperty("Loop");
            _loopDelayProp     = serializedObject.FindProperty("LoopDelay");
            _beatsProp         = serializedObject.FindProperty("Beats");
            RefreshCommandTypes();
        }

        // Rebuild the list of concrete ISequenceCommand types.
        // TypeCache is fast — no reflection scan per frame.
        private static void RefreshCommandTypes()
        {
            _commandTypes = new List<Type>();
            var names = new List<string> { "(None)" };

            var found = TypeCache.GetTypesDerivedFrom<ISequenceCommand>();
            foreach (var t in found)
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;

                _commandTypes.Add(t);
                names.Add(t.Name);
            }

            _commandTypeNames = names.ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Command Sequence Asset — defines a time-ordered list of commands.\n" +
                "Seed: used for deterministic random in commands.\n" +
                "Starting Delay: pause before the first beat.\n" +
                "Loop: if true, the sequence restarts. Loop Delay: pause between loops.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_seedProp);
            EditorGUILayout.PropertyField(_startingDelayProp);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_loopProp);

            if (_loopProp.boolValue)
                EditorGUILayout.PropertyField(_loopDelayProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sequence Beats", EditorStyles.boldLabel);

            DrawBeatsArray();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBeatsArray()
        {
            int size = _beatsProp.arraySize;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Beats: {size}", EditorStyles.miniLabel);

            if (GUILayout.Button("+ Add Beat", GUILayout.Width(80)))
            {
                _beatsProp.InsertArrayElementAtIndex(size);
                // Force Command to null — InsertArrayElement copies the previous element
                var newBeat = _beatsProp.GetArrayElementAtIndex(size);
                newBeat.FindPropertyRelative("Command").managedReferenceValue = null;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            for (int i = 0; i < _beatsProp.arraySize; i++)
            {
                // arraySize can shrink mid-loop when a beat is deleted
                if (i >= _beatsProp.arraySize)
                    break;

                DrawBeatElement(i);
            }
        }

        private void DrawBeatElement(int index)
        {
            var beatProp    = _beatsProp.GetArrayElementAtIndex(index);
            var timeProp    = beatProp.FindPropertyRelative("Time");
            var commandProp = beatProp.FindPropertyRelative("Command");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Header: label + reorder + delete ─────────────────────────────
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"Beat {index}", EditorStyles.boldLabel, GUILayout.Width(55));

            if (GUILayout.Button("▲", GUILayout.Width(22)) && index > 0)
                _beatsProp.MoveArrayElement(index, index - 1);

            if (GUILayout.Button("▼", GUILayout.Width(22)) && index < _beatsProp.arraySize - 1)
                _beatsProp.MoveArrayElement(index, index + 1);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("❐", GUILayout.Width(22)))
            {
                var srcCommand = commandProp.managedReferenceValue;
                _beatsProp.InsertArrayElementAtIndex(index + 1);
                var copyBeat    = _beatsProp.GetArrayElementAtIndex(index + 1);
                copyBeat.FindPropertyRelative("Time").floatValue = timeProp.floatValue;

                // Deep-copy the command via JSON to get an independent instance
                if (srcCommand != null)
                {
                    var json  = JsonUtility.ToJson(srcCommand);
                    var clone = Activator.CreateInstance(srcCommand.GetType());
                    JsonUtility.FromJsonOverwrite(json, clone);
                    copyBeat.FindPropertyRelative("Command").managedReferenceValue = clone;
                }
                else
                {
                    copyBeat.FindPropertyRelative("Command").managedReferenceValue = null;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
                return;
            }

            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                _beatsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
                return;
            }

            EditorGUILayout.EndHorizontal();

            // ── Time field ────────────────────────────────────────────────────
            EditorGUILayout.PropertyField(timeProp, new GUIContent("Time (s)"));

            // ── Command type picker ───────────────────────────────────────────
            DrawCommandTypePicker(commandProp);

            // ── Command fields — shown only when a type is selected ───────────
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
            var    currentType  = commandProp.managedReferenceValue?.GetType();
            int    currentIndex = 0; // 0 = "(None)"

            if (currentType != null)
            {
                int found = _commandTypes.IndexOf(currentType);
                if (found >= 0)
                    currentIndex = found + 1; // offset by 1 because index 0 = "(None)"
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Command Type", GUILayout.Width(105));

            int newIndex = EditorGUILayout.Popup(currentIndex, _commandTypeNames);

            if (newIndex != currentIndex)
            {
                if (newIndex == 0)
                {
                    commandProp.managedReferenceValue = null;
                }
                else
                {
                    var selectedType = _commandTypes[newIndex - 1];
                    commandProp.managedReferenceValue = Activator.CreateInstance(selectedType);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
