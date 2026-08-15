using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Gestures
{
    public static class GestureRecognizer
    {
        private const int SamplingPoints = 32;

        public static ShapeType RecognizeShape(List<Vector2> rawPoints, out float score)
        {
            if (rawPoints == null || rawPoints.Count < 5)
            {
                score = 0;
                return ShapeType.Triangle;
            }

            List<Vector2> processed = NormalizePoints(rawPoints);

            float triangleDist = DistanceAtBestAngle(processed, GestureTemplates.Triangle);
            float circleDist = DistanceAtBestAngle(processed, GestureTemplates.Circle);
            float squareDist = DistanceAtBestAngle(processed, GestureTemplates.Square);

            float minDist = Mathf.Min(triangleDist, Mathf.Min(circleDist, squareDist));

            score = Mathf.Clamp01(1f - (minDist / 0.8f));

            if (minDist == circleDist) return ShapeType.Circle;
            if (minDist == triangleDist) return ShapeType.Triangle;
            return ShapeType.Square;
        }

        #region Normalization Pipeline

        // Сделали публичным для нормализации шаблонов
        public static List<Vector2> NormalizePoints(List<Vector2> points)
        {
            List<Vector2> resampled = Resample(points, SamplingPoints);
            List<Vector2> rotated = RotateToZero(resampled);
            List<Vector2> scaled = ScaleToSquare(rotated, 1.0f);
            List<Vector2> translated = TranslateToOrigin(scaled);
            return translated;
        }

        private static List<Vector2> Resample(List<Vector2> points, int n)
        {
            float interval = PathLength(points) / (n - 1);
            float distanceAccumulated = 0f;
            List<Vector2> newPoints = new List<Vector2> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                float d = Vector2.Distance(points[i - 1], points[i]);
                if ((distanceAccumulated + d) >= interval)
                {
                    Vector2 firstPoint = points[i - 1];
                    float t = (interval - distanceAccumulated) / d;
                    Vector2 point = Vector2.Lerp(firstPoint, points[i], t);
                    newPoints.Add(point);
                    points.Insert(i, point);
                    distanceAccumulated = 0f;
                }
                else
                {
                    distanceAccumulated += d;
                }
            }

            if (newPoints.Count == n - 1)
            {
                newPoints.Add(points[points.Count - 1]);
            }
            return newPoints;
        }

        private static List<Vector2> RotateToZero(List<Vector2> points)
        {
            Vector2 centroid = Centroid(points);
            float radians = Mathf.Atan2(centroid.y - points[0].y, centroid.x - points[0].x);
            return RotateBy(points, -radians);
        }

        private static List<Vector2> RotateBy(List<Vector2> points, float radians)
        {
            List<Vector2> newPoints = new List<Vector2>();
            Vector2 c = Centroid(points);
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            for (int i = 0; i < points.Count; i++)
            {
                float dx = points[i].x - c.x;
                float dy = points[i].y - c.y;
                newPoints.Add(new Vector2(dx * cos - dy * sin + c.x, dx * sin + dy * cos + c.y));
            }
            return newPoints;
        }

        private static List<Vector2> ScaleToSquare(List<Vector2> points, float size)
        {
            Rect box = BoundingBox(points);
            List<Vector2> newPoints = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                float qx = points[i].x * (size / Mathf.Max(box.width, 0.001f));
                float qy = points[i].y * (size / Mathf.Max(box.height, 0.001f));
                newPoints.Add(new Vector2(qx, qy));
            }
            return newPoints;
        }

        private static List<Vector2> TranslateToOrigin(List<Vector2> points)
        {
            Vector2 c = Centroid(points);
            List<Vector2> newPoints = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                newPoints.Add(points[i] - c);
            }
            return newPoints;
        }

        private static float DistanceAtBestAngle(List<Vector2> points, List<Vector2> template)
        {
            float a = -Mathf.PI / 4f;
            float b = Mathf.PI / 4f;
            float threshold = Mathf.Deg2Rad * 2f;

            float phi = 0.5f * (-1f + Mathf.Sqrt(5f));
            float x1 = b - phi * (b - a);
            float x2 = a + phi * (b - a);
            float f1 = DistanceAtAngle(points, template, x1);
            float f2 = DistanceAtAngle(points, template, x2);

            while (Mathf.Abs(b - a) > threshold)
            {
                if (f1 < f2)
                {
                    b = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = b - phi * (b - a);
                    f1 = DistanceAtAngle(points, template, x1);
                }
                else
                {
                    a = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = a + phi * (b - a);
                    f2 = DistanceAtAngle(points, template, x2);
                }
            }
            return Mathf.Min(f1, f2);
        }

        private static float DistanceAtAngle(List<Vector2> points, List<Vector2> template, float radians)
        {
            List<Vector2> newPoints = RotateBy(points, radians);
            return PathDistance(newPoints, template);
        }

        // Поддержка рисования как по часовой, так и против часовой стрелки
        private static float PathDistance(List<Vector2> p1, List<Vector2> p2)
        {
            float forwardDist = 0f;
            float reverseDist = 0f;
            int count = Mathf.Min(p1.Count, p2.Count);

            for (int i = 0; i < count; i++)
            {
                forwardDist += Vector2.Distance(p1[i], p2[i]);
                reverseDist += Vector2.Distance(p1[i], p2[count - 1 - i]);
            }

            return Mathf.Min(forwardDist, reverseDist) / count;
        }

        private static float PathLength(List<Vector2> points)
        {
            float d = 0f;
            for (int i = 1; i < points.Count; i++)
                d += Vector2.Distance(points[i - 1], points[i]);
            return d;
        }

        private static Vector2 Centroid(List<Vector2> points)
        {
            Vector2 sum = Vector2.zero;
            foreach (var p in points) sum += p;
            return sum / points.Count;
        }

        private static Rect BoundingBox(List<Vector2> points)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        #endregion
    }
}