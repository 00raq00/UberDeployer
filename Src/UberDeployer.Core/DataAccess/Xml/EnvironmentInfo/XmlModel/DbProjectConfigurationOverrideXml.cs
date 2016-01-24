using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class DbProjectConfigurationOverrideXml
  {
    [XmlAttribute("projectName")]
    public string ProjectName { get; set; }

    public string DatabaseServerId { get; set; }
  }
}
