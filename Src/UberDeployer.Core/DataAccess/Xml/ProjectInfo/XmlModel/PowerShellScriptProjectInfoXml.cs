using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfo.XmlModel
{
  public class PowerShellScriptProjectInfoXml : ProjectInfoXml
  {
    public bool IsRemote { get; set; }

    public TargetMachineXml TargetMachine { get; set; }
    
    public string ScriptName { get; set; }
  }

  [XmlInclude(typeof(AppServerTargetMachineXml))]
  [XmlInclude(typeof(WebServerTargetMachinesXml))]
  [XmlInclude(typeof(TerminalServerTargetMachineXml))]
  [XmlInclude(typeof(SchedulerServerTargetMachinesXml))]
  [XmlInclude(typeof(DatabaseServerTargetMachineXml))]
  [XmlInclude(typeof(CustomEnvTargetMachineXml))]
  public abstract class TargetMachineXml { }

  public class AppServerTargetMachineXml : TargetMachineXml { }

  public class WebServerTargetMachinesXml : TargetMachineXml { }

  public class TerminalServerTargetMachineXml : TargetMachineXml { }

  public class SchedulerServerTargetMachinesXml : TargetMachineXml { }

  public class DatabaseServerTargetMachineXml : TargetMachineXml
  {
    public string DatabaseServerId { get; set; }
  }

  public class CustomEnvTargetMachineXml : TargetMachineXml
  {
    public string CustomEnvMachineId { get; set; }
  }
}