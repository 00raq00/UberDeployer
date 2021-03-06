﻿using System;
using System.Collections.Generic;
using System.Linq;
using UberDeployer.Common.IO;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Configuration;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Domain.Input;
using UberDeployer.Core.Management.Iis;
using UberDeployer.Core.Management.MsDeploy;

namespace UberDeployer.Core.Deployment.Tasks
{
  // TODO IMM HI: move common code up
  public class DeployWebAppDeploymentTask : DeploymentTask
  {
    private readonly IMsDeploy _msDeploy;
    private readonly IArtifactsRepository _artifactsRepository;
    private readonly IIisManager _iisManager;
    private readonly IFileAdapter _fileAdapter;
    private readonly IDirectoryAdapter _directoryAdapter;
    private readonly IZipFileAdapter _zipFileAdapter;
    private readonly IApplicationConfiguration _applicationConfiguration;

    #region Constructor(s)

    public DeployWebAppDeploymentTask(
      IProjectInfoRepository projectInfoRepository,
      IEnvironmentInfoRepository environmentInfoRepository,
      IMsDeploy msDeploy,
      IArtifactsRepository artifactsRepository,
      IIisManager iisManager,
      IFileAdapter fileAdapter,
      IZipFileAdapter zipFileAdapter,
      IApplicationConfiguration applicationConfiguration, 
      IDirectoryAdapter directoryAdapter)
      : base(projectInfoRepository, environmentInfoRepository)
    {
      Guard.NotNull(msDeploy, "msDeploy");
      Guard.NotNull(artifactsRepository, "artifactsRepository");
      Guard.NotNull(iisManager, "iisManager");
      Guard.NotNull(fileAdapter, "fileAdapter");
      Guard.NotNull(zipFileAdapter, "zipFileAdapter");
      Guard.NotNull(directoryAdapter, "directoryAdapter");

      _msDeploy = msDeploy;
      _artifactsRepository = artifactsRepository;
      _iisManager = iisManager;
      _fileAdapter = fileAdapter;
      _zipFileAdapter = zipFileAdapter;
      _applicationConfiguration = applicationConfiguration;
      _directoryAdapter = directoryAdapter;
    }

    #endregion

    #region Overrides of DeploymentTaskBase

