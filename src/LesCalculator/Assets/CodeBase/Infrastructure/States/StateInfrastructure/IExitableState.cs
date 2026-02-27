using RSG;

namespace CodeBase.Infrastructure.States.StateInfrastructure
{
  public interface IExitableState
  {
    IPromise BeginExit();
    
    void EndExit();
  }
}