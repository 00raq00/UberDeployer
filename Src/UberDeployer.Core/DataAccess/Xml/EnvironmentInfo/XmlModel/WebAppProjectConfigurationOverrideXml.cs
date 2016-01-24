using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class WebAppProjectConfigurationOverrideXml
  {
    [XmlAttribute("projectName")]
    public string ProjectName { get; set; }

    public string AppPoolId { get; set; }

    public string WebSiteName { get; set; }

    public string WebAppDirName { get; set; }

    public string WebAppName { get; set; }
  }
}
