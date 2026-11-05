using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Unity draws gizmos over everything in the Game view, including the QA overlay. This keeps sensor gizmo lines
    /// out of the screen areas the overlay covers by cutting each line where it enters and leaves them.
    /// Only the Game view is cut. The Scene view has no overlay.
    /// </summary>
    public static class OverlayGizmoMask
    {
        private const float Padding = 2f;

        private static readonly Dictionary<object, Rect[]> Covered = new Dictionary<object, Rect[]>();
        private static readonly List<Vector2> Pieces = new List<Vector2>();
        private static readonly List<Vector2> NextPieces = new List<Vector2>();
        private static readonly Vector3[] Corners = new Vector3[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Covered.Clear();
        }

        /// <summary>Tells the mask which screen areas, in pixels from the bottom left, the owner covers.</summary>
        public static void Publish(object owner, Rect[] rects)
        {
            Covered[owner] = rects;
        }

        public static void Withdraw(object owner)
        {
            Covered.Remove(owner);
        }

        /// <summary>Draws a gizmo line, leaving out the parts that are behind the overlay in the Game view.</summary>
        public static void DrawLine(Vector3 from, Vector3 to)
        {
            Camera camera = Camera.current != null ? Camera.current : Camera.main;
            if (Covered.Count == 0 || camera == null || camera.cameraType != CameraType.Game)
            {
                Gizmos.DrawLine(from, to);
                return;
            }

            GetVisibleParts(camera, from, to, Pieces);
            foreach (Vector2 piece in Pieces)
            {
                Gizmos.DrawLine(Vector3.LerpUnclamped(from, to, piece.x), Vector3.LerpUnclamped(from, to, piece.y));
            }
        }

        /// <summary>
        /// The parts of the line that are not behind the overlay, as ranges from 0 (at from) to 1 (at to).
        /// Parts behind the camera are not removed, since nothing is drawn there anyway.
        /// </summary>
        public static void GetVisibleParts(Camera camera, Vector3 from, Vector3 to, List<Vector2> parts)
        {
            parts.Clear();
            parts.Add(new Vector2(0f, 1f));

            Matrix4x4 matrix = camera.projectionMatrix * camera.worldToCameraMatrix;
            Vector4 a = matrix * new Vector4(from.x, from.y, from.z, 1f);
            Vector4 b = matrix * new Vector4(to.x, to.y, to.z, 1f);
            Rect view = camera.pixelRect;

            foreach (Rect[] rects in Covered.Values)
            {
                foreach (Rect rect in rects)
                {
                    var local = new Rect(rect.xMin - Padding - view.xMin, rect.yMin - Padding - view.yMin, rect.width + 2f * Padding, rect.height + 2f * Padding);
                    CutOut(parts, a, b, local, view.width, view.height);
                }
            }
        }

        /// <summary>The same shape as Gizmos.DrawFrustum, made of lines that respect the overlay.</summary>
        public static void DrawFrustum(Vector3 position, Quaternion rotation, float verticalFov, float far, float near, float aspect)
        {
            Gizmos.matrix = Matrix4x4.identity;

            float halfTan = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
            for (int plane = 0; plane < 2; plane++)
            {
                float distance = plane == 0 ? near : far;
                float halfHeight = distance * halfTan;
                float halfWidth = halfHeight * aspect;
                Corners[plane * 4 + 0] = position + rotation * new Vector3(-halfWidth, -halfHeight, distance);
                Corners[plane * 4 + 1] = position + rotation * new Vector3(halfWidth, -halfHeight, distance);
                Corners[plane * 4 + 2] = position + rotation * new Vector3(halfWidth, halfHeight, distance);
                Corners[plane * 4 + 3] = position + rotation * new Vector3(-halfWidth, halfHeight, distance);
            }

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                DrawLine(Corners[i], Corners[next]);
                DrawLine(Corners[4 + i], Corners[4 + next]);
                DrawLine(Corners[i], Corners[4 + i]);
            }
        }

        /// <summary>
        /// Removes the part of every piece that lies inside the rectangle. Clip coordinates change in a straight line along the
        /// line in the world, so the cut can be worked out on them and still be exact in the world.
        /// </summary>
        private static void CutOut(List<Vector2> pieces, Vector4 a, Vector4 b, Rect rect, float width, float height)
        {
            NextPieces.Clear();

            bool hasInside = TryGetInsideRange(a, b, rect, width, height, out float inside0, out float inside1);
            foreach (Vector2 piece in pieces)
            {
                if (!hasInside || inside1 <= piece.x || inside0 >= piece.y)
                {
                    NextPieces.Add(piece);
                    continue;
                }
                if (inside0 > piece.x)
                {
                    NextPieces.Add(new Vector2(piece.x, inside0));
                }
                if (inside1 < piece.y)
                {
                    NextPieces.Add(new Vector2(inside1, piece.y));
                }
            }

            pieces.Clear();
            pieces.AddRange(NextPieces);
        }

        private static bool TryGetInsideRange(Vector4 a, Vector4 b, Rect rect, float width, float height, out float start, out float end)
        {
            start = 0f;
            end = 1f;

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            // Each value is >= 0 when the point is on the inside of that edge.
            return Keep(a.w - 0.0001f, b.w - 0.0001f, ref start, ref end)
                && Keep(halfW * a.x + (halfW - rect.xMin) * a.w, halfW * b.x + (halfW - rect.xMin) * b.w, ref start, ref end)
                && Keep(-halfW * a.x + (rect.xMax - halfW) * a.w, -halfW * b.x + (rect.xMax - halfW) * b.w, ref start, ref end)
                && Keep(halfH * a.y + (halfH - rect.yMin) * a.w, halfH * b.y + (halfH - rect.yMin) * b.w, ref start, ref end)
                && Keep(-halfH * a.y + (rect.yMax - halfH) * a.w, -halfH * b.y + (rect.yMax - halfH) * b.w, ref start, ref end)
                && start < end;
        }

        private static bool Keep(float atStart, float atEnd, ref float start, ref float end)
        {
            if (atStart < 0f && atEnd < 0f) return false;
            if (atStart >= 0f && atEnd >= 0f) return true;

            float t = atStart / (atStart - atEnd);
            if (atStart < 0f)
            {
                start = Mathf.Max(start, t);
            }
            else
            {
                end = Mathf.Min(end, t);
            }
            return true;
        }
    }
}
