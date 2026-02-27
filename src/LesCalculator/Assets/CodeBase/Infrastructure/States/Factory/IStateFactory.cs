using CodeBase.Infrastructure.States.StateInfrastructure;

namespace CodeBase.Infrastructure.States.Factory
{
  public interface IStateFactory
  {
    T GetState<T>() where T : class, IExitableState;
  }
}