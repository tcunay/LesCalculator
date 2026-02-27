using CodeBase.Infrastructure.Loading;
using CodeBase.Infrastructure.States.StateInfrastructure;
using CodeBase.Infrastructure.States.StateMachine;

namespace CodeBase.Infrastructure.States.GameStates
{
    public class LoadingBattleState : SimpleState
    {
        private const string FightSceneName = "BattleScene";
        
        private readonly ISceneLoader _sceneLoader;
        private readonly IGameStateMachine _stateMachine;

        public LoadingBattleState(ISceneLoader sceneLoader, IGameStateMachine stateMachine)
        {
            _sceneLoader = sceneLoader;
            _stateMachine = stateMachine;
        }
        
        public override void Enter()
        {
            _sceneLoader.LoadScene(FightSceneName, EnterBattleLoopState);
        }

        private void EnterBattleLoopState()
        {
            _stateMachine.Enter<BattleLoopState>();
        }

    
    }
}