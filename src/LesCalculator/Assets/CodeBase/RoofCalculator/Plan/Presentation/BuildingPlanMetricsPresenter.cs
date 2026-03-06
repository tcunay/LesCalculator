using System.Globalization;
using System.Text;
using CodeBase.RoofCalculator.Plan.UI;
using TMPro;
using UnityEngine;
using Zenject;

namespace CodeBase.RoofCalculator.Plan.Presentation
{
    public sealed class BuildingPlanMetricsPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text _outputText;

        private BuildingPlanEditor _planEditor;

        [Inject]
        private void Construct(BuildingPlanEditor planEditor)
        {
            _planEditor = planEditor;
        }

        private void Start()
        {
            if (_outputText == null)
            {
                Debug.LogError("[BuildingPlanMetricsPresenter] Не назначен Output Text.", this);
                enabled = false;
                return;
            }

            _planEditor.PlanChanged += RecalculateAndRender;
            RecalculateAndRender();
        }

        private void OnDestroy()
        {
            _planEditor.PlanChanged -= RecalculateAndRender;
        }

        private void RecalculateAndRender()
        {
            if (!_planEditor.TryBuildPlanData(out BuildingPlanData planData, out string buildError))
            {
                _outputText.text = "Ошибка плана: " + buildError;
                return;
            }

            if (!BuildingPlanMath.TryCalculateMetrics(planData, out BuildingPlanMetrics metrics, out string calcError))
            {
                _outputText.text = "Ошибка расчёта: " + calcError;
                return;
            }

            StringBuilder text = new StringBuilder(1024);
            text.AppendLine("План дома:");
            text.AppendLine($"Площадь пятна: {Format(metrics.FootprintAreaMeters2)} м²");
            text.AppendLine($"Периметр наружных стен: {Format(metrics.OuterPerimeterMeters)} м");
            text.AppendLine($"Длина внутренних несущих: {Format(metrics.InternalWallsInsideContourLengthMeters)} м");
            text.AppendLine($"Габариты контура: {Format(metrics.BoundingWidthMeters)} x {Format(metrics.BoundingHeightMeters)} м");
            text.AppendLine($"Точек внешнего контура: {metrics.OuterVerticesCount}");
            text.AppendLine($"Сегментов внутренних несущих: {metrics.InternalWallsCount}");

            AppendOuterContourDetails(text, planData);
            AppendInternalWallsDetails(text, planData);

            _outputText.text = text.ToString();
        }

        private static void AppendOuterContourDetails(StringBuilder output, BuildingPlanData planData)
        {
            if (planData.OuterContourMeters == null || planData.OuterContourMeters.Count == 0)
            {
                return;
            }

            output.AppendLine();
            output.AppendLine("Вершины (координаты, м):");
            for (int i = 0; i < planData.OuterContourMeters.Count; i++)
            {
                Vector2 vertex = planData.OuterContourMeters[i];
                string name = BuildingPlanMath.GetVertexLabel(i);
                output.AppendLine($"{name}: X={Format(vertex.x)}, Y={Format(vertex.y)}");
            }

            if (planData.OuterContourMeters.Count < 2)
            {
                return;
            }

            output.AppendLine();
            output.AppendLine("Стороны внешнего контура:");
            for (int i = 0; i < planData.OuterContourMeters.Count; i++)
            {
                int nextIndex = (i + 1) % planData.OuterContourMeters.Count;
                float length = Vector2.Distance(planData.OuterContourMeters[i], planData.OuterContourMeters[nextIndex]);
                string sideName = BuildingPlanMath.GetVertexLabel(i) + BuildingPlanMath.GetVertexLabel(nextIndex);
                output.AppendLine($"{sideName} = {Format(length)} м");
            }

            if (planData.OuterContourMeters.Count < 3)
            {
                return;
            }

            output.AppendLine();
            output.AppendLine("Внутренние углы контура:");
            for (int i = 0; i < planData.OuterContourMeters.Count; i++)
            {
                int previousIndex = (i - 1 + planData.OuterContourMeters.Count) % planData.OuterContourMeters.Count;
                int nextIndex = (i + 1) % planData.OuterContourMeters.Count;
                float angle = BuildingPlanMath.CalculateInteriorAngleDegrees(planData.OuterContourMeters, i);
                string angleName =
                    "∠" +
                    BuildingPlanMath.GetVertexLabel(previousIndex) +
                    BuildingPlanMath.GetVertexLabel(i) +
                    BuildingPlanMath.GetVertexLabel(nextIndex);

                output.AppendLine($"{angleName} = {Format(angle)}°");
            }
        }

        private static void AppendInternalWallsDetails(StringBuilder output, BuildingPlanData planData)
        {
            if (planData.InternalLoadBearingWallsMeters == null || planData.InternalLoadBearingWallsMeters.Count == 0)
            {
                return;
            }

            output.AppendLine();
            output.AppendLine("Внутренние несущие (м):");
            for (int i = 0; i < planData.InternalLoadBearingWallsMeters.Count; i++)
            {
                LoadBearingWallSegment wall = planData.InternalLoadBearingWallsMeters[i];
                output.AppendLine($"W{i + 1}: {Format(wall.LengthMeters)}");
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
        }
    }
}
