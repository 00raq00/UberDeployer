using System.Collections.Generic;

namespace UberDeployer.WebApp2.Core.Models.Api
{
  public class ProjectViewModel
  {
    public string Name { get; set; }

    public ProjectTypeViewModel Type { get; set; }

    public List<string> AllowedEnvironmentNames { get; set; }
  }
}
