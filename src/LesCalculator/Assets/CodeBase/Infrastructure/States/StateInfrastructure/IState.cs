namespace CodeBase.Infrastructure.States.StateInfrastructure
{
  public interface IState: IExitableState
  {
    void Enter();
  }
}