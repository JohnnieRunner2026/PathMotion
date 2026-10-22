using UnityEditor;
using UnityEngine;
using PathMotion;

namespace PathMotion.Editor
{
    [CustomEditor(typeof(MotionPath))]
    public class MotionPathEditor : UnityEditor.Editor
    {
        MotionPath path;
        int selected = -1;
        const float k_DotSize = 0.12f;
        const int k_ArrowCount = 4;

        void OnEnable()
        {
            path = (MotionPath)target;
            int before = path.rotations != null ? path.rotations.Length : -1;
            path.SyncRotations();
            if (path.rotations.Length != before) EditorUtility.SetDirty(path);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Path Editing", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point at End"))
                {
                    Undo.RecordObject(path, "Add Path Point");
                    Vector3 last = path.PointCount > 0 ? path.points[path.PointCount - 1] : Vector3.zero;
                    path.AddPoint(last + Vector3.right);
                    selected = path.PointCount - 1;
                    EditorUtility.SetDirty(path);
                }
                if (GUILayout.Button("Insert After Sel"))
                {
                    if (selected >= 0 && path.PointCount > 0)
                    {
                        Undo.RecordObject(path, "Insert Path Point");
                        Vector3 a = path.points[selected];
                        Vector3 b = path.points[Mathf.Min(selected + 1, path.PointCount - 1)];
                        path.InsertPoint(selected + 1, (a + b) * 0.5f);
                        selected++;
                        EditorUtility.SetDirty(path);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(selected < 0 || path.PointCount <= 1);
                if (GUILayout.Button("Delete Selected"))
                {
                    Undo.RecordObject(path, "Delete Path Point");
                    path.RemovePoint(selected);
                    selected = Mathf.Clamp(selected, 0, path.PointCount - 1);
                    EditorUtility.SetDirty(path);
                }
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Clear All"))
                {
                    Undo.RecordObject(path, "Clear Path");
                    path.ClearPoints();
                    selected = -1;
                    EditorUtility.SetDirty(path);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to Default Curve"))
                {
                    Undo.RecordObject(path, "Reset Path Curve");
                    path.ResetToDefault();
                    selected = -1;
                    EditorUtility.SetDirty(path);
                }
                if (GUILayout.Button("Auto Tangents"))
                {
                    Undo.RecordObject(path, "Auto Tangents");
                    path.AutoComputeRotations();
                    EditorUtility.SetDirty(path);
                }
            }

            if (path.PointCount < 2)
                EditorGUILayout.HelpBox("This path has no curve yet. Click \"Reset to Default Curve\" (or Add Point at End) to create one.", MessageType.Warning);

            if (selected >= 0 && selected < path.PointCount)
            {
                Vector3 rv = (selected < path.rotations.Length) ? path.rotations[selected].eulerAngles : Vector3.zero;

                EditorGUI.BeginChangeCheck();
                Vector3 lv = EditorGUILayout.Vector3Field("Selected Point (local pos)", path.points[selected]);
                rv = EditorGUILayout.Vector3Field("Selected Point (rotation deg)", rv);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(path, "Edit Path Point");
                    path.SetPointLocal(selected, lv);
                    path.SetPointRotationLocal(selected, Quaternion.Euler(rv));
                    EditorUtility.SetDirty(path);
                }
            }

            EditorGUILayout.LabelField("Length (approx)", path.EstimateLength().ToString("F2"));
            EditorGUILayout.HelpBox(
                "Click a blue dot to select a point, then use the Unity Move (W) / Rotate (E) tool on it. " +
                "In Hermite mode, rotating a point bends the curve. Double-click the orange curve to add a point.",
                MessageType.Info);
        }

        void OnSceneGUI()
        {
            if (path.points == null) return;
            path.SyncRotations();

            // Double-click on the curve inserts a new waypoint (never break the handles).
            try { TryAddPointOnDoubleClick(); }
            catch (System.Exception ex) { Debug.LogException(ex); }

            DrawCurve();
            DrawSelectionDots();
            DrawSelectedTool();
        }

