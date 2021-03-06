﻿using System;

using AutoMapper;

using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Domain.Input;

using ExtensionInputParams = UberDeployer.Core.Domain.Input.ExtensionInputParams;

namespace UberDeployer.Agent.Service
{
  public static class DtoMapper
  {
    static DtoMapper()
    {
      Mapper.CreateMap<Core.Domain.ProjectInfo, Proxy.Dto.ProjectInfo>()
        .Include<Core.Domain.NtServiceProjectInfo, Proxy.Dto.NtServiceProjectInfo>()
        .Include<Core.Domain.WebAppProjectInfo, Proxy.Dto.WebAppProjectInfo>()
        .Include<Core.Domain.WebServiceProjectInfo, Proxy.Dto.WebServiceProjectInfo>()
        .Include<Core.Domain.TerminalAppProjectInfo, Proxy.Dto.TerminalAppProjectInfo>()
        .Include<Core.Domain.SchedulerAppProjectInfo, Proxy.Dto.SchedulerAppProjectInfo>()
        .Include<Core.Domain.DbProjectInfo, Proxy.Dto.DbProjectInfo>()
        .Include<Core.Domain.UberDeployerAgentProjectInfo, Proxy.Dto.UberDeployerAgentProjectInfo>()
        .Include<Core.Domain.ExtensionProjectInfo, Proxy.Dto.ProjectInfo>()
        .Include<Core.Domain.PowerShellScriptProjectInfo, Proxy.Dto.PowerShellScriptProjectInfo>();

      Mapper.CreateMap<Core.Domain.NtServiceProjectInfo, Proxy.Dto.NtServiceProjectInfo>();
      Mapper.CreateMap<Core.Domain.WebAppProjectInfo, Proxy.Dto.WebAppProjectInfo>();
      Mapper.CreateMap<Core.Domain.WebServiceProjectInfo, Proxy.Dto.WebServiceProjectInfo>();
      Mapper.CreateMap<Core.Domain.TerminalAppProjectInfo, Proxy.Dto.TerminalAppProjectInfo>();
      Mapper.CreateMap<Core.Domain.SchedulerAppProjectInfo, Proxy.Dto.SchedulerAppProjectInfo>();
      Mapper.CreateMap<Core.Domain.DbProjectInfo, Proxy.Dto.DbProjectInfo>();
      Mapper.CreateMap<Core.Domain.UberDeployerAgentProjectInfo, Proxy.Dto.UberDeployerAgentProjectInfo>();
      Mapper.CreateMap<Core.Domain.ExtensionProjectInfo, Proxy.Dto.ProjectInfo>();
      Mapper.CreateMap<Core.Domain.PowerShellScriptProjectInfo, Proxy.Dto.PowerShellScriptProjectInfo>();

      Mapper.CreateMap<Core.Domain.IisAppPoolInfo, Proxy.Dto.IisAppPoolInfo>();

      Mapper.CreateMap<Core.Domain.DatabaseServer, Proxy.Dto.DatabaseServer>();

      Mapper.CreateMap<Core.Domain.EnvironmentInfo, Proxy.Dto.EnvironmentInfo>();

      Mapper.CreateMap<Core.Domain.EnvironmentUser, Proxy.Dto.EnvironmentUser>();

      Mapper.CreateMap<Core.Domain.SchedulerAppTask, Proxy.Dto.SchedulerAppTask>();

      Mapper.CreateMap<Core.Domain.Repetition, Proxy.Dto.Repetition>();

      Mapper.CreateMap<Core.Domain.ProjectToFailoverClusterGroupMapping, Proxy.Dto.ProjectToFailoverClusterGroupMapping>();

      Mapper.CreateMap<Core.Domain.WebAppProjectConfigurationOverride, Proxy.Dto.WebAppProjectConfigurationOverride>();
      Mapper.CreateMap<Core.Domain.DbProjectConfigurationOverride, Proxy.Dto.DbProjectConfigurationOverride>();

      Mapper.CreateMap<Core.Deployment.Pipeline.Modules.DeploymentRequest, Proxy.Dto.DeploymentRequest>();

      Mapper.CreateMap<Core.Deployment.DiagnosticMessage, Proxy.Dto.DiagnosticMessage>();
      Mapper.CreateMap<Core.Deployment.DiagnosticMessageType, Proxy.Dto.DiagnosticMessageType>();

      Mapper.CreateMap<Proxy.Dto.DbScriptsToRunSelection, Core.Deployment.DbScriptsToRunSelection>();

      Mapper.CreateMap<Core.TeamCity.ApiModels.TeamCityBuildTypeBranch, Proxy.Dto.TeamCity.ProjectConfigurationBranch>();
      Mapper.CreateMap<Core.TeamCity.ApiModels.TeamCityBuildType, Proxy.Dto.TeamCity.ProjectConfiguration>();
      Mapper.CreateMap<Core.TeamCity.ApiModels.TeamCityBuild, Proxy.Dto.TeamCity.ProjectConfigurationBuild>();

      Mapper.CreateMap<Proxy.Dto.DependentProject, Core.ExternalDataCollectors.DependentProjectsSelection.DependentProject>();

      Mapper.CreateMap<Core.Domain.TerminalServerMachine, Proxy.Dto.TerminalServerMachine>();

      Mapper.AssertConfigurationIsValid();
    }

