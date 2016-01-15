using System.Linq;
using NUnit.Framework;
using UberDeployer.Core.DataAccess.Xml;
using UberDeployer.Core.DataAccess.Xml.EnvironmentInfo;
using UberDeployer.Core.Domain;

namespace UberDeployer.Tests.Core.DataAccess.Xml
{
  [TestFixture]
  public class XmlEnvironmentInfoRepositoryTests
  {
    private XmlEnvironmentInfoRepository _sut;
    
    private const string _testEnvName = "Tests";

    [SetUp]
    public void SetUp()
    {
      const string envConfigsDirPath = "Core\\DataAccess\\Xml";
      _sut = new XmlEnvironmentInfoRepository(envConfigsDirPath);
    }

    [Test]
    public void Configuration_loads_successfully()
    {
      // act assert
      Assert.DoesNotThrow(() => _sut.GetAll());
    }

    [Test]
    public void CustomEnvMachines_are_loaded_properly()
    {
      // arrange
      const string expectedMachineId = "SampleTargetMachine";
      const string expectedMachineName = "MachineName";

      // act
      EnvironmentInfo environmentInfo = _sut.FindByName(_testEnvName);
      
      CustomEnvMachine customEnvMachine = environmentInfo.CustomEnvMachines.SingleOrDefault(x => x.Id == expectedMachineId);

      // assert
      Assert.IsNotNull(customEnvMachine);
      Assert.AreEqual(expectedMachineName, customEnvMachine.MachineName);
    }
  }
}
