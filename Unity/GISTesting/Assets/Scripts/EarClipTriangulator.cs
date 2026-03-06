using System.Collections.Generic;
using UnityEngine;

public static class EarClipTriangulator
{
    public static int[] Triangulate(List<Vector2> poly)
    {
        int n = poly.Count;
        if (n < 3) return null;

        // Ensure CCW
        if (SignedArea(poly) < 0f) poly.Reverse();

        List<int> V = new List<int>(n);
        for (int i = 0; i < n; i++) V.Add(i);

        List<int> result = new List<int>();
        int guard = 0;

        while (V.Count > 3 && guard++ < 50000)
        {
            bool earFound = false;

            for (int i = 0; i < V.Count; i++)
            {
                int i0 = V[(i - 1 + V.Count) % V.Count];
                int i1 = V[i];
                int i2 = V[(i + 1) % V.Count];

                Vector2 a = poly[i0];
                Vector2 b = poly[i1];
                Vector2 c = poly[i2];

                if (!IsConvex(a, b, c)) continue;

                bool hasPointInside = false;
                for (int j = 0; j < V.Count; j++)
                {
                    int vi = V[j];
                    if (vi == i0 || vi == i1 || vi == i2) continue;
                    if (PointInTriangle(poly[vi], a, b, c))
                    {
                        hasPointInside = true;
                        break;
                    }
                }

                if (hasPointInside) continue;

                // Ear
                result.Add(i0);
                result.Add(i1);
                result.Add(i2);

                V.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound) break;
        }

        if (V.Count == 3)
        {
            result.Add(V[0]);
            result.Add(V[1]);
            result.Add(V[2]);
        }

        return result.Count >= 3 ? result.ToArray() : null;
    }

    static float SignedArea(List<Vector2> p)
    {
        float a = 0f;
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 v0 = p[i];
            Vector2 v1 = p[(i + 1) % p.Count];
            a += (v0.x * v1.y - v1.x * v0.y);
        }
        return 0.5f * a;
    }

    static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        return cross > 0f;
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float dX = p.x - c.x;
        float dY = p.y - c.y;
        float dX21 = c.x - b.x;
        float dY12 = b.y - c.y;
        float D = dY12 * (a.x - c.x) + dX21 * (a.y - c.y);
        float s = dY12 * dX + dX21 * dY;
        float t = (c.y - a.y) * dX + (a.x - c.x) * dY;

        if (D < 0) return (s <= 0) && (t <= 0) && (s + t >= D);
        return (s >= 0) && (t >= 0) && (s + t <= D);
    }
}