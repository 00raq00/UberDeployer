using System.Collections.Generic;

using NHibernate.Mapping;

namespace UberDeployer.Core.Management.Db.DbManager
{
  public interface IDbManager
  {
    void DropDatabase(string databaseName);

    void CreateDatabase(CreateDatabaseOptions databaseOptions);

    bool DatabaseExist(string databaseName);

    bool UserExists(string databaseName, string username);

    void AddUser(string databaseName, string username);

    void AddUserRole(string databaseName, string username, string roleName);

    bool CheckIfUserIsInRole(string databaseName, string username, string roleName);
    
    void DropAllDatabaseSnapshots(string databaseName);
  }
}