using UnityEditor;
using UnityEngine;
using PathMotion;

namespace PathMotion.Editor
{
    /// <summary>
    /// Global editor ticker that drives every PathPlayer whose "Preview In Editor" toggle is on.
    /// Because it is NOT tied to a custom inspector, the preview keeps running while you
    /// select and edit the MotionPath (drag points, rotate them) — you see the motion live,
    /// with no Play mode needed.
    /// </summary>
    [InitializeOnLoad]
    internal static class PathPlayerPreviewTicker
    {
        static double s_last;
        static double s_nextScan;
        static PathPlayer[] s_cache;

        static PathPlayerPreviewTicker()
        {
            s_last = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>
        /// The live preview hijacks the target's transform, so put everything back
        /// before Play mode starts (otherwise the object would begin play stranded
        /// somewhere on the path).
        /// </summary>
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            var all = Object.FindObjectsOfType<PathPlayer>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) all[i].RestorePose();
            s_cache = null;
            SceneView.RepaintAll();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - s_last);
            s_last = now;

            if (Application.isPlaying) return;
            if (dt <= 0f || dt > 0.5f) return; // skip after domain reload / lost focus jumps

            if (s_cache == null || now >= s_nextScan)
            {
                s_cache = Object.FindObjectsOfType<PathPlayer>();
                s_nextScan = now + 0.5;
            }
            if (s_cache.Length == 0) return;

            bool any = false;
            for (int i = 0; i < s_cache.Length; i++)
            {
                var p = s_cache[i];
                if (p == null) { s_cache = null; return; } // something was destroyed, rescan next tick

                if (!p.isActiveAndEnabled || !p.previewInEditor || p.ResolvedPath == null)
                {
                    p.RestorePose();
                    continue;
                }

                p.AdvanceEditorPreview(dt);
                any = true;
            }

            if (any) SceneView.RepaintAll();
        }
    }
}
