﻿using System.Xml.Serialization;

namespace UberDeployer.Core.DataAccess.Xml.ProjectInfos
{
  public class PowerShellScriptProjectInfoXml : ProjectInfoXml
  {
    public TargetMachineXml TargetMachine { get; set; }
  }

  [XmlInclude(typeof(AppServerTargetMachineXml))]
  [XmlInclude(typeof(WebServerTargetMachinesXml))]
  [XmlInclude(typeof(TerminalServerTargetMachineXml))]
  [XmlInclude(typeof(SchedulerServerTargetMachinesXml))]
  [XmlInclude(typeof(DatabaseServerTargetMachineXml))]
  public abstract class TargetMachineXml { }

  public class AppServerTargetMachineXml : TargetMachineXml { }

  public class WebServerTargetMachinesXml : TargetMachineXml { }

  public class TerminalServerTargetMachineXml : TargetMachineXml { }

  public class SchedulerServerTargetMachinesXml : TargetMachineXml { }

  public class DatabaseServerTargetMachineXml : TargetMachineXml
  {
    public string DatabaseServerId { get; set; }
  }
}