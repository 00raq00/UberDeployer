using System.Collections.Generic;
using System.Linq;

using UberDeployer.Core.TeamCity.ApiModels;

namespace UberDeployer.Core.TeamCity
{
  public class MockedTeamCityRestClient : ITeamCityRestClient
  {
    public IEnumerable<TeamCityProject> GetAllProjects()
    {
      return Enumerable.Empty<TeamCityProject>();
    }

    public TeamCityProject GetProject(string projectName)
    {
      return null;
    }

    public IEnumerable<TeamCityBuildType> GetBuildTypes(string projectName)
    {
      return Enumerable.Empty<TeamCityBuildType>();
    }

    public IEnumerable<TeamCityBuildType> GetBuildTypesWithBranches(string projectName)
    {
      return Enumerable.Empty<TeamCityBuildType>();
    }

    public IEnumerable<TeamCityBuild> GetBuilds(string buildTypeId, string branchName, int start, int count, bool onlySuccessful, bool onlyPinned)
    {
      return Enumerable.Empty<TeamCityBuild>();
    }

    public TeamCityBuild GetBuild(string buildId)
    {
      return null;
    }

    public TeamCityBuild GetLastSuccessfulBuild(string buildTypeId)
    {
      return null;
    }

    public void DownloadArtifacts(string buildId, string destinationFilePath)
    {
    }
  }
}