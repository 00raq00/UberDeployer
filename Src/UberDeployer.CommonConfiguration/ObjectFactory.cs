using Castle.Windsor;
using UberDeployer.Common.IO;
using UberDeployer.Core;
using UberDeployer.Core.Configuration;
using UberDeployer.Core.DataAccess.WebClient;
using UberDeployer.Core.Deployment;
using UberDeployer.Core.Deployment.Pipeline;
using UberDeployer.Core.Deployment.Pipeline.Modules;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Domain;
using UberDeployer.Core.ExternalDataCollectors;
using UberDeployer.Core.ExternalDataCollectors.DependentProjectsSelection;
using UberDeployer.Core.Management.Db;
using UberDeployer.Core.Management.Db.DbManager;
using UberDeployer.Core.Management.FailoverCluster;
using UberDeployer.Core.Management.Iis;
using UberDeployer.Core.Management.Metadata;
using UberDeployer.Core.Management.MsDeploy;
using UberDeployer.Core.Management.NtServices;
using UberDeployer.Core.Management.ScheduledTasks;
using UberDeployer.Core.TeamCity;

namespace UberDeployer.CommonConfiguration
{
  public class ObjectFactory : IObjectFactory
  {
    private static WindsorContainer _container;
    private static IObjectFactory _instance;

    private ObjectFactory()
    {
      // singleton
    }

    public IApplicationConfiguration CreateApplicationConfiguration()
    {
      return _container.Resolve<IApplicationConfiguration>();
    }

    public IProjectInfoRepository CreateProjectInfoRepository()
    {
      return _container.Resolve<IProjectInfoRepository>();
    }

    public IEnvironmentInfoRepository CreateEnvironmentInfoRepository()
    {
      return _container.Resolve<IEnvironmentInfoRepository>();
    }

    public IArtifactsRepository CreateArtifactsRepository()
    {
      return _container.Resolve<IArtifactsRepository>();
    }

    public IDeploymentRequestRepository CreateDeploymentRequestRepository()
    {
      return _container.Resolve<IDeploymentRequestRepository>();
    }

    public INtServiceManager CreateNtServiceManager()
    {
      return _container.Resolve<INtServiceManager>();
    }

    public IMsDeploy CreateIMsDeploy()
    {
      return _container.Resolve<IMsDeploy>();
    }

    public IIisManager CreateIIisManager()
    {
      return _container.Resolve<IIisManager>();
    }

    public ITaskScheduler CreateTaskScheduler()
    {
      return _container.Resolve<ITaskScheduler>();
    }

    public IDeploymentPipeline CreateDeploymentPipeline()
    {
      return _container.Resolve<IDeploymentPipeline>();
    }

    public IPasswordCollector CreatePasswordCollector()
    {
      IApplicationConfiguration applicationConfiguration = CreateApplicationConfiguration();
      
      return
        new AsynchronousWebPasswordCollector(
          CreateInternalApiWebClient(),
          applicationConfiguration.WebAsynchronousPasswordCollectorMaxWaitTimeInSeconds);
    }

    public IScriptsToRunSelector CreateScriptsToRunWebSelector()
    {
      IApplicationConfiguration applicationConfiguration = CreateApplicationConfiguration();

      return
        new ScriptsToRunSelector(
          CreateInternalApiWebClient(),
          applicationConfiguration.WebAsynchronousPasswordCollectorMaxWaitTimeInSeconds);
    }

    public IScriptsToRunSelector CreateScriptsToRunWebSelectorForEnvironmentDeploy()
    {
      return new ScriptsToRunSelectorForEnvironmentDeploy();
    }

    public IDbScriptRunnerFactory CreateDbScriptRunnerFactory()
    {
      return _container.Resolve<IDbScriptRunnerFactory>();
    }

    public IDbVersionProvider CreateDbVersionProvider()
    {
      return _container.Resolve<IDbVersionProvider>();
    }

    public IFailoverClusterManager CreateFailoverClusterManager()
    {
      return new PowerShellFailoverClusterManager();
    }

    public IDirectoryAdapter CreateDirectoryAdapter()
    {
      return _container.Resolve<IDirectoryAdapter>();
    }

    public IProjectMetadataExplorer CreateProjectMetadataExplorer()
    {
      return new ProjectMetadataExplorer(this, CreateProjectInfoRepository(), CreateEnvironmentInfoRepository(), CreateDbVersionProvider());
    }

    public IDirPathParamsResolver CreateDirPathParamsResolver()
    {
      return _container.Resolve<IDirPathParamsResolver>();
    }

    public IFileAdapter CreateFileAdapter()
    {
      return new FileAdapter();
    }

    public IZipFileAdapter CreateZipFileAdapter()
    {
      return new ZipFileAdapter();
    }

    public IEnvironmentDeployInfoRepository CreateEnvironmentDeployInfoRepository()
    {
      return _container.Resolve<IEnvironmentDeployInfoRepository>();
    }

    public IDbManagerFactory CreateDbManagerFactory()
    {
      return _container.Resolve<IDbManagerFactory>();
    }

    public IMsSqlDatabasePublisher CreateMsSqlDatabasePublisher()
    {
      return _container.Resolve<IMsSqlDatabasePublisher>();
    }

    public IEnvDeploymentPipeline CrateEnvDeploymentPipeline()
    {
      return _container.Resolve<IEnvDeploymentPipeline>();
    }

    public ITeamCityRestClient CreateTeamCityRestClient()
    {
      return _container.Resolve<ITeamCityRestClient>();
    }

    public IInternalApiWebClient CreateInternalApiWebClient()
    {
      IApplicationConfiguration applicationConfiguration = CreateApplicationConfiguration();

      return new InternalApiWebClient(applicationConfiguration.WebAppInternalApiEndpointUrl);
    }

    public IDependentProjectsToDeployWebSelector CreateDependentProjectsToDeployWebSelector()
    {
      IApplicationConfiguration applicationConfiguration = CreateApplicationConfiguration();

      return new DependentProjectsToDeployWebSelector(
        CreateInternalApiWebClient(), 
        applicationConfiguration.WebAsynchronousPasswordCollectorMaxWaitTimeInSeconds);
    }

    public IUserNameNormalizer CreateUserNameNormalizer()
    {
      return _container.Resolve<IUserNameNormalizer>();
    }

    public static IObjectFactory Instance
    {
      get { return (_instance ?? (_instance = new ObjectFactory())); }
    }

    public static WindsorContainer Container
    {
      get { return _container ?? (_container = new WindsorContainer()); }
    }
  }
}
