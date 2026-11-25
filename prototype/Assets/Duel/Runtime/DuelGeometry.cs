using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Moba.Duel
{
    // Groups painted faces into one mesh per moving piece (and one for the entire static field).
    internal sealed class DuelGeometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> indices = new List<int>();

        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color color)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal == Vector3.zero) return;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                Vector3 temp = c;
                c = b;
                b = temp;
                normal = -normal;
            }
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal);
                colors.Add(color);
                indices.Add(start + i);
            }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            Face(a, b, c, outward, color);
            Face(a, c, d, outward, color);
        }

        public void Column(Vector3 bottom, float height, Vector2 baseRadii, Vector2 topRadii,
            int sides, Color color, float rotation = 0f)
        {
            Vector3 top = bottom + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a = rotation + i * 2f * Mathf.PI / sides;
                float b = rotation + (i + 1) * 2f * Mathf.PI / sides;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 q = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                Vector3 loP = bottom + new Vector3(p.x * baseRadii.x, 0f, p.z * baseRadii.y);
                Vector3 loQ = bottom + new Vector3(q.x * baseRadii.x, 0f, q.z * baseRadii.y);
                Vector3 hiP = top + new Vector3(p.x * topRadii.x, 0f, p.z * topRadii.y);
                Vector3 hiQ = top + new Vector3(q.x * topRadii.x, 0f, q.z * topRadii.y);
                Quad(loP, hiP, hiQ, loQ, p + q, color);
                Face(top, hiP, hiQ, Vector3.up, color);
                Face(bottom, loP, loQ, Vector3.down, color);
            }
        }

        public void Oval(Vector3 center, Vector3 radius, int sides, Color color)
        {
            // The broad middle ring and offset shoulders give the stones and figures cut planes.
            Column(center - Vector3.up * radius.y, radius.y * 0.55f,
                new Vector2(radius.x * 0.28f, radius.z * 0.28f), new Vector2(radius.x * 0.89f, radius.z * 0.89f), sides, color);
            Column(center - Vector3.up * radius.y * 0.45f, radius.y * 0.79f,
                new Vector2(radius.x * 0.89f, radius.z * 0.89f), new Vector2(radius.x, radius.z), sides, color);
            Column(center + Vector3.up * radius.y * 0.34f, radius.y * 0.66f,
                new Vector2(radius.x, radius.z), new Vector2(radius.x * 0.38f, radius.z * 0.38f), sides, color);
        }

        public void Beam(Vector3 start, Vector3 end, float lowRadius, float highRadius, Color color, int sides = 7)
        {
            Vector3 along = (end - start).normalized;
            Vector3 side = Vector3.Cross(along, Mathf.Abs(along.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(along, side);
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                float b = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p = Mathf.Cos(a) * side + Mathf.Sin(a) * up;
                Vector3 q = Mathf.Cos(b) * side + Mathf.Sin(b) * up;
                Quad(start + p * lowRadius, start + q * lowRadius, end + q * highRadius, end + p * highRadius, p + q, color);
                Face(start, start + p * lowRadius, start + q * lowRadius, -along, color);
                Face(end, end + p * highRadius, end + q * highRadius, along, color);
            }
        }

        public Mesh Bake(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }
    }
}
