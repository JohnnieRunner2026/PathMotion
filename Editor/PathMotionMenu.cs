using UnityEditor;
using UnityEngine;
using PathMotion;

namespace PathMotion.Editor
{
    /// <summary>
    /// Quick-start menu entries.
    /// GameObject > PathMotion > Create MotionPath (with curve)
    /// GameObject > PathMotion > Create Path + Mover Demo
    /// </summary>
    public static class PathMotionMenu
    {
        [MenuItem("GameObject/PathMotion/Create MotionPath (with curve)", false, 10)]
        static void CreatePath()
        {
            var go = new GameObject("MotionPath");
            Undo.RegisterCreatedObjectUndo(go, "Create MotionPath");

            var path = go.AddComponent<MotionPath>();
            path.points = MotionPath.DefaultPoints();
            EditorUtility.SetDirty(path);

            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/PathMotion/Create Path + Mover Demo", false, 11)]
        static void CreateDemo()
        {
            var pathGO = new GameObject("MotionPath");
            Undo.RegisterCreatedObjectUndo(pathGO, "Create MotionPath");

            var path = pathGO.AddComponent<MotionPath>();
            path.points = MotionPath.DefaultPoints();
            EditorUtility.SetDirty(path);

            var mover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mover.name = "Mover";
            Undo.RegisterCreatedObjectUndo(mover, "Create Mover");

            var player = mover.AddComponent<PathPlayer>();
            player.path = path;

            Selection.activeGameObject = mover;
        }
    }
}
