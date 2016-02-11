using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.EnvironmentInfo.XmlModel
{
  public class Variable
  {
    [XmlAttribute("name")]
    public string Name { get; set; }

    [XmlAttribute("value")]
    public string Value { get; set; }
  }
}
