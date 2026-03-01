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

        public override void InstallBindings()
        {
            Container.Bind<BuildingPlanEditor>().FromInstance(_buildingPlanEditor).AsSingle();
            Container.Bind<BuildingPlanMetricsPresenter>().FromInstance(_metricsPresenter).AsSingle();
            
        }
    }
}
