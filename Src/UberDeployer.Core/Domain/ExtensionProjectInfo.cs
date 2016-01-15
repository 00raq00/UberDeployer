using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using NHibernate.Hql.Ast.ANTLR.Tree;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Deployment.Tasks;
using UberDeployer.Core.Domain.Input;
using UberDeployer.Core.Management.FailoverCluster;

namespace UberDeployer.Core.Domain
{
  public class ExtensionProjectInfo : ProjectInfo
  {
    public string ExtendedProjectName { get; set; }

    public ExtensionProjectInfo(
      string name, 
      string artifactsRepositoryName, 
      IEnumerable<string> allowedEnvironmentNames, 
      string artifactsRepositoryDirName, 
      bool artifactsAreNotEnvironmentSpecific,
      string extendedProjectName,
      List<string> dependendProjectNames = null)
      : base(name, artifactsRepositoryName, allowedEnvironmentNames, dependendProjectNames, artifactsRepositoryDirName, artifactsAreNotEnvironmentSpecific)
    {
      Guard.NotNullNorEmpty(extendedProjectName, "extendedProjectName");

      ExtendedProjectName = extendedProjectName;
    }

    #region Overrides of ProjectInfo

    public override ProjectType Type
    {
      get { return ProjectType.Extension; }
    }

    public override InputParams CreateEmptyInputParams()
    {
      return new ExtensionInputParams();
    }

    public override DeploymentTask CreateDeploymentTask(IObjectFactory objectFactory)
    {
      if (objectFactory == null)
      {
        throw new ArgumentNullException("objectFactory");
      }

      return
        new DeployExtensionProjectDeploymentTask(
          objectFactory.CreateProjectInfoRepository(),
          objectFactory.CreateEnvironmentInfoRepository(),
          objectFactory.CreateArtifactsRepository(),
          objectFactory.CreateDirectoryAdapter(),
          objectFactory.CreateFileAdapter(),
          objectFactory.CreateZipFileAdapter(),
          objectFactory.CreateFailoverClusterManager(),
          objectFactory.CreateNtServiceManager());
    }

    public override IEnumerable<string> GetTargetFolders(IObjectFactory objectFactory, EnvironmentInfo environmentInfo)
    {
      Guard.NotNull(objectFactory, "objectFactory");
      Guard.NotNull(environmentInfo, "environmentInfo");

      IProjectInfoRepository projectInfoRepository = objectFactory.CreateProjectInfoRepository();
      
      var extendedNtServiceProject = projectInfoRepository.FindByName(ExtendedProjectName) as NtServiceProjectInfo;
      if (extendedNtServiceProject == null)
      {
        throw new ConfigurationErrorsException(string.Format("Extended NT service project: [{0}] not found", ExtendedProjectName));
      }

      string targetDirPath = Path.Combine(environmentInfo.NtServicesBaseDirPath, extendedNtServiceProject.NtServiceDirName, extendedNtServiceProject.ExtensionsDirName);

      if (environmentInfo.EnableFailoverClusteringForNtServices)
      {
        IFailoverClusterManager failoverClusterManager = objectFactory.CreateFailoverClusterManager();

        string clusterGroupName = environmentInfo.GetFailoverClusterGroupNameForProject(ExtendedProjectName);

        return failoverClusterManager.GetPossibleNodeNames(environmentInfo.FailoverClusterMachineName, clusterGroupName)
          .Select(nodeName => EnvironmentInfo.GetNetworkPath(nodeName, targetDirPath));
      }
      
      return new[] { environmentInfo.GetAppServerNetworkPath(targetDirPath) };
    }

    public override string GetMainAssemblyFileName()
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}
