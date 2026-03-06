using CodeBase.RoofCalculator.Plan.Presentation;
using CodeBase.RoofCalculator.Plan.UI;
using UnityEngine;
using Zenject;

namespace CodeBase.Infrastructure.Installers
{
    public class RoofSceneInstaller : MonoInstaller
    {
        [SerializeField] private BuildingPlanEditor _buildingPlanEditor;
        [SerializeField] private BuildingPlanMetricsPresenter _metricsPresenter;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private PlanWindowsContainer _windowsContainer;

        public override void InstallBindings()
        {
            PlanWindowsContainer windowsContainer = EnsureWindowsContainer();
            if (windowsContainer != null)
            {
                Container.Bind<PlanWindowsContainer>().FromInstance(windowsContainer).AsSingle();
            }
            
            Container.Bind<BuildingPlanEditor>().FromInstance(_buildingPlanEditor).AsSingle();
            Container.Bind<BuildingPlanMetricsPresenter>().FromInstance(_metricsPresenter).AsSingle();
            Container.Bind<Canvas>().FromInstance(_canvas).AsSingle();

            
        }

        private PlanWindowsContainer EnsureWindowsContainer()
        {
            if (_windowsContainer != null)
            {
                return _windowsContainer;
            }

            if (_canvas == null)
            {
                Debug.LogError("[RoofSceneInstaller] Не назначен Canvas для создания контейнера окон.", this);
                return null;
            }

            Transform existing = _canvas.transform.Find("PlanWindowsContainer");
            if (existing != null && existing.TryGetComponent(out PlanWindowsContainer existingContainer))
            {
                _windowsContainer = existingContainer;
                return _windowsContainer;
            }

            GameObject containerObject = new GameObject("PlanWindowsContainer", typeof(RectTransform), typeof(PlanWindowsContainer));
            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.SetParent(_canvas.transform, false);
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;
            containerRect.pivot = new Vector2(0f, 1f);

            _windowsContainer = containerObject.GetComponent<PlanWindowsContainer>();
            return _windowsContainer;
        }
    }
}
