#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ChessCards
{
    [CustomEditor(typeof(ChessCardsScreen))]
    public sealed class ChessCardsScreenEditor : Editor
    {
        bool portrait;
        int layer;
        int selected = -1;
        int dragControl;
        bool resizing;
        Rect dragOrigin;
        Vector2 dragStart;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Kind"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("PreviewMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ThemeOverride"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Screen Layout", EditorStyles.boldLabel);
            bool nextPortrait = GUILayout.Toolbar(portrait ? 1 : 0, new[] { "Landscape 1280 × 720", "Portrait 720 × 1280" }) == 1;
            if (nextPortrait != portrait) selected = -1;
            portrait = nextPortrait;
            var layers = ((ChessCardsScreen)target).Kind == ScreenKind.Courtyard
                ? new[] { "Screen", "Settings", "Mode picker" }
                : new[] { "Screen", "Settings", "Go scoring" };
            int nextLayer = EditorGUILayout.Popup("Preview layer", Mathf.Min(layer, layers.Length - 1), layers);
            if (nextLayer != layer) selected = -1;
            layer = nextLayer;
            var placements = serializedObject.FindProperty("Placements");
            DrawLayout(placements);

            EditorGUILayout.HelpBox("Drag a block to move it. Drag the lower-right handle to resize it. The preview shows layout bounds.", MessageType.Info);
            EditorGUILayout.PropertyField(placements, true);
            serializedObject.ApplyModifiedProperties();
        }

        void DrawLayout(SerializedProperty placements)
        {
            float designWidth = portrait ? 720 : 1280, designHeight = portrait ? 1280 : 720;
            float previewHeight = portrait ? 390 : 265;
            Rect available = EditorGUILayout.GetControlRect(false, previewHeight);
            float scale = Mathf.Min(available.width / designWidth, previewHeight / designHeight);
            if (scale <= 0) return;
            Rect preview = new Rect(available.center.x - designWidth * scale / 2, available.y, designWidth * scale, designHeight * scale);
            EditorGUI.DrawRect(preview, new Color(.13f, .08f, .18f));

            int id = GUIUtility.GetControlID(FocusType.Passive, preview);
            Event evt = Event.current;
            int hit = -1;
            for (int i = 0; i < placements.arraySize; i++)
            {
                if (!Visible(PlacementKind(placements, i))) continue;
                Rect rect = ToPreview(PlacementRect(placements, i).rectValue, preview, scale);
                if (rect.Contains(evt.mousePosition)) hit = i;
            }

            if (selected >= placements.arraySize || (selected >= 0 && !Visible(PlacementKind(placements, selected)))) selected = -1;
            if (evt.type == EventType.MouseDown && evt.button == 0 && preview.Contains(evt.mousePosition))
            {
                resizing = selected >= 0 && Handle(ToPreview(PlacementRect(placements, selected).rectValue, preview, scale)).Contains(evt.mousePosition);
                if (!resizing) selected = hit;
                if (selected >= 0)
                {
                    dragStart = evt.mousePosition;
                    dragOrigin = PlacementRect(placements, selected).rectValue;
                    dragControl = GUIUtility.hotControl = id;
                }
                evt.Use();
                Repaint();
            }

            if (GUIUtility.hotControl == id && dragControl == id && selected >= 0)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    Vector2 delta = (evt.mousePosition - dragStart) / scale;
                    Rect edited = dragOrigin;
                    if (resizing)
                    {
                        edited.width = Mathf.Clamp(dragOrigin.width + delta.x, 20, designWidth - dragOrigin.x);
                        edited.height = Mathf.Clamp(dragOrigin.height + delta.y, 20, designHeight - dragOrigin.y);
                    }
                    else
                    {
                        edited.x = Mathf.Clamp(dragOrigin.x + delta.x, 0, Mathf.Max(0, designWidth - dragOrigin.width));
                        edited.y = Mathf.Clamp(dragOrigin.y + delta.y, 0, Mathf.Max(0, designHeight - dragOrigin.height));
                    }
                    PlacementRect(placements, selected).rectValue = edited;
                    evt.Use();
                    Repaint();
                }
                if (evt.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    dragControl = 0;
                    evt.Use();
                }
            }

            if (evt.type != EventType.Repaint) return;
            for (int i = 0; i < placements.arraySize; i++)
                if (i != selected && Visible(PlacementKind(placements, i))) DrawBlock(placements, i, preview, scale, false);
            if (selected >= 0) DrawBlock(placements, selected, preview, scale, true);
        }

        bool Visible(ScreenBlock block)
        {
            if (layer == 1) return block == ScreenBlock.SettingsWindow;
            bool match = ((ChessCardsScreen)target).Kind == ScreenKind.Match;
            if (layer == 2) return match
                ? block == ScreenBlock.Scoring || block == ScreenBlock.Actions
                : block == ScreenBlock.ModePicker;
            return block != ScreenBlock.SettingsWindow && block != ScreenBlock.ModePicker && block != ScreenBlock.Scoring;
        }

        SerializedProperty PlacementRect(SerializedProperty placements, int index)
        { return placements.GetArrayElementAtIndex(index).FindPropertyRelative(portrait ? "Portrait" : "Landscape"); }

        static ScreenBlock PlacementKind(SerializedProperty placements, int index)
        { return (ScreenBlock)placements.GetArrayElementAtIndex(index).FindPropertyRelative("Block").enumValueIndex; }

        static Rect ToPreview(Rect rect, Rect preview, float scale)
        { return new Rect(preview.x + rect.x * scale, preview.y + rect.y * scale, rect.width * scale, rect.height * scale); }

        static Rect Handle(Rect rect) { return new Rect(rect.xMax - 8, rect.yMax - 8, 8, 8); }

        void DrawBlock(SerializedProperty placements, int index, Rect preview, float scale, bool active)
        {
            Rect rect = ToPreview(PlacementRect(placements, index).rectValue, preview, scale);
            var block = placements.GetArrayElementAtIndex(index).FindPropertyRelative("Block");
            EditorGUI.DrawRect(rect, active ? new Color(.80f, .53f, .15f, .68f) : new Color(.39f, .23f, .60f, .64f));
            GUI.Label(rect, block.enumDisplayNames[block.enumValueIndex], EditorStyles.centeredGreyMiniLabel);
            if (active) EditorGUI.DrawRect(Handle(rect), Color.white);
        }
    }
}
#endif
