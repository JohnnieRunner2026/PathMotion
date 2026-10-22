using UnityEditor;
using UnityEngine;
using PathMotion;

namespace PathMotion.Editor
{
    [CustomEditor(typeof(PathPlayer))]
    public class PathPlayerEditor : UnityEditor.Editor
    {
        PathPlayer player;

        void OnEnable() { player = (PathPlayer)target; }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview / Play (Editor)", EditorStyles.boldLabel);

            // Make the path situation obvious instead of silently doing nothing.
            var usedPath = player.ResolvedPath;
            if (player.path == null && usedPath != null)
            {
                EditorGUILayout.HelpBox(
                    "Path field is empty — auto-using \"" + usedPath.gameObject.name + "\" (" +
                    usedPath.PointCount + " points).", MessageType.Warning);
                if (GUILayout.Button("Assign that MotionPath to the Path field"))
                {
                    Undo.RecordObject(player, "Assign MotionPath");
                    player.path = usedPath;
                    EditorUtility.SetDirty(player);
                }
            }
            else if (usedPath == null)
            {
                EditorGUILayout.HelpBox(
                    "No MotionPath found in the scene. Create one: GameObject > PathMotion > Create MotionPath.",
                    MessageType.Error);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string state = usedPath == null
                    ? "NO PATH — nothing to preview"
                    : (player.previewInEditor
                        ? (Application.isPlaying ? "Playing (Play mode)" : "LIVE — moving in the Scene view now")
                        : "Paused");
                EditorGUILayout.LabelField(state, EditorStyles.miniBoldLabel);
            }

            EditorGUI.BeginChangeCheck();
            float pv = EditorGUILayout.Slider("Preview t", player.editorPreview, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                player.editorPreview = pv;
                player.SampleAtNormalizedTime(pv);
                SceneView.RepaintAll();
                EditorUtility.SetDirty(player);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(player.previewInEditor ? "Pause Live Preview" : "Play Live Preview"))
                {
                    player.previewInEditor = !player.previewInEditor;
                    if (!player.previewInEditor) player.RestorePose();
                    EditorUtility.SetDirty(player);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Rewind to Start"))
                {
                    player.playClock = 0f;
                    player.editorPreview = 0f;
                    player.SampleAtNormalizedTime(0f);
                    SceneView.RepaintAll();
                }
            }

            if (player.previewInEditor)
            {
                EditorGUILayout.HelpBox(
                    "Live preview runs automatically in the editor — no Play mode, no button.\n" +
                    "Drag / rotate the points on the MotionPath while it runs: the motion updates instantly, " +
                    "so you can judge speed and rhythm as you shape the curve.\n" +
                    "Lower 'Preview Speed' below 1 to watch it in slow motion.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Preview paused. The object was put back to where it started.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Export to AnimationClip (.anim)", GUILayout.Height(28)))
            {
                ExportClip();
            }
            EditorGUILayout.HelpBox(
                "Exports a legacy AnimationClip you can tweak in Unity's Animation window " +
                "and reuse on any object. It also attaches an Animation component with the clip " +
                "so you can play it immediately.", MessageType.Info);
        }

