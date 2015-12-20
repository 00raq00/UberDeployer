using UberDeployer.WebApp2.Core.Utils;

namespace UberDeployer.WebApp2.Core.Models
{
  public abstract class BaseViewModel
  {
    public string Username
    {
      get { return SecurityUtils.CurrentUsername; }
    }

    public AppPage CurrentAppPage { get; set; }

    public bool IsEnvDeployEnabled { get { return true; } }
  }
}
