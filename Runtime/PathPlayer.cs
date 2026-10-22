using UnityEngine;

namespace PathMotion
{
    /// <summary>
    /// Drives a target Transform along a MotionPath.
    ///
    /// Speed is NOT forced to be uniform: the `progressCurve` maps normalized
    /// time (x: 0..1) to path progress (y: 0..1). Keyframe it yourself in the
    /// inspector's curve editor to make the object ease in/out, pause (flat
    /// region), rush ahead (steep region), etc.
    ///
    /// At any time you can bake the result into a reusable, adjustable
    /// AnimationClip via the inspector "Export" button.
    /// </summary>
    [ExecuteInEditMode]
    public class PathPlayer : MonoBehaviour
    {
        public MotionPath path;
        public Transform target;

        [Tooltip("x = normalized time (0..1), y = path progress (0..1). Edit the curve to key your own speed.")]
        public AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float duration = 5f;
        public WrapMode wrap = WrapMode.Loop;
        public bool orientToPath = true;
        public Vector3 upVector = Vector3.up;

        [Range(0f, 1f)] public float editorPreview = 0f; // inspector scrubber

        [HideInInspector] public bool playing = false;
        [HideInInspector] public float playClock = 0f;

        [Tooltip("In the editor (not Play mode) automatically animate along the path so you can judge speed/rhythm live. No Play mode needed.")]
        public bool previewInEditor = true;

        [Tooltip("Editor preview playback speed only. 1 = real time, 0.5 = half speed (easier to judge rhythm). Does NOT affect the exported clip.")]
        [Range(0.05f, 4f)] public float previewSpeed = 1f;

        // Pose captured when the live preview starts, so pausing puts the object back.
        [System.NonSerialized] bool poseSaved = false;
        [System.NonSerialized] Vector3 savedLocalPos;
        [System.NonSerialized] Quaternion savedLocalRot = Quaternion.identity;

        public Transform ResolvedTarget => target != null ? target : transform;

        [System.NonSerialized] MotionPath _autoPath;

        /// <summary>
        /// The path actually used. If `path` was left empty we fall back to a MotionPath
        /// on this GameObject, or else the first MotionPath in the scene — so the preview
        /// still runs instead of silently doing nothing.
        /// </summary>
        public MotionPath ResolvedPath
        {
            get
            {
                if (path != null) return path;
                if (_autoPath != null) return _autoPath;

                _autoPath = GetComponent<MotionPath>();
                if (_autoPath == null)
                {
                    var all = FindObjectsOfType<MotionPath>();
                    if (all != null && all.Length > 0) _autoPath = all[0];
                }
                return _autoPath;
            }
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return; // editor preview is driven by PathPlayerPreviewTicker
#endif
            if (playing && ResolvedPath != null)
            {
                playClock += Time.deltaTime / Mathf.Max(0.0001f, duration);
                playClock = Wrap(playClock);
                SampleAtNormalizedTime(playClock);
            }
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) RestorePose();
#endif
        }

        /// <summary>Remember where the object was before the live preview hijacked it.</summary>
        public void SavePose()
        {
            if (poseSaved) return;
            Transform t = ResolvedTarget;
            if (t == null) return;
            savedLocalPos = t.localPosition;
            savedLocalRot = t.localRotation;
            poseSaved = true;
        }

        /// <summary>Put the object back where it was before the live preview ran.</summary>
        public void RestorePose()
        {
            if (!poseSaved) return;
            poseSaved = false;
            Transform t = ResolvedTarget;
            if (t == null) return;
            t.localPosition = savedLocalPos;
            t.localRotation = savedLocalRot;
        }

        /// <summary>Advance the editor live-preview by deltaSeconds (used by the editor ticker).</summary>
        public void AdvanceEditorPreview(float deltaSeconds)
        {
            if (ResolvedPath == null) return;
            SavePose();
            playClock += (deltaSeconds * Mathf.Max(0.01f, previewSpeed)) / Mathf.Max(0.0001f, duration);
            playClock = Wrap(playClock);
            SampleAtNormalizedTime(playClock);
            editorPreview = playClock;
        }

        /// <summary>True when the target already sits at the given pose (avoids redundant transform writes).</summary>
        static bool AlreadyAt(Transform t, Vector3 pos, Quaternion rot, bool checkRot)
        {
            if ((t.localPosition - pos).sqrMagnitude > 1e-10f) return false;
            if (checkRot && Quaternion.Angle(t.localRotation, rot) > 0.01f) return false;
            return true;
        }

        public float Wrap(float c)
        {
            switch (wrap)
            {
                case WrapMode.Loop:
                    return c - Mathf.Floor(c);
                case WrapMode.PingPong:
                {
                    float m = c - Mathf.Floor(c);
                    return (Mathf.Floor(c) % 2f == 0f) ? m : 1f - m;
                }
                default:
                    return Mathf.Clamp01(c);
            }
        }

        public void Play() { playing = true; }
        public void Stop() { playing = false; }

        /// <summary>Place the target at normalized time nt (0..1) along the path.</summary>
        public void SampleAtNormalizedTime(float nt)
        {
            MotionPath mp = ResolvedPath;
            if (mp == null) return;
            nt = Mathf.Clamp01(nt);
            float progress = Mathf.Clamp01(progressCurve.Evaluate(nt));
            Vector3 worldPos = mp.EvaluateWorld(progress);

            Transform tgt = ResolvedTarget;
            if (tgt == null) return;

            Vector3 localPos = (tgt.parent != null)
                ? tgt.parent.InverseTransformPoint(worldPos)
                : worldPos;

            Quaternion localRot = tgt.localRotation;
            bool hasRot = false;
            if (orientToPath)
            {
                Vector3 tan = mp.EvaluateTangentWorld(progress);
                if (tan.sqrMagnitude > 1e-6f)
                {
                    Quaternion worldRot = Quaternion.LookRotation(tan, upVector);
                    localRot = (tgt.parent != null)
                        ? Quaternion.Inverse(tgt.parent.rotation) * worldRot
                        : worldRot;
                    hasRot = true;
                }
            }

            // Skip redundant writes; in edit mode every write marks the scene dirty.
            if (AlreadyAt(tgt, localPos, localRot, hasRot)) return;

            tgt.localPosition = localPos;
            if (hasRot) tgt.localRotation = localRot;
        }
    }
}
