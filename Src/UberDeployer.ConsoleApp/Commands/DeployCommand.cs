﻿using System;
using System.Reflection;
using System.Security.Principal;

using log4net;

using UberDeployer.Common;
using UberDeployer.CommonConfiguration;
using UberDeployer.ConsoleApp.Commander;
using UberDeployer.Core.Deployment;
using UberDeployer.Core.Deployment.Pipeline;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain;

namespace UberDeployer.ConsoleApp.Commands
{
  public class DeployCommand : ConsoleCommand
  {
    private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public DeployCommand(CommandDispatcher commandDispatcher)
      : base(commandDispatcher)
    {
    }

    public override int Run(string[] args)
    {
      if (args.Length != 4 && args.Length != 5)
      {
        DisplayCommandUsage();

        return 1;
      }

      IProjectInfoRepository projectInfoRepository =
        ObjectFactory.Instance.CreateProjectInfoRepository();

      string projectName = args[0];
      string projectConfigurationName = args[1];
      string projectConfigurationBuildId = args[2];
      string targetEnvironmentName = args[3];
      bool isSimulation = (args.Length >= 5 ? string.Equals(args[4], "simulate", StringComparison.OrdinalIgnoreCase) : false);

      ProjectInfo projectInfo = projectInfoRepository.FindByName(projectName);

      if (projectInfo == null)
      {
        OutputWriter.WriteLine("Project named '{0}' doesn't exist.", projectName);
        return 1;
      }

      Guid deploymentId = Guid.NewGuid();

      var deploymentInfo =
        new DeploymentInfo(
          deploymentId,
          isSimulation,
          projectName,
          projectConfigurationName,
          projectConfigurationBuildId,
          targetEnvironmentName,
          projectInfo.CreateEmptyInputParams());

      try
      {
        DeploymentTask deploymentTask =
          projectInfo.CreateDeploymentTask(ObjectFactory.Instance);

        IDeploymentPipeline deploymentPipeline =
          ObjectFactory.Instance.CreateDeploymentPipeline();

        deploymentPipeline.DiagnosticMessagePosted +=
          (sender, tmpArgs) => LogMessage(tmpArgs.Message, tmpArgs.MessageType);

        var deploymentContext = new DeploymentContext(RequesterIdentity);

        deploymentPipeline.StartDeployment(deploymentInfo, deploymentTask, deploymentContext, false);

        return 0;
      }
      catch (Exception exc)
      {
        LogMessage("Error: " + exc.Message, DiagnosticMessageType.Error, exc);

        return 1;
      }
    }

    public override void DisplayCommandUsage()
    {
      OutputWriter.WriteLine("Usage: {0} project projectConfiguration buildId targetEnvironment", CommandName);
    }

    protected void LogMessage(string message, DiagnosticMessageType messageType, Exception exception = null)
    {
      switch (messageType)
      {
        case DiagnosticMessageType.Trace:
          _log.TraceIfEnabled(() => message);
          break;

        case DiagnosticMessageType.Info:
          _log.InfoIfEnabled(() => message);
          break;

        case DiagnosticMessageType.Warn:
          _log.WarnIfEnabled(() => message, exception);
          break;

        case DiagnosticMessageType.Error:
          _log.ErrorIfEnabled(() => message, exception);
          break;

        default:
          throw new ArgumentOutOfRangeException("messageType", messageType, null);
      }
    }

    public override string CommandName
    {
      get
      {
        return "deploy";
      }
    }

    private static string RequesterIdentity
    {
      get
      {
        var windowsIdentity = WindowsIdentity.GetCurrent();

        if (windowsIdentity == null)
        {
          throw new InternalException("Couldn't get requester identity.");
        }

        return windowsIdentity.Name;
      }
    }
  }
}