        void ExportClip()
        {
            if (player.ResolvedPath == null)
            {
                EditorUtility.DisplayDialog("Export", "No MotionPath found. Create or assign one first.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Export AnimationClip",
                "PathMotion_" + player.ResolvedTarget.name,
                "anim",
                "Save baked path animation");

            if (string.IsNullOrEmpty(path)) return;

            var clip = new AnimationClip
            {
                legacy = true,
                frameRate = 30f,
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };

            float dur = Mathf.Max(0.01f, player.duration);
            int frames = Mathf.Max(2, Mathf.RoundToInt(dur * clip.frameRate));

            var kx = new Keyframe[frames + 1];
            var ky = new Keyframe[frames + 1];
            var kz = new Keyframe[frames + 1];
            Keyframe[] rx = null, ry = null, rz = null, rw = null;

            Transform tgt = player.ResolvedTarget;
            MotionPath mp = player.ResolvedPath;
            bool bakeRot = player.orientToPath;

            for (int i = 0; i <= frames; i++)
            {
                float nt = i / (float)frames;
                float progress = Mathf.Clamp01(player.progressCurve.Evaluate(nt));
                Vector3 worldPos = mp.EvaluateWorld(progress);
                Vector3 localPos = (tgt.parent != null)
                    ? tgt.parent.InverseTransformPoint(worldPos)
                    : worldPos;
                float time = nt * dur;

                kx[i] = new Keyframe(time, localPos.x);
                ky[i] = new Keyframe(time, localPos.y);
                kz[i] = new Keyframe(time, localPos.z);

                if (bakeRot)
                {
                    Vector3 tan = mp.EvaluateTangentWorld(progress);
                    if (tan.sqrMagnitude > 1e-6f)
                    {
                        Quaternion worldRot = Quaternion.LookRotation(tan, player.upVector);
                        Quaternion localRot = (tgt.parent != null)
                            ? Quaternion.Inverse(tgt.parent.rotation) * worldRot
                            : worldRot;
                        if (rx == null)
                        {
                            rx = new Keyframe[frames + 1];
                            ry = new Keyframe[frames + 1];
                            rz = new Keyframe[frames + 1];
                            rw = new Keyframe[frames + 1];
                        }
                        rx[i] = new Keyframe(time, localRot.x);
                        ry[i] = new Keyframe(time, localRot.y);
                        rz[i] = new Keyframe(time, localRot.z);
                        rw[i] = new Keyframe(time, localRot.w);
                    }
                }
            }

            var curveX = new AnimationCurve(kx);
            var curveY = new AnimationCurve(ky);
            var curveZ = new AnimationCurve(kz);
            curveX = SetTangents(curveX, AnimationUtility.TangentMode.Auto);
            curveY = SetTangents(curveY, AnimationUtility.TangentMode.Auto);
            curveZ = SetTangents(curveZ, AnimationUtility.TangentMode.Auto);

            const string relPath = ""; // clip animates the object it is attached to
            clip.SetCurve(relPath, typeof(Transform), "localPosition.x", curveX);
            clip.SetCurve(relPath, typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve(relPath, typeof(Transform), "localPosition.z", curveZ);

            if (bakeRot && rx != null)
            {
                var cRx = new AnimationCurve(rx);
                var cRy = new AnimationCurve(ry);
                var cRz = new AnimationCurve(rz);
                var cRw = new AnimationCurve(rw);
                cRx = SetTangents(cRx, AnimationUtility.TangentMode.Auto);
                cRy = SetTangents(cRy, AnimationUtility.TangentMode.Auto);
                cRz = SetTangents(cRz, AnimationUtility.TangentMode.Auto);
                cRw = SetTangents(cRw, AnimationUtility.TangentMode.Auto);
                clip.SetCurve(relPath, typeof(Transform), "localRotation.x", cRx);
                clip.SetCurve(relPath, typeof(Transform), "localRotation.y", cRy);
                clip.SetCurve(relPath, typeof(Transform), "localRotation.z", cRz);
                clip.SetCurve(relPath, typeof(Transform), "localRotation.w", cRw);
            }

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            // Attach an Animation component so the clip is immediately playable.
            var go = tgt.gameObject;
            var anim = go.GetComponent<Animation>();
            if (anim == null)
            {
                Undo.RecordObject(go, "Add Animation");
                anim = go.AddComponent<Animation>();
            }
            if (anim.GetClip(clip.name) != null)
                anim.RemoveClip(clip.name);
            anim.AddClip(clip, clip.name);
            anim.clip = clip;

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            EditorUtility.DisplayDialog("Export", "AnimationClip exported to:\n" + path, "OK");
        }

        static AnimationCurve SetTangents(AnimationCurve c, AnimationUtility.TangentMode mode)
        {
            for (int i = 0; i < c.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, mode);
                AnimationUtility.SetKeyRightTangentMode(c, i, mode);
            }
            return c;
        }
    }
}
