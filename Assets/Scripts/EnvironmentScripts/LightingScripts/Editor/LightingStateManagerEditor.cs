using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LightingStateManager))]
public class LightingStateManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LightingStateManager manager = (LightingStateManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Transiciones", EditorStyles.boldLabel);

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("→ Tapat (snap)"))
            manager.SnapToState(LightingStateManager.LightingState.Tapat);

        if (GUILayout.Button("→ Radiografia"))
            manager.TransitionTo(LightingStateManager.LightingState.Radiografia);

        if (GUILayout.Button("→ Lluna"))
            manager.TransitionTo(LightingStateManager.LightingState.Lluna);

        if (GUILayout.Button("→ Azul (snap)"))
            manager.SnapToState(LightingStateManager.LightingState.Blue);

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Entra en Play Mode para usar los botones.", MessageType.Info);

        GUI.enabled = true;
    }
}