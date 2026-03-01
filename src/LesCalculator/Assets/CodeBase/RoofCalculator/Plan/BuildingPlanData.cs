using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeBase.RoofCalculator.Plan
{
    [Serializable]
    public struct LoadBearingWallSegment
    {
        public Vector2 StartMeters;
        public Vector2 EndMeters;

        public LoadBearingWallSegment(Vector2 startMeters, Vector2 endMeters)
        {
            StartMeters = startMeters;
            EndMeters = endMeters;
        }

        public float LengthMeters => Vector2.Distance(StartMeters, EndMeters);
        public Vector2 MidPointMeters => (StartMeters + EndMeters) * 0.5f;
    }

    [Serializable]
    public sealed class BuildingPlanData
    {
        public float PlanWidthMeters;
        public float PlanHeightMeters;
        public List<Vector2> OuterContourMeters = new List<Vector2>();
        public List<LoadBearingWallSegment> InternalLoadBearingWallsMeters = new List<LoadBearingWallSegment>();
    }

    public struct BuildingPlanMetrics
    {
        public int OuterVerticesCount;
        public int InternalWallsCount;

        public float FootprintAreaMeters2;
        public float OuterPerimeterMeters;

        public float BoundingWidthMeters;
        public float BoundingHeightMeters;

        public float TotalInternalWallsLengthMeters;
        public float InternalWallsInsideContourLengthMeters;
    }

    public static class BuildingPlanMath
    {
        public static bool TryCalculateMetrics(BuildingPlanData planData, out BuildingPlanMetrics metrics, out string error)
        {
            metrics = default;
            error = string.Empty;

            if (planData == null)
            {
                error = "План дома не задан.";
                return false;
            }

            if (planData.OuterContourMeters == null || planData.OuterContourMeters.Count < 3)
            {
                error = "Внешний контур должен содержать минимум 3 точки.";
                return false;
            }

            float signedArea = SignedPolygonArea(planData.OuterContourMeters);
            float area = Mathf.Abs(signedArea);
            if (area < 0.001f)
            {
                error = "Площадь внешнего контура слишком мала. Проверьте точки.";
                return false;
            }

            float perimeter = 0f;
            Vector2 min = planData.OuterContourMeters[0];
            Vector2 max = planData.OuterContourMeters[0];

            for (int i = 0; i < planData.OuterContourMeters.Count; i++)
            {
                Vector2 current = planData.OuterContourMeters[i];
                Vector2 next = planData.OuterContourMeters[(i + 1) % planData.OuterContourMeters.Count];

                perimeter += Vector2.Distance(current, next);

                min = Vector2.Min(min, current);
                max = Vector2.Max(max, current);
            }

            float internalTotalLength = 0f;
            float internalInsideContourLength = 0f;

            if (planData.InternalLoadBearingWallsMeters != null)
            {
                for (int i = 0; i < planData.InternalLoadBearingWallsMeters.Count; i++)
                {
                    LoadBearingWallSegment wall = planData.InternalLoadBearingWallsMeters[i];
                    float length = wall.LengthMeters;
                    internalTotalLength += length;

                    if (IsPointInsidePolygon(wall.MidPointMeters, planData.OuterContourMeters))
                    {
                        internalInsideContourLength += length;
                    }
                }
            }

            metrics = new BuildingPlanMetrics
            {
                OuterVerticesCount = planData.OuterContourMeters.Count,
                InternalWallsCount = planData.InternalLoadBearingWallsMeters != null
                    ? planData.InternalLoadBearingWallsMeters.Count
                    : 0,
                FootprintAreaMeters2 = area,
                OuterPerimeterMeters = perimeter,
                BoundingWidthMeters = max.x - min.x,
                BoundingHeightMeters = max.y - min.y,
                TotalInternalWallsLengthMeters = internalTotalLength,
                InternalWallsInsideContourLengthMeters = internalInsideContourLength
            };

            return true;
        }

        private static float SignedPolygonArea(IReadOnlyList<Vector2> vertices)
        {
            float area2 = 0f;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                Vector2 next = vertices[(i + 1) % vertices.Count];
                area2 += current.x * next.y - next.x * current.y;
            }

            return area2 * 0.5f;
        }

        private static bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 vi = polygon[i];
                Vector2 vj = polygon[j];

                bool intersects =
                    ((vi.y > point.y) != (vj.y > point.y)) &&
                    (point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y + Mathf.Epsilon) + vi.x);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
