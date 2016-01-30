using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using UberDeployer.Core.Management.Db;
using UberDeployer.Core.Management.Db.DbManager;

namespace UberDeployer.Tests.Domain.Management.Db.DbManager
{
  [TestFixture]
  public class MsSqlDbManagerTests
  {
    private IDbManager _msSqlDbManager;

    [SetUp]
    public void SetUp()
    {
      _msSqlDbManager = new MsSqlDbManager("localhost");
    }

    [Test]
    [Ignore]
    public void IntegrationTest()
    {
      string dbName = "test" + DateTime.Now.ToString().Replace("-", string.Empty).Replace(":", string.Empty).Replace(" ", string.Empty);

      if (_msSqlDbManager.DatabaseExist(dbName))
      {
        _msSqlDbManager.DropDatabase(dbName);
      }

      _msSqlDbManager.CreateDatabase(new CreateDatabaseOptions(dbName));

      const string username = "NT AUTHORITY\\SYSTEM";
      const string roleName = "db_datareader";
      _msSqlDbManager.AddUser(dbName, username);

      if (_msSqlDbManager.UserExists(dbName, username))
      {
        _msSqlDbManager.AddUserRole(dbName, username, roleName);
      }

      Assert.IsTrue(_msSqlDbManager.DatabaseExist(dbName));
      Assert.IsTrue(_msSqlDbManager.CheckIfUserIsInRole(dbName, username, roleName));

      _msSqlDbManager.DropDatabase(dbName);

      Assert.IsFalse(_msSqlDbManager.DatabaseExist(dbName));
    }
  }
}
