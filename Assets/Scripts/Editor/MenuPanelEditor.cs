using UnityEditor;
using UnityEngine;

namespace VRRocket.Editor
{
    /// <summary>The menu's Inspector: where its words are edited for each state, with a preview switch for the canvas.</summary>
    [CustomEditor(typeof(MenuPanel))]
    public sealed class MenuPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Edit the menu's words here, not on the texts in the canvas: the game writes the canvas from these fields.\n" +
                "First Play is the menu at the start; After Success is the menu that comes back after a good flight.\n" +
                "Preview picks which one the canvas shows while you edit.", MessageType.Info);
            serializedObject.Update();
            var preview = serializedObject.FindProperty("m_Preview");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Preview");
                if (GUILayout.Toggle(preview.enumValueIndex == (int)MenuState.FirstPlay, "First Play", EditorStyles.miniButtonLeft)) preview.enumValueIndex = (int)MenuState.FirstPlay;
                if (GUILayout.Toggle(preview.enumValueIndex == (int)MenuState.AfterSuccess, "After Success", EditorStyles.miniButtonRight)) preview.enumValueIndex = (int)MenuState.AfterSuccess;
            }
            serializedObject.ApplyModifiedProperties();
            DrawPropertiesExcluding(serializedObject, "m_Script", "m_Preview");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
