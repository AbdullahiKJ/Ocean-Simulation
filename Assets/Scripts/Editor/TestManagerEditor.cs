using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(TestManager))]
public class TestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw normal inspector properties
        DrawDefaultInspector();

        // Get reference to the target script
        TestManager manager = (TestManager)target;

        GUILayout.Space(10);

        // Create button
        if (GUILayout.Button("Run Active Test"))
        {
            manager.RunActiveTest();
        }
    }
}