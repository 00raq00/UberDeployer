using System;
using System.Collections.Generic;
using System.IO;
using Castle.Core;
using Castle.MicroKernel.Registration;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;
using UberDeployer.Common.IO;
using UberDeployer.Core.Configuration;
using UberDeployer.Core.DataAccess.Json;
using UberDeployer.Core.DataAccess;
using UberDeployer.Core.DataAccess.NHibernate;
using UberDeployer.Core.DataAccess.Xml;
using UberDeployer.Core.DataAccess.Xml.EnvironmentInfo;
using UberDeployer.Core.DataAccess.Xml.ProjectInfo;
using UberDeployer.Core.Deployment;
using UberDeployer.Core.Deployment.Pipeline;
using UberDeployer.Core.Deployment.Pipeline.Modules;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Management.Cmd;
using UberDeployer.Core.Management.Db;
using UberDeployer.Core.Management.Db.DbManager;
using UberDeployer.Core.Management.Iis;
using UberDeployer.Core.Management.Metadata;
using UberDeployer.Core.Management.MsDeploy;
using UberDeployer.Core.Management.NtServices;
using UberDeployer.Core.Management.ScheduledTasks;
using UberDeployer.Core.TeamCity;

namespace UberDeployer.CommonConfiguration
{
  public class Bootstraper
  {
    private static readonly string _BaseDirPath = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string _ApplicationConfigPath = Path.Combine(_BaseDirPath, @"Data\ApplicationConfiguration.xml");
    private static readonly string _ProjectInfosFilePath = Path.Combine(_BaseDirPath, @"Data\ProjectInfos.xml");
    private static readonly string _EnvironmentInfosDirPath = Path.Combine(_BaseDirPath, @"Data");
    private static readonly string _EnvironmentDeployInfosDirPath = Path.Combine(_BaseDirPath, @"Data\EnvDeploy");

    private static readonly TimeSpan _NtServiceManagerOperationsTimeout = TimeSpan.FromMinutes(2);

    private static ISessionFactory _sessionFactory;

    private static readonly object _mutex = new object();

