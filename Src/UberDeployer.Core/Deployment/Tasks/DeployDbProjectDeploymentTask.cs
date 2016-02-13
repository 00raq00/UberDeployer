using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UberDeployer.Common.IO;
using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Deployment.Steps;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Management.Db;
using UberDeployer.Core.Management.Db.DbManager;

namespace UberDeployer.Core.Deployment.Tasks
{
  public class DeployDbProjectDeploymentTask : DeploymentTask
  {
    private readonly IArtifactsRepository _artifactsRepository;
    private readonly IDbScriptRunnerFactory _dbScriptRunnerFactory;
    private readonly IDbVersionProvider _dbVersionProvider;
    private readonly IFileAdapter _fileAdapter;
    private readonly IDirectoryAdapter _directoryAdapter;
    private readonly IZipFileAdapter _zipFileAdapter;
    private readonly IScriptsToRunSelector _createScriptsToRunSelector;
    private readonly IMsSqlDatabasePublisher _databasePublisher;
    private readonly IDbManagerFactory _dbManagerFactory;
    private readonly IUserNameNormalizer _userNameNormalizer;

    private readonly string[] _dbUserRoles = { "db_datareader", "db_datawriter" };    

    public DeployDbProjectDeploymentTask(
      IProjectInfoRepository projectInfoRepository, 
      IEnvironmentInfoRepository environmentInfoRepository, 
      IArtifactsRepository artifactsRepository, 
      IDbScriptRunnerFactory dbScriptRunnerFactory, 
      IDbVersionProvider dbVersionProvider, 
      IFileAdapter fileAdapter, 
      IZipFileAdapter zipFileAdapter, 
      IScriptsToRunSelector createScriptsToRunSelector, 
      IMsSqlDatabasePublisher databasePublisher, 
      IDbManagerFactory dbManagerFactory, 
      IUserNameNormalizer userNameNormalizer,
      IDirectoryAdapter directoryAdapter)
      : base(projectInfoRepository, environmentInfoRepository)
    {
      Guard.NotNull(artifactsRepository, "artifactsRepository");
      Guard.NotNull(dbVersionProvider, "dbVersionProvider");
      Guard.NotNull(dbScriptRunnerFactory, "dbScriptRunnerFactory");
      Guard.NotNull(fileAdapter, "fileAdapter");
      Guard.NotNull(zipFileAdapter, "zipFileAdapter");
      Guard.NotNull(createScriptsToRunSelector, "createScriptsToRunWebSelector");
      Guard.NotNull(databasePublisher, "databasePublisher");
      Guard.NotNull(dbManagerFactory, "dbManagerFactory");
      Guard.NotNull(userNameNormalizer, "userNameNormalizer");
      Guard.NotNull(directoryAdapter, "directoryAdapter");

      _artifactsRepository = artifactsRepository;
      _dbScriptRunnerFactory = dbScriptRunnerFactory;
      _dbVersionProvider = dbVersionProvider;
      _fileAdapter = fileAdapter;
      _zipFileAdapter = zipFileAdapter;
      _createScriptsToRunSelector = createScriptsToRunSelector;
      _databasePublisher = databasePublisher;
      _dbManagerFactory = dbManagerFactory;
      _userNameNormalizer = userNameNormalizer;
      _directoryAdapter = directoryAdapter;
    }

