using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(OceanManager))]
public class OceanManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw normal inspector properties
        DrawDefaultInspector();

        // Get reference to the target script
        OceanManager manager = (OceanManager)target;

        GUILayout.Space(10);

        // Create button
        if (GUILayout.Button("Re-Initialise Ocean"))
        {
            manager.Initialise();
        }
    }
}