    protected override void DoPrepare()
    {
      EnvironmentInfo environmentInfo = GetEnvironmentInfo();
      WebAppProjectInfo projectInfo = GetProjectInfo<WebAppProjectInfo>();
      WebAppInputParams inputParams = (WebAppInputParams)DeploymentInfo.InputParams;

      if (inputParams.OnlyIncludedWebMachines != null)
      {
        if (!inputParams.OnlyIncludedWebMachines.Any())
        {
          throw new DeploymentTaskException("If inputParams OnlyIncludedWebMachines has been specified, it must contain at least one web machine.");
        }

        string[] invalidMachineNames =
          inputParams.OnlyIncludedWebMachines
            .Except(environmentInfo.WebServerMachineNames)
            .ToArray();

        if (invalidMachineNames.Any())
        {
          throw new DeploymentTaskException(string.Format("Invalid web machines '{0}' have been specified.", string.Join(",", invalidMachineNames)));
        }
      }

      if (projectInfo == null)
      {
        throw new InvalidOperationException(string.Format("Project info must be of type '{0}'.", typeof(WebAppProjectInfo).FullName));
      }

      // create a step for downloading the artifacts
      var downloadArtifactsDeploymentStep =
        new DownloadArtifactsDeploymentStep(
          projectInfo,
          DeploymentInfo,
          GetTempDirPath(),
          _artifactsRepository);

      AddSubTask(downloadArtifactsDeploymentStep);

      // create a step for extracting the artifacts
      var extractArtifactsDeploymentStep =
        new ExtractArtifactsDeploymentStep(
          projectInfo,
          environmentInfo,
          DeploymentInfo,
          downloadArtifactsDeploymentStep.ArtifactsFilePath,
          GetTempDirPath(),
          _fileAdapter,
          _directoryAdapter,
          _zipFileAdapter);

      AddSubTask(extractArtifactsDeploymentStep);

      if (projectInfo.ArtifactsAreEnvironmentSpecific)
      {
        var binariesConfiguratorStep =
          new ConfigureBinariesStep(
            environmentInfo.ConfigurationTemplateName,
            GetTempDirPath());

        AddSubTask(binariesConfiguratorStep);
      }

      WebAppProjectConfiguration configuration =
        environmentInfo.GetWebAppProjectConfiguration(projectInfo);

      CheckConfiguration(configuration);

      string webSiteName = configuration.WebSiteName;
      string webAppName = configuration.WebAppName;
      IisAppPoolInfo appPoolInfo = environmentInfo.GetAppPoolInfo(configuration.AppPoolId);

      IEnumerable<string> webMachinesToDeployTo =
        (inputParams.OnlyIncludedWebMachines ?? environmentInfo.WebServerMachineNames)
          .Distinct();

      foreach (string webServerMachineName in webMachinesToDeployTo)
      {
/* // TODO IMM HI: xxx we don't need this for now - should we parameterize this somehow?
        string webApplicationPhysicalPath =
          _iisManager.GetWebApplicationPath(
            webServerMachineName,
            string.Format("{0}/{1}", webSiteName, webAppName));

        if (!string.IsNullOrEmpty(webApplicationPhysicalPath))
        {
          string webApplicationNetworkPath =
            string.Format(
              "\\\\{0}\\{1}${2}",
              webServerMachineName,
              webApplicationPhysicalPath[0],
              webApplicationPhysicalPath.Substring(2));

          if (Directory.Exists(webApplicationNetworkPath))
          {
            var backupFilesDeploymentStep =
              new BackupFilesDeploymentStep(
                webApplicationNetworkPath);

            AddSubTask(backupFilesDeploymentStep);
          }
        }
*/

        // create a step for creating a WebDeploy package
        // TODO IMM HI: add possibility to specify physical path on the target machine
        var createWebDeployPackageDeploymentStep =
          new CreateWebDeployPackageDeploymentStep(
            _msDeploy,
            new Lazy<string>(() => extractArtifactsDeploymentStep.BinariesDirPath),
            webSiteName,
            webAppName);

        AddSubTask(createWebDeployPackageDeploymentStep);

        // create a step for deploying the WebDeploy package to the target machine
        var deployWebDeployPackageDeploymentStep =
          new DeployWebDeployPackageDeploymentStep(
            _msDeploy,
            webServerMachineName,
            new Lazy<string>(() => createWebDeployPackageDeploymentStep.PackageFilePath));

        AddSubTask(deployWebDeployPackageDeploymentStep);

        // check if the app pool exists on the target machine
        if (_applicationConfiguration.CheckIfAppPoolExists && !_iisManager.AppPoolExists(webServerMachineName, appPoolInfo.Name))
        {
          // create a step for creating a new app pool
          var createAppPoolDeploymentStep =
            new CreateAppPoolDeploymentStep(
              _iisManager,
              webServerMachineName,
              appPoolInfo);

          AddSubTask(createAppPoolDeploymentStep);

          // create a step for assigning the app pool to the web application
          var setAppPoolDeploymentStep =
            new SetAppPoolDeploymentStep(
              _iisManager,
              webServerMachineName,
              webSiteName,
              appPoolInfo,
              webAppName);

          AddSubTask(setAppPoolDeploymentStep);
        }
      }
    }

    private void CheckConfiguration(WebAppProjectConfiguration configuration)
    {
      if (string.IsNullOrEmpty(configuration.WebAppName))
      {
        List<WebAppProjectInfo> webAppProjects = GetProjects<WebAppProjectInfo>();
        
        if (webAppProjects.Count(x => x.WebSiteName == configuration.WebSiteName) > 1)
        {
          throw new DeploymentTaskException(string.Format("Configuration error, WebAppName is empty and there is more than one application on specified WebSiteName: '{0}'.", configuration.WebSiteName));
        }
      }
    }

    public override string Description
    {
      get
      {
        return
          string.Format(
            "Deploy web app '{0} ({1}:{2})' to '{3}'.",
            DeploymentInfo.ProjectName,
            DeploymentInfo.ProjectConfigurationName,
            DeploymentInfo.ProjectConfigurationBuildId,
            DeploymentInfo.TargetEnvironmentName);
      }
    }

    #endregion
  }
}
