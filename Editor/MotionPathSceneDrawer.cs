using UnityEditor;
using UnityEngine;
using PathMotion;

namespace PathMotion.Editor
{
    /// <summary>
    /// Draws every MotionPath curve in the Scene view at all times — independent of
    /// selection and independent of the Scene view "Gizmos" toggle.
    /// IMPORTANT: this class is DRAW-ONLY. It never handles mouse events or draws
    /// interactive handles, so it can never interfere with editing the path.
    /// (Double-click-to-add lives in MotionPathEditor, scoped to the selected path.)
    /// </summary>
    [InitializeOnLoad]
    internal static class MotionPathSceneDrawer
    {
        static MotionPathSceneDrawer()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var paths = Object.FindObjectsOfType<MotionPath>();
            if (paths == null || paths.Length == 0) return;

            foreach (var path in paths)
            {
                if (path == null || path.points == null || path.points.Length < 2) continue;

                int segCount = path.closed ? path.points.Length : path.points.Length - 1;
                int steps = Mathf.Max(2, path.samplesPerSegment * segCount);
                var pts = new Vector3[steps + 1];
                for (int s = 0; s <= steps; s++)
                    pts[s] = path.EvaluateWorld(s / (float)steps);

                Handles.color = new Color(1f, 0.55f, 0.1f, 0.9f);
                Handles.DrawAAPolyLine(3f, pts);
            }
        }
    }
}