        void DrawCurve()
        {
            if (path.PointCount < 2) return;

            int segCount = path.closed ? path.PointCount : path.PointCount - 1;
            int steps = Mathf.Max(2, path.samplesPerSegment * segCount);
            var pts = new Vector3[steps + 1];
            for (int s = 0; s <= steps; s++)
                pts[s] = path.EvaluateWorld(s / (float)steps);

            Handles.color = new Color(1f, 0.55f, 0.1f, 1f); // bright orange
            Handles.DrawAAPolyLine(4f, pts);

            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            for (int a = 1; a <= k_ArrowCount; a++)
            {
                float t = a / (k_ArrowCount + 1f);
                Vector3 pos = path.EvaluateWorld(t);
                Vector3 tan = path.EvaluateTangentWorld(t);
                if (tan.sqrMagnitude > 0.0001f)
                {
                    float size = HandleUtility.GetHandleSize(pos) * 0.35f;
                    Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(tan), size, EventType.Repaint);
                }
            }
        }

        void DrawSelectionDots()
        {
            for (int i = 0; i < path.PointCount; i++)
            {
                Vector3 world = path.transform.TransformPoint(path.points[i]);
                float size = HandleUtility.GetHandleSize(world) * k_DotSize;

                if (i == selected)
                {
                    // marker only; the move/rotate tool below drives this point
                    Handles.color = Color.green;
                    Handles.SphereHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
                }
                else
                {
                    Handles.color = new Color(0.2f, 0.8f, 1f);
                    if (Handles.Button(world, Quaternion.identity, size, size, Handles.SphereHandleCap))
                    {
                        selected = i;
                        Repaint();
                    }
                }

                Handles.Label(world + Vector3.up * size * 1.5f, i.ToString());
            }
        }

        void DrawSelectedTool()
        {
            if (selected < 0 || selected >= path.PointCount) return;

            Vector3 world = path.transform.TransformPoint(path.points[selected]);
            Quaternion worldRot = path.transform.rotation * path.rotations[selected];
            Quaternion axesRot = (Tools.pivotRotation == PivotRotation.Local) ? worldRot : Quaternion.identity;

            EditorGUI.BeginChangeCheck();
            Vector3 nw = world;
            Quaternion nr = worldRot;

            switch (Tools.current)
            {
                case Tool.Rotate:
                    nr = Handles.DoRotationHandle(worldRot, world);
                    break;
                case Tool.Transform: // combined move+rotate
                    nw = Handles.DoPositionHandle(world, axesRot);
                    nr = Handles.DoRotationHandle(worldRot, world);
                    break;
                case Tool.Move:
                case Tool.Rect:
                case Tool.Scale:
                default:
                    nw = Handles.DoPositionHandle(world, axesRot);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(path, "Edit Path Point");
                if (nw != world) path.SetPointLocal(selected, path.transform.InverseTransformPoint(nw));
                if (nr != worldRot) path.SetPointRotationLocal(selected, Quaternion.Inverse(path.transform.rotation) * nr);
                EditorUtility.SetDirty(path);
            }
        }

        void TryAddPointOnDoubleClick()
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type != EventType.MouseDown || e.button != 0 || e.clickCount != 2) return;
            if (path.PointCount < 2) return;

            Vector2 mouse = e.mousePosition;

            // Don't add when the double-click lands on an existing handle.
            foreach (var lp in path.points)
            {
                Vector2 sp = HandleUtility.WorldToGUIPoint(path.transform.TransformPoint(lp));
                if ((sp - mouse).sqrMagnitude <= 12f * 12f) return;
            }

            int segCount = path.closed ? path.PointCount : path.PointCount - 1;
            int steps = Mathf.Max(2, path.samplesPerSegment * segCount);

            float bestDist = float.MaxValue;
            float bestT = 0f;
            Vector2 prevS = HandleUtility.WorldToGUIPoint(path.EvaluateWorld(0f));

            for (int i = 1; i <= steps; i++)
            {
                float t1 = i / (float)steps;
                Vector2 curS = HandleUtility.WorldToGUIPoint(path.EvaluateWorld(t1));

                Vector2 ab = curS - prevS;
                float len2 = ab.sqrMagnitude;
                float u = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(mouse - prevS, ab) / len2) : 0f;
                Vector2 closest = prevS + ab * u;
                float d = (mouse - closest).magnitude;

                if (d < bestDist)
                {
                    bestDist = d;
                    bestT = ((i - 1) + u) / steps;
                }
                prevS = curS;
            }

            if (bestDist > 14f) return;

            Undo.RecordObject(path, "Add Path Point (Double-Click)");
            int insertIndex = path.InsertPointAt(bestT);
            if (insertIndex < 0) return;

            if (PrefabUtility.IsPartOfPrefabInstance(path))
                PrefabUtility.RecordPrefabInstancePropertyModifications(path);

            selected = insertIndex;
            EditorUtility.SetDirty(path);
            Repaint();
            e.Use();
        }
    }
}
