using System;

namespace UberDeployer.WebApp2.Core.Models.Api
{
  public class CollectScriptsToRunRequest
  {
    public Guid? DeploymentId { get; set; }

    public string[] ScriptsToRun { get; set; }
  }
}