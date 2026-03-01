using System.Globalization;
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

            _outputText.text =
                "План дома (черновой):\n" +
                $"Площадь пятна: {Format(metrics.FootprintAreaMeters2)} м²\n" +
                $"Периметр наружных стен: {Format(metrics.OuterPerimeterMeters)} м\n" +
                $"Длина внутренних несущих: {Format(metrics.InternalWallsInsideContourLengthMeters)} м\n" +
                $"Габариты контура: {Format(metrics.BoundingWidthMeters)} x {Format(metrics.BoundingHeightMeters)} м\n" +
                $"Точек внешнего контура: {metrics.OuterVerticesCount}\n" +
                $"Сегментов внутренних несущих: {metrics.InternalWallsCount}";
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
        }
    }
}