    protected override void DoPrepare()
    {
      EnvironmentInfo environmentInfo = GetEnvironmentInfo();
      DbProjectInfo projectInfo = GetProjectInfo<DbProjectInfo>();

      DbProjectConfiguration dbProjectConfiguration =
        environmentInfo.GetDbProjectConfiguration(projectInfo);

      DatabaseServer databaseServer =
        environmentInfo.GetDatabaseServer(dbProjectConfiguration.DatabaseServerId);

      string databaseServerMachineName = databaseServer.MachineName;

      bool databaseExists = _dbVersionProvider.CheckIfDatabaseExists(
        projectInfo.DbName,
        databaseServerMachineName);

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

      if (databaseExists)
      {
        // create a step for gathering scripts to run
        var gatherDbScriptsToRunDeploymentStep = new GatherDbScriptsToRunDeploymentStep(
          projectInfo.DbName,
          new Lazy<string>(() => extractArtifactsDeploymentStep.BinariesDirPath),
          databaseServerMachineName,
          environmentInfo.Name,
          DeploymentInfo,
          _dbVersionProvider,
          _createScriptsToRunSelector
          );

        AddSubTask(gatherDbScriptsToRunDeploymentStep);

        // create a step for running scripts
        var runDbScriptsDeploymentStep = new RunDbScriptsDeploymentStep(
            GetScriptRunner(projectInfo.IsTransactional, databaseServerMachineName, projectInfo.DbName, environmentInfo.DatabaseServers.FirstOrDefault(e => e.Id == projectInfo.DatabaseServerId)),
            databaseServerMachineName,
            new DeferredEnumerable<DbScriptToRun>(() => gatherDbScriptsToRunDeploymentStep.ScriptsToRun));

        AddSubTask(runDbScriptsDeploymentStep);
      }

      else
      {
        // create step for dropping database
        var createDatabaseDeploymentStep = new CreateDatabaseDeploymentStep(projectInfo, databaseServer, _dbManagerFactory);

        AddSubTask(createDatabaseDeploymentStep);

        // create step for deploying dacpac
        var publishDatabaseDeploymentStep =
          new PublishDatabaseDeploymentStep(
            projectInfo,
            databaseServer,
            GetTempDirPath(),
            _databasePublisher);

        AddSubTask(publishDatabaseDeploymentStep);
      }

      foreach (string userId in projectInfo.Users)
      {
        var environmentUser = environmentInfo.EnvironmentUsers.SingleOrDefault(x => x.Id == userId);

        if (environmentUser == null)
        {
          throw new DeploymentTaskException(string.Format("User [{0}] doesn't exist in enviroment configuration [{1}] in project [{2}]", userId, environmentInfo.Name, projectInfo.Name));
        }

        string user = _userNameNormalizer.ConvertToPreWin2000UserName(environmentUser.UserName, environmentInfo.DomainName);

        IDbManager manager = _dbManagerFactory.CreateDbManager(databaseServerMachineName);

       if (databaseExists && manager.UserExists(projectInfo.DbName, user))
        {
          foreach (string dbUserRole in _dbUserRoles)
          {
            if (!manager.CheckIfUserIsInRole(projectInfo.DbName, user, dbUserRole))
            {
              AddSubTask(new AddRoleToUserStep(manager, projectInfo.DbName, user, dbUserRole));
            }
          }
        }
        else
        {
          AddSubTask(new AddUserToDatabaseStep(manager, projectInfo.DbName, user));
          foreach (string dbUserRole in _dbUserRoles)
          {
            AddSubTask(new AddRoleToUserStep(manager, projectInfo.DbName, user, dbUserRole));
          }
        }
      }
    }

    protected override void Simulate()
    {
      foreach (DeploymentTaskBase subTask in SubTasks)
      {
        subTask.Execute();

        if (subTask is GatherDbScriptsToRunDeploymentStep)
        {
          var gatherDbScriptsToRunDeploymentStep =
            (GatherDbScriptsToRunDeploymentStep)subTask;

          List<string> scriptsToRun =
            gatherDbScriptsToRunDeploymentStep.ScriptsToRun
              .Select(str => Path.GetFileNameWithoutExtension(str.ScriptPath))
              .ToList();

          string diagnosticMessage =
            string.Format(
              "Will run [{0}] script(s): [{1}].",
              scriptsToRun.Count,
              scriptsToRun.Count > 0 ? string.Join(", ", scriptsToRun) : "(none)");

          PostDiagnosticMessage(
            diagnosticMessage,
            DiagnosticMessageType.Info);

          break;
        }
      }
    }

    public override string Description
    {
      get
      {
        return
          string.Format(
            "Deploy db project '{0} ({1}:{2})' on '{3}'.",
            DeploymentInfo.ProjectName,
            DeploymentInfo.ProjectConfigurationName,
            DeploymentInfo.ProjectConfigurationBuildId,
            DeploymentInfo.TargetEnvironmentName);
      }
    }

    private IDbScriptRunner GetScriptRunner(bool isTransactional, string databaseServerMachineName, string dbName, DatabaseServer databaseServers)
    {
      string argumentsSqlCmd = "";
      if (databaseServers != null)
      {
        argumentsSqlCmd = string.Join(" ",
        databaseServers.SqlPackageVariables.Select(
          kv => string.Format(" /v {0}=\"{1}\"", kv.Key, kv.Value)));
      }
      
      IDbScriptRunner scriptRunner =
      _dbScriptRunnerFactory.CreateDbScriptRunner(isTransactional, databaseServerMachineName, dbName, argumentsSqlCmd);

      if (scriptRunner == null)
      {
        throw new DeploymentTaskException(string.Format("Can not create script runner for specified database server machine: '{0}'.", databaseServerMachineName));
      }

      return scriptRunner;
    }
  }
}