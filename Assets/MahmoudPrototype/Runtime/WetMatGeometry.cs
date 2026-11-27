using System.Collections.Generic;
using UnityEngine;

namespace ButterflyEffect.Prototype
{
    internal sealed class WetMatGeometry
    {
        internal const float MinimumUp = 0.45f;
        private const float SurfaceLift = 0.002f;
        private readonly Transform surface;
        private readonly Vector3[] sourceVertices;
        private readonly int[] sourceTriangles;

        // Intersecting a triangle with a rectangle produces at most seven vertices.
        private readonly Vector3[] clipA = new Vector3[7];
        private readonly Vector3[] clipB = new Vector3[7];
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public WetMatGeometry(MeshCollider collider)
        {
            surface = collider.transform;
            Mesh source = collider.sharedMesh;
            sourceVertices = source.vertices;
            sourceTriangles = source.triangles;
        }

        public void Fit(Mesh target, Vector2 worldCenter, float reach)
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();
            Matrix4x4 toWorld = surface.localToWorldMatrix;
            Matrix4x4 toLocal = surface.worldToLocalMatrix;
            Matrix4x4 toWorldNormal = toLocal.transpose;

            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                Vector3 a = sourceVertices[sourceTriangles[i]];
                Vector3 b = sourceVertices[sourceTriangles[i + 1]];
                Vector3 c = sourceVertices[sourceTriangles[i + 2]];
                Vector3 normal = toWorldNormal.MultiplyVector(Vector3.Cross(b - a, c - a)).normalized;
                if (normal.y < MinimumUp) continue;

                clipA[0] = toWorld.MultiplyPoint3x4(a);
                clipA[1] = toWorld.MultiplyPoint3x4(b);
                clipA[2] = toWorld.MultiplyPoint3x4(c);
                int count = Clip(clipA, 3, clipB, 0, worldCenter.x - reach, 1f);
                count = Clip(clipB, count, clipA, 0, worldCenter.x + reach, -1f);
                count = Clip(clipA, count, clipB, 2, worldCenter.y - reach, 1f);
                count = Clip(clipB, count, clipA, 2, worldCenter.y + reach, -1f);
                if (count < 3) continue;

                int first = vertices.Count;
                for (int v = 0; v < count; v++)
                {
                    Vector3 point = clipA[v];
                    vertices.Add(toLocal.MultiplyPoint3x4(point + Vector3.up * SurfaceLift));
                    uvs.Add((new Vector2(point.x, point.z) - worldCenter) / reach);
                }
                for (int v = 1; v < count - 1; v++)
                {
                    triangles.Add(first);
                    triangles.Add(first + v);
                    triangles.Add(first + v + 1);
                }
            }

            target.Clear();
            target.SetVertices(vertices);
            target.SetUVs(0, uvs);
            target.SetTriangles(triangles, 0);
        }

        private static int Clip(Vector3[] input, int count, Vector3[] output, int axis, float edge, float inward)
        {
            if (count == 0) return 0;
            Vector3 previous = input[count - 1];
            float previousDistance = (previous[axis] - edge) * inward;
            int written = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 current = input[i];
                float currentDistance = (current[axis] - edge) * inward;
                // On-edge vertices are kept directly, without a duplicate intersection.
                if (previousDistance * currentDistance < 0f)
                {
                    float fraction = previousDistance / (previousDistance - currentDistance);
                    output[written++] = Vector3.LerpUnclamped(previous, current, fraction);
                }
                if (currentDistance >= 0f) output[written++] = current;
                previous = current;
                previousDistance = currentDistance;
            }
            return written;
        }
    }
}
