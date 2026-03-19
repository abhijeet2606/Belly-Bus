
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissionUIManager))]
public class MissionUIManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Use the button below to forcefully delete all mission progress, claims, and the local cache file. This is useful for testing from a clean state.", MessageType.Info);

        MissionUIManager script = (MissionUIManager)target;
        
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // A reddish color to indicate a destructive action
        if (GUILayout.Button("Force Reset All Mission Data"))
        {
            if (EditorUtility.DisplayDialog("Confirm Reset",
                "Are you sure you want to delete all mission progress, claims, and the cache file? This cannot be undone.",
                "Yes, Reset Data", "Cancel"))
            {
                script.ForceResetAllMissionProgress();
                EditorUtility.DisplayDialog("Reset Complete", "All mission data has been cleared.", "OK");
            }
        }
        GUI.backgroundColor = Color.white;
    }
}
