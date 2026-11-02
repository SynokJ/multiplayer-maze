using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MazeController))]
public class MazeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (MazeController)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate"))
            Run(controller, "Generate Maze", controller.Generate);

        if (GUILayout.Button("Random Seed + Generate"))
            Run(controller, "Generate Maze (random seed)", () =>
            {
                controller.SetDefaultSeed(Random.Range(int.MinValue, int.MaxValue));
                controller.Generate();
            });

        if (GUILayout.Button("Clear"))
            Run(controller, "Clear Maze", controller.Clear);

        if (controller.Data != null)
        {
            var data = controller.Data;
            var farthest = data.FindFarthestCell(data.width / 2, data.height / 2);
            EditorGUILayout.HelpBox(
                $"Seed {data.seed}, {data.width}x{data.height}\n" +
                $"Farthest cell from center: [{farthest.x}, {farthest.y}], {farthest.distance} steps",
                MessageType.Info);
        }
    }

    private static void Run(MazeController controller, string undoName, System.Action action)
    {
        Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, undoName);
        action();
        EditorUtility.SetDirty(controller);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }
}
