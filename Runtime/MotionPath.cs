using System.Collections.Generic;
using UnityEngine;

namespace PathMotion
{
    /// <summary>
    /// A motion path made of control points. Each point stores a position AND a rotation.
    /// In Hermite mode the point rotation's forward axis is the curve tangent direction, so
    /// rotating a point bends the curve. CatmullRom mode uses automatic smooth tangents and
    /// ignores rotations. Linear mode joins points with straight segments.
    /// Points/rotations are stored in LOCAL space so the whole path can be moved/rotated by
    /// transforming this GameObject.
    /// </summary>
    [ExecuteInEditMode]
    public class MotionPath : MonoBehaviour
    {
        public enum Mode { CatmullRom, Linear, Hermite } // Hermite appended to keep old serialized values valid

        [Tooltip("Hermite: rotate a point to bend the curve (rotation.forward = tangent). CatmullRom: auto smooth tangents. Linear: straight segments.")]
        public Mode mode = Mode.Hermite;
        public bool closed = false;
        public int samplesPerSegment = 20;
        [Tooltip("Global tangent length multiplier (1 = Catmull-Rom feel). Bigger = looser curves.")]
        public float tangentLength = 1f;

        [HideInInspector] public Vector3[] points = new Vector3[0];
        [HideInInspector] public Quaternion[] rotations = new Quaternion[0];

        public int PointCount => points != null ? points.Length : 0;

        // ---------- defaults / setup ----------

        public static Vector3[] DefaultPoints()
        {
            return new Vector3[]
            {
                new Vector3(-4f, 0f, 0f),
                new Vector3(-2f, 2f, 2f),
                new Vector3( 2f, 0f, -2f),
                new Vector3( 4f, 2f, 0f)
            };
        }

        void Reset()
        {
            if (points == null || points.Length < 2)
                points = DefaultPoints();
            AutoComputeRotations();
        }

        public void ResetToDefault()
        {
            points = DefaultPoints();
            AutoComputeRotations();
        }

        public void ClearPoints()
        {
            points = new Vector3[0];
            rotations = new Quaternion[0];
        }

        /// <summary>Make rotations the same length as points (preserving existing where possible).</summary>
        public void SyncRotations()
        {
            if (points == null) points = new Vector3[0];
            if (rotations != null && rotations.Length == points.Length) return;
            rotations = RotationListSynced().ToArray();
        }

        /// <summary>Recompute every point rotation to the automatic (Catmull-Rom-like) tangent direction.</summary>
        public void AutoComputeRotations()
        {
            var pts = points ?? new Vector3[0];
            rotations = new Quaternion[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                rotations[i] = AutoRot(pts, i);
        }

        // ---------- point / rotation mutation (keeps arrays in sync) ----------

        public void AddPoint(Vector3 localPos)
        {
            var p = new List<Vector3>(points ?? new Vector3[0]);
            var r = RotationListSynced();
            p.Add(localPos);
            r.Add(AutoRot(p, p.Count - 1));
            points = p.ToArray();
            rotations = r.ToArray();
        }

        public void InsertPoint(int index, Vector3 localPos)
        {
            InsertPointInternal(index, localPos, Quaternion.identity, true);
        }

        public void InsertPoint(int index, Vector3 localPos, Quaternion localRot)
        {
            InsertPointInternal(index, localPos, localRot, false);
        }

        void InsertPointInternal(int index, Vector3 localPos, Quaternion localRot, bool useAuto)
        {
            var p = new List<Vector3>(points ?? new Vector3[0]);
            var r = RotationListSynced();
            index = Mathf.Clamp(index, 0, p.Count);
            p.Insert(index, localPos);
            r.Insert(index, useAuto ? AutoRot(p, index) : localRot);
            points = p.ToArray();
            rotations = r.ToArray();
        }

        public void RemovePoint(int index)
        {
            if (points == null || points.Length == 0) return;
            index = Mathf.Clamp(index, 0, points.Length - 1);
            var p = new List<Vector3>(points);
            var r = RotationListSynced();
            p.RemoveAt(index);
            if (index < r.Count) r.RemoveAt(index);
            points = p.ToArray();
            rotations = r.ToArray();
        }

        public void SetPointLocal(int index, Vector3 localPos)
        {
            if (points != null && index >= 0 && index < points.Length) points[index] = localPos;
        }

        public void SetPointRotationLocal(int index, Quaternion localRot)
        {
            SyncRotations();
            if (rotations != null && index >= 0 && index < rotations.Length) rotations[index] = localRot;
        }

        /// <summary>
        /// Insert a new waypoint on the curve at normalized progress t (0..1), keeping the
        /// curve shape unchanged at that spot (its rotation is set to the current tangent).
        /// Returns the index of the inserted point, or -1 if there are too few points.
        /// </summary>
        public int InsertPointAt(float t)
        {
            if (points == null || points.Length < 2) return -1;
            t = Mathf.Clamp01(t);

            int segCount = closed ? points.Length : points.Length - 1;
            int seg = Mathf.FloorToInt(t * segCount);
            if (seg >= segCount) seg = segCount - 1;
            int insertIndex = Mathf.Clamp(seg + 1, 0, points.Length);

            Vector3 localPos = transform.InverseTransformPoint(EvaluateWorld(t));
            Vector3 localDir = transform.InverseTransformDirection(EvaluateTangentWorld(t));
            Quaternion localRot = localDir.sqrMagnitude > 1e-8f
                ? Quaternion.LookRotation(localDir.normalized, Vector3.up)
                : Quaternion.identity;

            InsertPoint(insertIndex, localPos, localRot);
            return insertIndex;
        }

        // ---------- evaluation ----------

        public Vector3 EvaluateWorld(float t)
        {
            if (points == null || points.Length == 0) return transform.position;
            if (points.Length == 1) return transform.TransformPoint(points[0]);

            t = Mathf.Clamp01(t);
            int segCount = closed ? points.Length : points.Length - 1;
            float ft = t * segCount;
            int seg = Mathf.FloorToInt(ft);
            if (seg >= segCount) seg = segCount - 1;
            float u = ft - seg;

            if (mode == Mode.Linear)
            {
                Vector3 a = transform.TransformPoint(points[ClampIndex(seg)]);
                Vector3 b = transform.TransformPoint(points[ClampIndex(seg + 1)]);
                return Vector3.Lerp(a, b, u);
            }

            if (mode == Mode.Hermite)
            {
                Vector3 P0 = transform.TransformPoint(points[ClampIndex(seg)]);
                Vector3 P1 = transform.TransformPoint(points[ClampIndex(seg + 1)]);
                Vector3 M0 = GetWorldTangent(seg);
                Vector3 M1 = GetWorldTangent(seg + 1);
                return Hermite(P0, P1, M0, M1, u);
            }

            // CatmullRom
            Vector3 p0 = transform.TransformPoint(points[ClampIndex(seg - 1)]);
            Vector3 p1 = transform.TransformPoint(points[ClampIndex(seg)]);
            Vector3 p2 = transform.TransformPoint(points[ClampIndex(seg + 1)]);
            Vector3 p3 = transform.TransformPoint(points[ClampIndex(seg + 2)]);
            return CatmullRom(p0, p1, p2, p3, u);
        }

        public Vector3 EvaluateTangentWorld(float t, float step = 0.001f)
        {
            t = Mathf.Clamp01(t);
            Vector3 a = EvaluateWorld(Mathf.Max(0f, t - step));
            Vector3 b = EvaluateWorld(Mathf.Min(1f, t + step));
            Vector3 tan = b - a;
            if (tan.sqrMagnitude < 1e-8f)
                tan = EvaluateWorld(Mathf.Min(1f, t + step * 4f)) - EvaluateWorld(t);
            return tan.normalized;
        }

        public float EstimateLength(int steps = 200)
        {
            if (points == null || points.Length < 2) return 0f;
            float len = 0f;
            Vector3 prev = EvaluateWorld(0f);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 cur = EvaluateWorld(i / (float)steps);
                len += Vector3.Distance(prev, cur);
                prev = cur;
            }
            return len;
        }