    public static void Bootstrap(bool mockTeamCity = false)
    {
      var container = ObjectFactory.Container;

      container.Register(
        Component.For<IApplicationConfigurationRepository>()
          .UsingFactoryMethod(() => new XmlApplicationConfigurationRepository(_ApplicationConfigPath))
          .LifeStyle.Transient,

        Component.For<IApplicationConfiguration>()
          .UsingFactoryMethod((kernel) => kernel.Resolve<IApplicationConfigurationRepository>().LoadConfiguration())
          .LifeStyle.Singleton,

        Component.For<IProjectInfoRepository>()
          .UsingFactoryMethod(() => new XmlProjectInfoRepository(_ProjectInfosFilePath))
          .LifeStyle.Singleton,

        Component.For<IEnvironmentInfoRepository>()
          .UsingFactoryMethod(() => new XmlEnvironmentInfoRepository(_EnvironmentInfosDirPath))
          .LifeStyle.Singleton,
          
        Component.For<IEnvironmentDeployInfoRepository>()
          .UsingFactoryMethod(() => new JsonEnvironmentDeployInfoRepository(_EnvironmentDeployInfosDirPath))
          .LifeStyle.Singleton);

      container.Register(
        Component.For<IDirectoryAdapter>()
          .ImplementedBy<DirectoryAdapter>()
          .LifeStyle.Is(LifestyleType.Transient));

      container.Register(
        Component.For<ITeamCityRestClient>()
          .UsingFactoryMethod(
            () =>
            {
              var appConfig = container.Resolve<IApplicationConfiguration>();

              ITeamCityRestClient restClient;
              if (mockTeamCity)
              {
                restClient = new MockedTeamCityRestClient();
              }
              else
              {
                restClient = new TeamCityRestClient(
                  new Uri(string.Format("http://{0}:{1}", appConfig.TeamCityHostName, appConfig.TeamCityPort)),
                  appConfig.TeamCityUserName,
                  appConfig.TeamCityPassword);
              }

              container.Release(appConfig);

              return restClient;
            })
          .LifeStyle.Transient);

      container.Register(
        Component.For<IArtifactsRepository>().ImplementedBy<TeamCityArtifactsRepository>()
          .LifeStyle.Transient);

      container.Register(
        Component.For<IDeploymentRequestRepository>()
          .UsingFactoryMethod(() => new NHibernateDeploymentRequestRepository(SessionFactory))
          .LifeStyle.Transient);

      container.Register(
        Component.For<INtServiceManager>()
          .UsingFactoryMethod(
            () =>
            {
              var appConfig = container.Resolve<IApplicationConfiguration>();

              return
                new ScExeBasedNtServiceManager(
                  appConfig.ScExePath,
                  _NtServiceManagerOperationsTimeout);
            })
          .LifeStyle.Transient);

      container.Register(
        Component.For<ITaskScheduler>()
         .ImplementedBy<TaskScheduler>()
         .LifeStyle.Transient);

      // TODO IMM HI: config?
      container.Register(
        Component.For<IMsDeploy>()
          .UsingFactoryMethod(() => new MsDeploy(Path.Combine(_BaseDirPath, "msdeploy.exe")))
          .LifeStyle.Transient);

      // TODO IMM HI: config?
      container.Register(
        Component.For<IIisManager>()
          .UsingFactoryMethod(() => new MsDeployBasedIisManager(container.Resolve<IMsDeploy>()))
          .LifeStyle.Transient);

      // TODO IMM HI: config?
      container.Register(
        Component.For<IDeploymentPipeline>()
          .UsingFactoryMethod(
            () =>
            {
              var deploymentRequestRepository = container.Resolve<IDeploymentRequestRepository>();
              var applicationConfiguration = container.Resolve<IApplicationConfiguration>();              
              var auditingModule = new AuditingModule(deploymentRequestRepository);
              var enforceTargetEnvironmentConstraintsModule = new EnforceTargetEnvironmentConstraintsModule();
              var deploymentPipeline = new DeploymentPipeline(applicationConfiguration, ObjectFactory.Instance);

              deploymentPipeline.AddModule(auditingModule);
              deploymentPipeline.AddModule(enforceTargetEnvironmentConstraintsModule);

              return deploymentPipeline;
            })
          .LifeStyle.Transient);

      container.Register(
        Component.For<IEnvDeploymentPipeline>()
          .ImplementedBy<EnvDeploymentPipeline>()
          .LifeStyle.Transient);

      container.Register(
        Component.For<IDbManagerFactory>()
          .ImplementedBy<MsSqlDbManagerFactory>()
          .LifeStyle.Transient);

      container.Register(
        Component.For<IMsSqlDatabasePublisher>()
          .UsingFactoryMethod(
            kernel =>
            {
              var applicationConfiguration = kernel.Resolve<IApplicationConfiguration>();
              var cmdExecutor = kernel.Resolve<ICmdExecutor>();

              return new MsSqlDatabasePublisher(cmdExecutor, applicationConfiguration.SqlPackageDirPath);
            })
          .LifeStyle.Transient);

      container.Register(
        Component.For<ICmdExecutor>()
          .ImplementedBy<CmdExecutor>()
          .LifeStyle.Transient);

      container.Register(
        Component.For<IDbVersionProvider>()
          .UsingFactoryMethod(
            () =>
            {
              // order is important - from more specific to less
              IEnumerable<DbVersionTableInfo> versionTableInfos =
                new List<DbVersionTableInfo>
                    {
                      new DbVersionTableInfo
                        {
                          TableName = "VERSION",
                          VersionColumnName = "dbVersion",
                          MigrationColumnName = "migrated"
                        },
                      new DbVersionTableInfo
                        {
                          TableName = "VERSION",
                          VersionColumnName = "dbVersion"
                        },
                      new DbVersionTableInfo
                        {
                          TableName = "VERSIONHISTORY",
                          VersionColumnName = "DBLabel"
                        }
                    };

              return new DbVersionProvider(versionTableInfos);
            })
          .LifeStyle.Transient);

      container.Register(
        Component.For<IProjectMetadataExplorer>()
          .ImplementedBy<ProjectMetadataExplorer>()
          .LifeStyle.Is(LifestyleType.Transient));

      container.Register(
        Component.For<IDirPathParamsResolver>()
          .UsingFactoryMethod(
            () =>
            {
              var appConfig = container.Resolve<IApplicationConfiguration>();
              return new DirPathParamsResolver(appConfig.ManualDeploymentPackageCurrentDateFormat);
            })
          .LifeStyle.Is(LifestyleType.Transient));

      container.Register(
        Component.For<IDbScriptRunnerFactory>()
          .ImplementedBy<MsSqlDbScriptRunnerFactory>()
          .LifeStyle.Is(LifestyleType.Transient));

      container.Register(
        Component.For<IUserNameNormalizer>()
          .ImplementedBy<UserNameNormalizer>()
          .LifeStyle.Transient);
    }

    private static ISessionFactory CreateNHibernateSessionFactory()
    {
      string connectionString = ObjectFactory.Instance.CreateApplicationConfiguration().ConnectionString;

      FluentConfiguration fluentConfiguration =
        Fluently.Configure()
          .Database(
            MsSqlConfiguration.MsSql2008
              .ConnectionString(connectionString))
          .Mappings(mc => mc.FluentMappings.AddFromAssemblyOf<NHibernateRepository>());

      return fluentConfiguration.BuildSessionFactory();
    }

    private static ISessionFactory SessionFactory
    {
      get
      {
        lock (_mutex)
        {
          return _sessionFactory ?? (_sessionFactory = CreateNHibernateSessionFactory());
        }
      }
    }
  }
}