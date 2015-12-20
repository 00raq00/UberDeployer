using System;
using System.Collections.Generic;

namespace UberDeployer.WebApp2.Core.Models.Api
{
  public class CollectProjectDependenciesToDeployRequest
  {
    public Guid? DeploymentId { get; set; }    
    
    public List<DependentProject> DependentProjects { get; set; }
  }
}