        // ---------- internals ----------

        int ClampIndex(int i)
        {
            if (points == null || points.Length == 0) return 0;
            if (closed)
            {
                i %= points.Length;
                if (i < 0) i += points.Length;
                return i;
            }
            return Mathf.Clamp(i, 0, points.Length - 1);
        }

        int WrapIdx(int i, int n)
        {
            i %= n;
            if (i < 0) i += n;
            return i;
        }

        Vector3 AutoTangentDirLocal(IList<Vector3> pts, int i)
        {
            if (pts == null || pts.Count < 2) return Vector3.forward;
            int prev = closed ? WrapIdx(i - 1, pts.Count) : Mathf.Max(0, i - 1);
            int next = closed ? WrapIdx(i + 1, pts.Count) : Mathf.Min(pts.Count - 1, i + 1);
            Vector3 d = pts[next] - pts[prev];
            if (d.sqrMagnitude < 1e-8f) d = Vector3.forward;
            return d;
        }

        Quaternion AutoRot(IList<Vector3> pts, int i)
        {
            Vector3 d = AutoTangentDirLocal(pts, i);
            if (d.sqrMagnitude < 1e-8f) d = Vector3.forward;
            return Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        List<Quaternion> RotationListSynced()
        {
            var src = points ?? new Vector3[0];
            var r = (rotations != null) ? new List<Quaternion>(rotations) : new List<Quaternion>();
            if (r.Count > src.Length) r.RemoveRange(src.Length, r.Count - src.Length);
            while (r.Count < src.Length) r.Add(AutoRot(src, r.Count));
            return r;
        }

        Vector3 GetWorldTangent(int i)
        {
            i = ClampIndex(i);

            Vector3 localDir = (rotations != null && i < rotations.Length)
                ? (rotations[i] * Vector3.forward)
                : AutoTangentDirLocal(points, i);
            if (localDir.sqrMagnitude < 1e-8f) localDir = Vector3.forward;

            Vector3 worldDir = transform.TransformDirection(localDir.normalized).normalized;

            Vector3 a = transform.TransformPoint(points[ClampIndex(i - 1)]);
            Vector3 b = transform.TransformPoint(points[ClampIndex(i + 1)]);
            float mag = Vector3.Distance(a, b) * 0.5f * Mathf.Max(0.0001f, tangentLength);
            return worldDir * mag;
        }

        static Vector3 Hermite(Vector3 P0, Vector3 P1, Vector3 M0, Vector3 M1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;
            return h00 * P0 + h10 * M0 + h01 * P1 + h11 * M1;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void OnDrawGizmos()
        {
            if (points == null || points.Length < 2) return;

            Gizmos.color = new Color(1f, 0.45f, 0f); // orange-red, readable on light & dark
            int segCount = closed ? points.Length : points.Length - 1;
            int steps = Mathf.Max(2, samplesPerSegment * segCount);
            Vector3 prev = EvaluateWorld(0f);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 cur = EvaluateWorld(i / (float)steps);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f);
            foreach (var lp in points)
            {
                Vector3 wp = transform.TransformPoint(lp);
                Gizmos.DrawSphere(wp, 0.12f);
            }
        }
    }
}
