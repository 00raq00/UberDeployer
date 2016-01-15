﻿using System;
﻿using System.Collections;
﻿using System.Collections.Generic;
﻿using System.IO;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

using UberDeployer.Core.DataAccess.Xml;
﻿using UberDeployer.Core.DataAccess.Xml.ProjectInfo;
﻿using UberDeployer.Core.Domain;

namespace UberDeployer.Tests.Core.DataAccess
{
  [TestFixture]
  public class XmlProjectInfoRepositoryTests
  {
    private XmlProjectInfoRepository _projectInfoRepository;

    [SetUp]
    public void SetUp()
    {
      const string filename = "TestProjectInfo.xml";
      string path = Path.Combine("Core\\DataAccess\\Xml", filename);
      _projectInfoRepository = new XmlProjectInfoRepository(path);
    }

    [Test]
    public void FileLoadSuccessful()
    {
      Assert.DoesNotThrow(() => _projectInfoRepository.GetAll());
    }

    [Test]
    public void DbSettingsLoadUsersCorrectly()
    {
      ProjectInfo info = _projectInfoRepository.FindByName("UberDeployer.SampleDb");
      
      DbProjectInfo dbInfo = (DbProjectInfo)info;

      Assert.AreEqual(3,dbInfo.Users.Count);
      Assert.AreEqual("Username1", dbInfo.Users.First());
    }

    [Test]
    public void NtSettingsLoadDependendProjectsCorrectly()
    {
      ProjectInfo info = _projectInfoRepository.FindByName("UberDeployer.SampleNtService");
      
      NtServiceProjectInfo ntServiceProjectInfo = (NtServiceProjectInfo)info;

      Assert.AreEqual(1, ntServiceProjectInfo.DependendProjectNames.Count);
      Assert.IsTrue(string.Equals("UberDeployer.SampleDependendNtService", ntServiceProjectInfo.DependendProjectNames.First()));
    }

    [Test]
    public void FindProjectNameWithDependencies_throws_exception_when_configuration_not_found()
    {
      var output = _projectInfoRepository.FindProjectNameWithDependencies("xyz");
      Assert.AreEqual(0, output.Count);
    }

    [Test]
    public void FindProjectNameWithDependencies_find_district_dependencies()
    {
      var dependencyList = _projectInfoRepository.FindProjectNameWithDependencies("UberDeployer.SampleNtServiceWithDependences");
      Assert.AreEqual(4, dependencyList.Count);
      Assert.AreEqual("UberDeployer.SampleDb", dependencyList[0].Name);
      Assert.AreEqual("UberDeployer.SampleNtDependendService", dependencyList[1].Name);
      Assert.AreEqual("UberDeployer.SampleWebApp", dependencyList[2].Name);
      Assert.AreEqual("UberDeployer.SampleNtServiceWithDependences", dependencyList[3].Name);
    }

    [Test]
    public void FindProjectNameWithDependencies_detects_cycles_in_dependencies()
    {
      var dependencyList = _projectInfoRepository.FindProjectNameWithDependencies("UberDeployer.SampleNtDependendServiceWithCycle1");
      Assert.AreEqual(2, dependencyList.Count);
      Assert.IsTrue(dependencyList.Any(x => x.Name == "UberDeployer.SampleNtDependendServiceWithCycle1"));
      Assert.IsTrue(dependencyList.Any(x => x.Name == "UberDeployer.SampleNtDependendServiceWithCycle2"));
    }

    [TestCaseSource("GetPowerShellScriptProjectInfoTestCases")]
    public void PowerShellScriptProjectInfo_is_loaded_properly_with_target_machine(string projectName, Type expectedTargetMachineType)
    {
      // act
      ProjectInfo projectInfo = _projectInfoRepository.FindByName(projectName);

      // assert
      var powerShellScriptProjectInfo = projectInfo as PowerShellScriptProjectInfo;
      Assert.IsNotNull(powerShellScriptProjectInfo);

      Assert.IsInstanceOf(expectedTargetMachineType, powerShellScriptProjectInfo.TargetMachine);
    }

    [Test]
    public void PowerShellScriptProjectInfo_loads_properly_DatabaseServerId()
    {
      // arrange
      const string expectedDatabaseServerId = "Database.Server";

      // act
      ProjectInfo projectInfo = _projectInfoRepository.FindByName("UberDeployer.SamplePowerShellScriptProjectForDatabaseServer");

      // assert
      var powerShellScriptProjectInfo = projectInfo as PowerShellScriptProjectInfo;
      Assert.IsNotNull(powerShellScriptProjectInfo);
      
      var databaseServerTargetMachine = powerShellScriptProjectInfo.TargetMachine as DatabaseServerTargetMachine;
      Assert.IsNotNull(databaseServerTargetMachine);
      Assert.AreEqual(expectedDatabaseServerId, databaseServerTargetMachine.DatabaseServerId);
    }

    [Test]
    public void PowerShellScriptProjectInfo_loads_properly_CustomMachineId()
    {
      // arrange
      const string expectedMachineId = "SamplePSScriptTargetMachine";

      // act
      ProjectInfo projectInfo = _projectInfoRepository.FindByName("UberDeployer.SamplePowerShellScriptProjectForCustomEnvMachine");

      // assert
      var powerShellScriptProjectInfo = projectInfo as PowerShellScriptProjectInfo;
      Assert.IsNotNull(powerShellScriptProjectInfo);

      var customEnvTargetMachine = powerShellScriptProjectInfo.TargetMachine as CustomEnvTargetMachine;
      Assert.IsNotNull(customEnvTargetMachine);
      Assert.AreEqual(expectedMachineId, customEnvTargetMachine.CustomEnvMachineId);
    }

    public static IEnumerable<TestCaseData> GetPowerShellScriptProjectInfoTestCases()
    {
      yield return new TestCaseData("UberDeployer.SamplePowerShellScriptProjectForAppServer", typeof(AppServerTargetMachine))
        .SetName(typeof(AppServerTargetMachine).Name);

      yield return new TestCaseData("UberDeployer.SamplePowerShellScriptProjectForWebServer", typeof(WebServerTargetMachines))
        .SetName(typeof(WebServerTargetMachines).Name);

      yield return new TestCaseData("UberDeployer.SamplePowerShellScriptProjectForTerminalServer", typeof(TerminalServerTargetMachine))
        .SetName(typeof(TerminalServerTargetMachine).Name);

      yield return new TestCaseData("UberDeployer.SamplePowerShellScriptProjectForSchedulerServer", typeof(SchedulerServerTargetMachines))
        .SetName(typeof(SchedulerServerTargetMachines).Name);

      yield return new TestCaseData("UberDeployer.SamplePowerShellScriptProjectForDatabaseServer", typeof(DatabaseServerTargetMachine))
        .SetName(typeof(DatabaseServerTargetMachine).Name);

      yield return new TestCaseData("UberDeployer.SamplePowerShellScriptProjectForCustomEnvMachine", typeof(CustomEnvTargetMachine))
        .SetName(typeof(CustomEnvTargetMachine).Name);
    }
  }
}
