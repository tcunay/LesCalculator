using CodeBase.Infrastructure.Loading;
using CodeBase.Infrastructure.States.StateInfrastructure;
using CodeBase.Infrastructure.States.StateMachine;

namespace CodeBase.Infrastructure.States.GameStates
{
    public class LoadingRoofState : SimpleState
    {
        private const string RoofSceneName = "RoofScene";
        
        private readonly ISceneLoader _sceneLoader;
        private readonly IGameStateMachine _stateMachine;

        public LoadingRoofState(ISceneLoader sceneLoader, IGameStateMachine stateMachine)
        {
            _sceneLoader = sceneLoader;
            _stateMachine = stateMachine;
        }
        
        public override void Enter()
        {
            _sceneLoader.LoadScene(RoofSceneName, EnterRoofLoopState);
        }

        private void EnterRoofLoopState()
        {
            _stateMachine.Enter<RoofLoopState>();
        }

    
    }
}