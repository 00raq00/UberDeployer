using UberDeployer.Common.SyntaxSugar;
using UberDeployer.Core.Domain;
using UberDeployer.Core.Management.Db.DbManager;

namespace UberDeployer.Core.Deployment.Steps
{
  public class DropDatabaseDeploymentStep : DeploymentStep
  {
    private readonly DbProjectInfo _projectInfo;
    private readonly DatabaseServer _databaseServer;
    private readonly IDbManagerFactory _dbManagerFactory;

    public DropDatabaseDeploymentStep(DbProjectInfo projectInfo, DatabaseServer databaseServer, IDbManagerFactory dbManagerFactory)
    {
      Guard.NotNull(projectInfo, "projectInfo");
      Guard.NotNull(databaseServer, "databaseServer");
      Guard.NotNull(dbManagerFactory, "dbManager");

      _projectInfo = projectInfo;
      _databaseServer = databaseServer;
      _dbManagerFactory = dbManagerFactory;
    }

    protected override void DoExecute()
    {
      IDbManager dbManager = _dbManagerFactory.CreateDbManager(_databaseServer.MachineName);

      if (dbManager.DatabaseExist(_projectInfo.DbName))
      {
        // database cannot be removed when any snapshot exist
        dbManager.DropAllDatabaseSnapshots(_projectInfo.DbName);

        dbManager.DropDatabase(_projectInfo.DbName);
      }
    }

    public override string Description
    {
      get
      {
        return string.Format(
          "Drop database {0} on server {1}", 
          _projectInfo.DbName, 
          _databaseServer);
      }
    }
  }
}