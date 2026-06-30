using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Architecture.States;
using UnityEngine;

namespace hp55games.Mobile.Core.UIScripts.Overlays
{
    public class DebugToolOverlay : MonoBehaviour
    {
        private const float HEADER_HEIGHT = 60f;
        private const float CLOSE_BUTTON_SIZE = 50f;
        private const float SECTION_SPACING = 20f;
        private const float ITEM_HEIGHT = 40f;
        private const float SLIDER_WIDTH = 300f;
        
        private bool _isVisible = true;
        private bool _cheat1Enabled;
        private bool _cheat2Enabled;
        private bool _cheat3Enabled;
        
        private Rect _windowRect;
        private Vector2 _scrollPosition;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueLabelStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;
        
        private IGameStateMachine _stateMachine;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _currentAlpha = 0.9f;

        private void Awake()
        {
            if (ServiceRegistry.TryResolve<IGameStateMachine>(out _stateMachine))
            {
                Debug.Log("[DebugToolOverlay] GameStateMachine found and connected.");
            }
            else
            {
                Debug.LogWarning("[DebugToolOverlay] GameStateMachine not found in ServiceRegistry.");
            }
            
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            UpdatePanelSize();
        }

        private void Update()
        {
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
                UpdatePanelSize();
            }
        }

        private void UpdatePanelSize()
        {
            _windowRect = new Rect(0, 0, Screen.width, Screen.height);
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            InitializeStyles();
            _windowRect = GUILayout.Window(0, _windowRect, DrawDebugWindow, "", GUIStyle.none);
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(20, 20, 10, 10),
                normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, _currentAlpha)) }
            };

            _sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.8f, 1f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };

            _valueLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.5f, 1f, 0.5f) }
            };

            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(30, 0, 0, 0),
                normal = { textColor = Color.white },
                onNormal = { textColor = Color.green }
            };

            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.8f, 0.2f, 0.2f, _currentAlpha)) },
                hover = { textColor = Color.white, background = MakeTex(2, 2, new Color(1f, 0.3f, 0.3f, _currentAlpha)) }
            };

            _panelStyle = new GUIStyle(GUI.skin.box) 
            { 
                normal = { background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.05f, _currentAlpha)) } 
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 15, 15),
                normal = { background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, _currentAlpha * 0.7f)) }
            };

            _stylesInitialized = true;
        }

        private void UpdateAlpha()
        {
            _stylesInitialized = false;
        }

        private void DrawDebugWindow(int windowID)
        {
            GUILayout.BeginVertical(_panelStyle);

            DrawHeader();
            
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            DrawAlphaSlider();
            DrawStaticInfo();
            DrawCheatsSection();
            
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(_headerStyle, GUILayout.Height(HEADER_HEIGHT));
            
            GUILayout.Label("🛠 DEBUG TOOL", _headerStyle, GUILayout.ExpandWidth(true));
            
            if (GUILayout.Button("✕", _closeButtonStyle, GUILayout.Width(CLOSE_BUTTON_SIZE), GUILayout.Height(CLOSE_BUTTON_SIZE)))
            {
                ToggleVisibility();
            }
            
            GUILayout.EndHorizontal();
        }

        private void DrawAlphaSlider()
        {
            GUILayout.Space(SECTION_SPACING);
            
            GUILayout.BeginVertical(_boxStyle);
            
            GUILayout.Label("▼ TRANSPARENCY CONTROL", _sectionTitleStyle, GUILayout.Height(ITEM_HEIGHT));
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Opacity:", _labelStyle, GUILayout.Width(100));
            
            float newAlpha = GUILayout.HorizontalSlider(_currentAlpha, 0.1f, 1f, GUILayout.Width(SLIDER_WIDTH), GUILayout.Height(30));
            if (Mathf.Abs(newAlpha - _currentAlpha) > 0.01f)
            {
                _currentAlpha = newAlpha;
                UpdateAlpha();
            }
            
            GUILayout.Label($"{(_currentAlpha * 100f):F0}%", _valueLabelStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
        }

        private void DrawStaticInfo()
        {
            GUILayout.Space(SECTION_SPACING);
            
            GUILayout.BeginVertical(_boxStyle);
            
            GUILayout.Label("▼ SYSTEM INFORMATION", _sectionTitleStyle, GUILayout.Height(ITEM_HEIGHT));
            GUILayout.Space(10);

            DrawInfoRow("Game State:", GetCurrentGameState());
            DrawInfoRow("FSM Status:", GetFSMStatus());
            DrawInfoRow("FPS:", $"{(int)(1f / Time.deltaTime)}");
            DrawInfoRow("Time Scale:", $"{Time.timeScale:F2}");
            DrawInfoRow("Resolution:", $"{Screen.width} × {Screen.height}");
            DrawInfoRow("Platform:", Application.platform.ToString());
            
            GUILayout.EndVertical();
        }

        private void DrawInfoRow(string label, string value)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(ITEM_HEIGHT));
            GUILayout.Label(label, _labelStyle, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, _valueLabelStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawCheatsSection()
        {
            GUILayout.Space(SECTION_SPACING);
            
            GUILayout.BeginVertical(_boxStyle);
            
            GUILayout.Label("▼ CHEATS", _sectionTitleStyle, GUILayout.Height(ITEM_HEIGHT));
            GUILayout.Space(10);

            _cheat1Enabled = GUILayout.Toggle(_cheat1Enabled, "  Cheat 1", _toggleStyle, GUILayout.Height(ITEM_HEIGHT));

            _cheat2Enabled = GUILayout.Toggle(_cheat2Enabled, "  Cheat 2", _toggleStyle, GUILayout.Height(ITEM_HEIGHT));

            _cheat3Enabled = GUILayout.Toggle(_cheat3Enabled, "  Cheat 3", _toggleStyle, GUILayout.Height(ITEM_HEIGHT));

            GUILayout.EndVertical();
            
            GUILayout.Space(SECTION_SPACING);
        }

        private string GetCurrentGameState()
        {
            if (_stateMachine?.Current != null)
            {
                return _stateMachine.Current.GetType().Name;
            }
            return "No State";
        }

        private string GetFSMStatus()
        {
            if (_stateMachine == null) return "N/A";
            
            var type = _stateMachine.GetType();
            var field = type.GetField("_isTransitioning", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                bool isTransitioning = (bool)field.GetValue(_stateMachine);
                return isTransitioning ? "Transitioning" : "Stable";
            }
            
            return "Unknown";
        }

        private void OnCheat1Toggled(bool enabled)
        {
            Debug.Log($"Cheat 1 {(enabled ? "Enabled" : "Disabled")}");
        }

        private void OnCheat2Toggled(bool enabled)
        {
            Debug.Log($"Cheat 2 {(enabled ? "Enabled" : "Disabled")}");
        }

        private void OnCheat3Toggled(bool enabled)
        {
            Debug.Log($"Cheat 3 {(enabled ? "Enabled" : "Disabled")}");
        }

        public void ToggleVisibility()
        {
            _isVisible = !_isVisible;
        }

        private Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var result = new Texture2D(width, height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
    }
}
