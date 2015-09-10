﻿using System;
using System.Collections.Generic;
using System.Linq;

using Moq;

using NUnit.Framework;

using UberDeployer.Common.IO;
using UberDeployer.Core.Configuration;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Domain.Input;
using UberDeployer.Core.Management.Iis;
using UberDeployer.Core.Management.MsDeploy;
using UberDeployer.Tests.Core.Generators;

namespace UberDeployer.Tests.Core.Deployment
{
  [TestFixture]
  public class DeployWebAppDeploymentTaskTests
  {
    // SUT
    private DeployWebAppDeploymentTask _deployWebAppDeploymentTask;

    private Mock<IMsDeploy> _msDeploy;
    private Mock<IProjectInfoRepository> _projectInfoRepositoryFake;
    private Mock<IEnvironmentInfoRepository> _environmentInfoRepositoryFake;
    private Mock<IArtifactsRepository> _artifactsRepository;
    private Mock<IIisManager> _iisManager;
    private Mock<IFileAdapter> _fileAdapterFake;
    private Mock<IZipFileAdapter> _zipFileAdapterFake;
    private Mock<IApplicationConfiguration> _applicationConfigurationFake;
    private Mock<IDirectoryAdapter> _directoryAdapterFake;

    private WebAppProjectInfo _projectInfo;
    private EnvironmentInfo _environmentInfo;

    [SetUp]
    public virtual void SetUp()
    {
      _msDeploy = new Mock<IMsDeploy>();
      _artifactsRepository = new Mock<IArtifactsRepository>();
      _projectInfoRepositoryFake = new Mock<IProjectInfoRepository>(MockBehavior.Loose);
      _environmentInfoRepositoryFake = new Mock<IEnvironmentInfoRepository>();
      _iisManager = new Mock<IIisManager>();
      _fileAdapterFake = new Mock<IFileAdapter>(MockBehavior.Loose);
      _zipFileAdapterFake = new Mock<IZipFileAdapter>(MockBehavior.Loose);
      _applicationConfigurationFake = new Mock<IApplicationConfiguration>();
      _directoryAdapterFake = new Mock<IDirectoryAdapter>();

      _projectInfo = ProjectInfoGenerator.GetWebAppProjectInfo();
      _environmentInfo = DeploymentDataGenerator.GetEnvironmentInfo();

      _deployWebAppDeploymentTask =
        new DeployWebAppDeploymentTask(
          _projectInfoRepositoryFake.Object,
          _environmentInfoRepositoryFake.Object,
          _msDeploy.Object,
          _artifactsRepository.Object,
          _iisManager.Object,
          _fileAdapterFake.Object,
          _zipFileAdapterFake.Object,
          _applicationConfigurationFake.Object,
          _directoryAdapterFake.Object);

      _deployWebAppDeploymentTask.Initialize(DeploymentInfoGenerator.GetWebAppDeploymentInfo());

      _projectInfoRepositoryFake.Setup(x => x.FindByName(It.IsAny<string>()))
        .Returns(_projectInfo);

      _environmentInfoRepositoryFake.Setup(x => x.FindByName(It.IsAny<string>()))
        .Returns(_environmentInfo);
    }

    [Test]
    [TestCaseSource("GetInvalidWebMachineNames")]
    public void Prepare_should_throw_exception_when_web_machines_are_invalid(List<string> webMachines)
    {
      // Arrange
      var webInputParams =
        new WebAppInputParams(webMachines);

      DeploymentInfo deploymentInfo =
        new DeploymentInfo(
          Guid.NewGuid(),
          false,
          "projectName",
          "projectConfigurationName",
          "projectConfigurationBuildId",
          "targetEnvironmentName",
          webInputParams);

      _deployWebAppDeploymentTask.Initialize(deploymentInfo);

      // Act assert
      Assert.Throws<DeploymentTaskException>(() => _deployWebAppDeploymentTask.Prepare());
    }

    [Test]
    public void Prepare_should_throw_exception_when_web_appplication_name_is_empty_and_web_site_has_more_than_one_application()
    {
      // arrange  
      const string webSiteName = "webSiteName";

      _projectInfo = GetWebProjectInfoWithEmptyAppName(webSiteName);
      
      _projectInfoRepositoryFake.Setup(x => x.FindByName(It.IsAny<string>()))
        .Returns(_projectInfo);

      _projectInfoRepositoryFake.Setup(x => x.GetAll())
        .Returns(new List<WebAppProjectInfo>
        {
          GetWebProjectInfoWithEmptyAppName(webSiteName),
          GetWebProjectInfoWithEmptyAppName(webSiteName)
        });

      // act assert
      Assert.Throws<DeploymentTaskException>(() => _deployWebAppDeploymentTask.Prepare());
    }

    private static WebAppProjectInfo GetWebProjectInfoWithEmptyAppName(string webSiteName)
    {
      return new WebAppProjectInfo(
        "webproj",
        "artifacts_repository_name",
        new[] { "env_name" },
        "artifacts_repository_dir_name",
        true,
        "app_pool_id",
        webSiteName,
        "web_app_dir_name",
        null);
    }

    [Test]
    public void Prepare_should_create_subTask_AppPoolDeploymentStep_when_app_pool_does_not_exist_and_flag_is_on()
    {
      // Arrange
      _iisManager.Setup(x => x.AppPoolExists(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
      _applicationConfigurationFake.SetupGet(x => x.CheckIfAppPoolExists).Returns(true);

      // Act
      _deployWebAppDeploymentTask.Prepare();

      // Assert
      Assert.IsTrue(_deployWebAppDeploymentTask.SubTasks.Any(st => st is CreateAppPoolDeploymentStep));
    }

    [Test]
    public void Prepare_should_not_create_subTask_AppPoolDeploymentStep_when_the_flag_is_off()
    {
      // Arrange
      _iisManager.Setup(x => x.AppPoolExists(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
      _applicationConfigurationFake.SetupGet(x => x.CheckIfAppPoolExists).Returns(false);

      // Act
      _deployWebAppDeploymentTask.Prepare();

      // Assert
      Assert.IsFalse(_deployWebAppDeploymentTask.SubTasks.Any(st => st is CreateAppPoolDeploymentStep));
    }

    // ReSharper disable UnusedMethodReturnValue.Local

    private static IEnumerable<List<string>> GetInvalidWebMachineNames()
    {
      yield return new List<string>();
      yield return new List<string> { "incorrectWebmachine" };
    }

    // ReSharper restore UnusedMethodReturnValue.Local
  }
}
