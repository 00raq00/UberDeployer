using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class CustomEnvMachineXml
  {
    [XmlAttribute]
    public string Id { get; set; }

    [XmlAttribute]
    public string MachineName { get; set; }
  }
}
