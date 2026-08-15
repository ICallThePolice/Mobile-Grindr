using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace SpellSystem.Gestures
{
    [NoAutoStaticsCleanup]
    public static class GestureTemplates
    {
        // Нормализуем все шаблоны при старте под единый размер 1x1
        public static readonly List<Vector2> Triangle = GestureRecognizer.NormalizePoints(GenerateTriangle());
        public static readonly List<Vector2> Circle = GestureRecognizer.NormalizePoints(GenerateCircle());
        public static readonly List<Vector2> Square = GestureRecognizer.NormalizePoints(GenerateSquare());

        private static List<Vector2> GenerateCircle()
        {
            List<Vector2> points = new List<Vector2>();
            for (int i = 0; i < 32; i++)
            {
                float angle = (i / 32f) * Mathf.PI * 2f;
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
            return points;
        }

        private static List<Vector2> GenerateTriangle()
        {
            List<Vector2> raw = new List<Vector2>
            {
                new Vector2(0, 1),
                new Vector2(0.866f, -0.5f),
                new Vector2(-0.866f, -0.5f),
                new Vector2(0, 1)
            };
            return InterpolatePath(raw, 32);
        }

        private static List<Vector2> GenerateSquare()
        {
            List<Vector2> raw = new List<Vector2>
            {
                new Vector2(-0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(-0.5f, -0.5f),
                new Vector2(-0.5f, 0.5f)
            };
            return InterpolatePath(raw, 32);
        }

        private static List<Vector2> InterpolatePath(List<Vector2> waypoints, int targetCount)
        {
            List<Vector2> result = new List<Vector2>();
            int segments = waypoints.Count - 1;
            int pointsPerSegment = targetCount / segments;

            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < pointsPerSegment; j++)
                {
                    float t = (float)j / pointsPerSegment;
                    result.Add(Vector2.Lerp(waypoints[i], waypoints[i + 1], t));
                }
            }
            while (result.Count < targetCount)
            {
                result.Add(waypoints[waypoints.Count - 1]);
            }
            return result;
        }
    }
}