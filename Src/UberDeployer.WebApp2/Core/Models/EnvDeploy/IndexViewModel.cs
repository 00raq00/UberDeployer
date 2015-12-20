namespace UberDeployer.WebApp2.Core.Models.EnvDeploy
{
  public class EnvDeployViewModel : BaseViewModel
  {
    public EnvDeployViewModel()
    {
      CurrentAppPage = AppPage.EnvDeployment;
    }

    public string InitialTargetEnvironment { get; set; }
  }
}