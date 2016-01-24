using System;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Conventions.AcceptanceCriteria;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain.Input;

namespace UberDeployer.Core.Domain
{
  public class PowerShellScriptProjectInfo : ProjectInfo
  {
    public string _scriptPath;

    public PowerShellScriptProjectInfo(
      string name, 
      string artifactsRepositoryName, 
      IEnumerable<string> allowedEnvironmentNames, 
      string artifactsRepositoryDirName, 
      bool artifactsAreNotEnvironmentSpecific,
      TargetMachine targetMachine,
      string scriptName,
      bool isRemote,
      List<string> dependendProjectNames = null)
      : base(
        name, 
        artifactsRepositoryName, 
        allowedEnvironmentNames, 
        dependendProjectNames, 
        artifactsRepositoryDirName, 
        artifactsAreNotEnvironmentSpecific)
    {
      Guard.NotNullNorEmpty(scriptName, "scriptName");

      TargetMachine = targetMachine;
      ScriptName = scriptName;
      IsRemote = isRemote;
    }

    public override ProjectType Type
    {
      get { return ProjectType.PowerShellScript; }
    }

    public override InputParams CreateEmptyInputParams()
    {
      return new PowerShellInputParams();
    }

    public override DeploymentTask CreateDeploymentTask(IObjectFactory objectFactory)
    {
      return
        new DeployPowerShellScriptDeploymentTask(
          objectFactory.CreateProjectInfoRepository(),
          objectFactory.CreateEnvironmentInfoRepository(),
          objectFactory.CreateArtifactsRepository(),
          objectFactory.CreateFileAdapter(),
          objectFactory.CreateDirectoryAdapter(),
          objectFactory.CreateZipFileAdapter());
    }

    public override IEnumerable<string> GetTargetFolders(IObjectFactory objectFactory, EnvironmentInfo environmentInfo)
    {
      throw new System.NotImplementedException();
    }

    public override string GetMainAssemblyFileName()
    {
      throw new System.NotImplementedException();
    }

    public TargetMachine TargetMachine { get; private set; }
    
    public string ScriptName { get; private set; }

    public bool IsRemote { get; private set; }

    public IEnumerable<string> GetTargetMachines(EnvironmentInfo environmentInfo)
    {
      if (IsRemote == false)
      {
        return new string[] { };
      }

      if (TargetMachine == null || TargetMachine == null)
      {
        throw new DeploymentTaskException("Target machine to run PowerShell script is not specified. Set 'ExecuteOnMachine' property in ProjectInfos.xml");
      }

      var customEnvTargetMachine = TargetMachine as CustomEnvTargetMachine;
      if (customEnvTargetMachine != null)
      {
        return new [] { environmentInfo.GetCustomEnvMachine(customEnvTargetMachine.CustomEnvMachineId).MachineName };
      }

      var appServerMachine = TargetMachine as AppServerTargetMachine;
      if (appServerMachine != null)
      {
        if (environmentInfo.EnableFailoverClusteringForNtServices)
        {
          throw new NotSupportedException("Getting target machines for failover cluster is not supported.");
        }

        return new [] { environmentInfo.AppServerMachineName };
      }

      var webServerMachines = TargetMachine as WebServerTargetMachines;
      if (webServerMachines != null)
      {
        return environmentInfo.WebServerMachineNames;
      }

      var terminalServerMachine = TargetMachine as TerminalServerTargetMachine;
      if (terminalServerMachine != null)
      {
        return environmentInfo.TerminalServerMachines
          .Select(x => x.MachineName);
      }

      var schedulerServerMachines = TargetMachine as SchedulerServerTargetMachines;
      if (schedulerServerMachines != null)
      {
        return environmentInfo.SchedulerServerTasksMachineNames;
      }

      var databaseServerMachine = TargetMachine as DatabaseServerTargetMachine;
      if (databaseServerMachine != null)
      {
        return new [] { environmentInfo.GetDatabaseServer(databaseServerMachine.DatabaseServerId).MachineName };
      }

      throw new DeploymentTaskException(string.Format("Target Machine type is not supported [{0}]", TargetMachine));
    }
  }

  public abstract class TargetMachine { }

  public class AppServerTargetMachine : TargetMachine
  {
  }

  public class WebServerTargetMachines : TargetMachine { }

  public class TerminalServerTargetMachine : TargetMachine { }

  public class SchedulerServerTargetMachines : TargetMachine { }

  public class DatabaseServerTargetMachine : TargetMachine
  {
    public string DatabaseServerId { get; set; }
  }

  public class CustomEnvTargetMachine : TargetMachine
  {
    public string CustomEnvMachineId { get; set; }
  }
}