    public static TResult Map<TInput, TResult>(TInput input)
    {
      return Mapper.Map<TInput, TResult>(input);
    }

    //TODO MARIO move to other converter?
    public static Core.Domain.DeploymentInfo ConvertDeploymentInfo(Proxy.Dto.DeploymentInfo deploymentInfo, Core.Domain.ProjectInfo projectInfo)
    {
      InputParams inputParams = ConvertInputParams(deploymentInfo.InputParams);

      return
        new Core.Domain.DeploymentInfo(
          deploymentInfo.DeploymentId,
          deploymentInfo.IsSimulation,
          deploymentInfo.ProjectName,
          deploymentInfo.ProjectConfigurationName,
          deploymentInfo.ProjectConfigurationBuildId,
          deploymentInfo.TargetEnvironmentName,
          inputParams);
    }

    private static InputParams ConvertInputParams(Proxy.Dto.Input.InputParams inputParams)
    {
      Guard.NotNull(inputParams, "inputParams");

      var dbInputParams = inputParams as Proxy.Dto.Input.DbInputParams;

      if (dbInputParams != null)
      {
        return new DbInputParams();
      }

      var ntServiceInputParams = inputParams as Proxy.Dto.Input.NtServiceInputParams;

      if (ntServiceInputParams != null)
      {
        return new NtServiceInputParams();
      }

      var schedulerAppInputParams = inputParams as Proxy.Dto.Input.SchedulerAppInputParams;

      if (schedulerAppInputParams != null)
      {
        return new SchedulerAppInputParams();
      }

      var terminalAppInputParams = inputParams as Proxy.Dto.Input.TerminalAppInputParams;

      if (terminalAppInputParams != null)
      {
        return new TerminalAppInputParams();
      }

      var webAppInputParams = inputParams as Proxy.Dto.Input.WebAppInputParams;

      if (webAppInputParams != null)
      {
        return
          new WebAppInputParams(
            webAppInputParams.OnlyIncludedWebMachines);
      }

      var webServiceInputParams = inputParams as Proxy.Dto.Input.WebServiceInputParams;

      if (webServiceInputParams != null)
      {
        return new WebServiceInputParams();
      }

      var extensionInputParams = inputParams as Proxy.Dto.Input.ExtensionInputParams;

      if (extensionInputParams != null)
      {
        return new ExtensionInputParams();
      }

      var powerShellScriptInputParams = inputParams as Proxy.Dto.Input.PowerShellScriptInputParams;
      if (powerShellScriptInputParams != null)
      {
        return new PowerShellInputParams();
      }

      throw new NotSupportedException(string.Format("Unknown input params type: '{0}'.", inputParams.GetType().FullName));
    }
  }
}