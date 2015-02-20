﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Moq;
using NUnit.Framework;
using UberDeployer.Core.Deployment;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Management.Db;
using UberDeployer.Core.Tests.Generators;
using UberDeployer.Core.Tests.TestUtils;

namespace UberDeployer.Core.Tests.Deployment
{
  [TestFixture]
  public class GatherDbScriptsToRunDeploymentStepTests
  {
    private GatherDbScriptsToRunDeploymentStep _deploymentStep;
    private const string _ScriptPath = "TestData/TestSqlScripts";
    private const string _SqlServerName = "sqlServerName";
    private const string _Environment = "env";

    private Mock<IDbVersionProvider> _dbVersionProviderFake;
    private Mock<IScriptsToRunWebSelector> _scriptsToRunWebSelector;

    [SetUp]
    public void SetUp()
    {
      _dbVersionProviderFake = new Mock<IDbVersionProvider>(MockBehavior.Loose);
      _scriptsToRunWebSelector = new Mock<IScriptsToRunWebSelector>(MockBehavior.Loose);

      DbProjectInfo dbProjectInfo = ProjectInfoGenerator.GetDbProjectInfo();

      DeploymentInfo di = DeploymentInfoGenerator.GetDbDeploymentInfo();

      _deploymentStep = new GatherDbScriptsToRunDeploymentStep(dbProjectInfo.DbName, new Lazy<string>(() => _ScriptPath), _SqlServerName, _Environment, di, _dbVersionProviderFake.Object, _scriptsToRunWebSelector.Object);
    }

    [Test]
    [TestCase("scriptsDirectoryPath", typeof(ArgumentNullException))]
    [TestCase("sqlServerName", typeof(ArgumentException))]
    [TestCase("environmentName", typeof(ArgumentException))]
    [TestCase("dbVersionProvider", typeof(ArgumentNullException))]
    public void Constructor_fails_when_parameter_is_null(string nullParamName, Type expectedExceptionType)
    {
      Assert.Throws(
        expectedExceptionType,
        () => ReflectionTestTools.CreateInstance<GatherDbScriptsToRunDeploymentStep>(GetDefaultConstructorParams(), nullParamName));
    }

    [Test]
    public void Description_is_not_empty()
    {
      _deploymentStep.Prepare();

      Assert.IsNotNullOrEmpty(_deploymentStep.Description);
    }

    [Test]
    public void DoExecute_calls_DbVersionProvider()
    {
      // arrange
      _dbVersionProviderFake
        .Setup(x => x.GetVersions(It.IsAny<string>(), It.IsAny<string>())).
        Returns(new List<string>() { "1.2", "1.3" });

      // act
      _deploymentStep.PrepareAndExecute();

      // assert
      _dbVersionProviderFake.VerifyAll();
    }

    [Test]
    public void DoExecute_gathers_not_executed_scripts()
    {
      // arrange
      string[] executedScriptsVersion = new[] { "1.2", "1.3" };
      const string notExecutedScript = "1.4.sql";

      _dbVersionProviderFake
        .Setup(x => x.GetVersions(It.IsAny<string>(), It.IsAny<string>())).
        Returns(executedScriptsVersion);

      // act
      _deploymentStep.PrepareAndExecute();

      // assert
      Assert.IsTrue(_deploymentStep.ScriptsToRun.Any(x => Path.GetFileName(x.ScriptPath) == notExecutedScript));
    }

    [Test]
    public void DoExecute_not_gathers_older_scripts_than_current()
    {
      // arrange
      string[] executedScriptsVersion = new[] { "1.3" };
      const string scriptOlderThanCurrent = "1.2.sql";

      _dbVersionProviderFake
        .Setup(x => x.GetVersions(It.IsAny<string>(), It.IsAny<string>())).
        Returns(executedScriptsVersion);

      // act
      _deploymentStep.PrepareAndExecute();

      // assert
      Assert.IsFalse(_deploymentStep.ScriptsToRun.Any(x => Path.GetFileName(x.ScriptPath) == scriptOlderThanCurrent));
    }

    [Test]
    public void DoExecute_not_gathers_scripts_with_not_supported_name()
    {
      // arrange
      string[] executedScriptsVersion = new[] { "1.2" };
      const string notSupportedScript = "1.3a.sql";

      _dbVersionProviderFake
        .Setup(x => x.GetVersions(It.IsAny<string>(), It.IsAny<string>())).
        Returns(executedScriptsVersion);

      // act
      _deploymentStep.PrepareAndExecute();

      // assert
      Assert.IsFalse(_deploymentStep.ScriptsToRun.Any(x => Path.GetFileName(x.ScriptPath) == notSupportedScript));
    }

    [Test]
    public void DoExecute_gathers_scripts_marked_as_non_transactional()
    {
      // arrange
      string[] executedScriptsVersion = new[] { "1.2", "1.3" };

      const string nonTransactionalScriptToExecute = "1.3.notrans.sql";

      _dbVersionProviderFake
        .Setup(x => x.GetVersions(It.IsAny<string>(), It.IsAny<string>())).
        Returns(executedScriptsVersion);

      // act
      _deploymentStep.PrepareAndExecute();

      // assert
      Assert.IsTrue(_deploymentStep.ScriptsToRun.Any(x => Path.GetFileName(x.ScriptPath) == nonTransactionalScriptToExecute));
    }

    private OrderedDictionary GetDefaultConstructorParams()
    {
      return
        new OrderedDictionary
        {
          { "dbName", "database_name" },
          { "scriptsDirectoryPath", new Lazy<string>(() => _ScriptPath) },
          { "sqlServerName", _SqlServerName },
          { "environmentName", _Environment },
          { "dbVersionProvider", _dbVersionProviderFake.Object }
        };
    }
  }
}