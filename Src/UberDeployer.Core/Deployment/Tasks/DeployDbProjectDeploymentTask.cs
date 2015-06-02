﻿using System;
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
    private readonly IZipFileAdapter _zipFileAdapter;
    private readonly IScriptsToRunWebSelector _createScriptsToRunWebSelector;
    private readonly IMsSqlDatabasePublisher _databasePublisher;
    private readonly IDbManagerFactory _dbManagerFactory;

    public DeployDbProjectDeploymentTask(IProjectInfoRepository projectInfoRepository, IEnvironmentInfoRepository environmentInfoRepository, IArtifactsRepository artifactsRepository, IDbScriptRunnerFactory dbScriptRunnerFactory, IDbVersionProvider dbVersionProvider, IFileAdapter fileAdapter, IZipFileAdapter zipFileAdapter, IScriptsToRunWebSelector createScriptsToRunWebSelector, IMsSqlDatabasePublisher databasePublisher, IDbManagerFactory dbManagerFactory)
      : base(projectInfoRepository, environmentInfoRepository)
    {
      Guard.NotNull(artifactsRepository, "artifactsRepository");
      Guard.NotNull(dbVersionProvider, "dbVersionProvider");
      Guard.NotNull(dbScriptRunnerFactory, "dbScriptRunnerFactory");
      Guard.NotNull(fileAdapter, "fileAdapter");
      Guard.NotNull(zipFileAdapter, "zipFileAdapter");
      Guard.NotNull(createScriptsToRunWebSelector, "createScriptsToRunWebSelector");
      Guard.NotNull(databasePublisher, "databasePublisher");
      Guard.NotNull(dbManagerFactory, "dbManagerFactory");

      _artifactsRepository = artifactsRepository;
      _dbScriptRunnerFactory = dbScriptRunnerFactory;
      _dbVersionProvider = dbVersionProvider;
      _fileAdapter = fileAdapter;
      _zipFileAdapter = zipFileAdapter;
      _createScriptsToRunWebSelector = createScriptsToRunWebSelector;
      _databasePublisher = databasePublisher;
      _dbManagerFactory = dbManagerFactory;
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
          _createScriptsToRunWebSelector
          );

        AddSubTask(gatherDbScriptsToRunDeploymentStep);

        // create a step for running scripts
        var runDbScriptsDeploymentStep =
          new RunDbScriptsDeploymentStep(
            GetScriptRunner(projectInfo.IsTransactional, databaseServerMachineName, projectInfo.DbName),
            databaseServerMachineName,
            new DeferredEnumerable<DbScriptToRun>(() => gatherDbScriptsToRunDeploymentStep.ScriptsToRun));

        AddSubTask(runDbScriptsDeploymentStep);
      }

      else
      {
        // create step for deploying dacpac
        var publishDatabaseDeploymentStep =
          new PublishDatabaseDeploymentStep(
            projectInfo,
            databaseServer,
            GetTempDirPath(),
            _databasePublisher);

        AddSubTask(publishDatabaseDeploymentStep);
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

    private IDbScriptRunner GetScriptRunner(bool isTransactional, string databaseServerMachineName, string dbName)
    {
      IDbScriptRunner scriptRunner =
        _dbScriptRunnerFactory.CreateDbScriptRunner(isTransactional, databaseServerMachineName, dbName);

      if (scriptRunner == null)
      {
        throw new DeploymentTaskException(string.Format("Can not create script runner for specified database server machine: '{0}'.", databaseServerMachineName));
      }

      return scriptRunner;
    }
  }
}