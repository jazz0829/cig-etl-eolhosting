using Eol.Cig.Etl.Shared.Utils;
using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace Eol.Cig.Etl.EolHosting.Load
{
    public static class EolHostingUploaderHelper
    {
        private static ILog _logger;
        public static List<SqlTable> GetSchema(ILog logger, string sqlConnectionString)
        {

            _logger = logger;

            var sqlTables = new List<SqlTable>();

            var currentColumn = string.Empty;
            var currentTable = string.Empty;
            var currentSchema = string.Empty;
            var currentCatalog = string.Empty;

            try
            {
                DataTable sqlDatabaseStructure;
                using (var sqlConnection = Retry.Do(() => SqlServerUtils.OpenConnection(sqlConnectionString), TimeSpan.FromSeconds(60), 5))
                {
                    sqlDatabaseStructure = sqlConnection.GetSchema("Columns");
                    SqlConnection.ClearPool(sqlConnection);
                }

                var selectedRows = from info in sqlDatabaseStructure.AsEnumerable()
                                   select new
                                   {
                                       TableCatalog = info["TABLE_CATALOG"],
                                       TableSchema = info["TABLE_SCHEMA"],
                                       TableName = info["TABLE_NAME"],
                                       ColumnName = info["COLUMN_NAME"],
                                       DataType = GetExactDataType(info)
                                   };

                foreach (var row in selectedRows)
                {
                    currentColumn = (string)row.ColumnName;
                    currentTable = (string)row.TableName;
                    currentSchema = (string)row.TableSchema;
                    currentCatalog = (string)row.TableCatalog;

                    var tableExist = false;
                    for (var i = 0; i < sqlTables.Count && !tableExist; i++)
                    {
                        var table = sqlTables[i];

                        if (table.Name == currentTable
                            && table.Schema == currentSchema
                            && table.Catalog == currentCatalog)
                        {
                            var dataType = (string)row.DataType;
                            table.Columns.Add((string)row.ColumnName, dataType);
                            tableExist = true;
                        }
                    }
                    if (!tableExist)
                    {
                        var sqlTable = new SqlTable((string)row.TableCatalog, (string)row.TableSchema, (string)row.TableName);
                        sqlTable.Columns.Add((string)row.ColumnName, (string)row.DataType);
                        sqlTables.Add(sqlTable);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Adding column {currentColumn} to table {currentCatalog}.{currentSchema}.{currentTable}");
                logger.Debug(ex.ToString());
                throw;
            }
            return sqlTables;
        }

        private static object GetExactDataType(DataRow row)
        {
            var dataType = row["DATA_TYPE"];

            switch (dataType)
            {
                case "nvarchar":
                case "varchar":
                case "nchar":
                case "char":
                case "ntext":
                case "text":
                case "varbinary":
                case "binary":
                    return $"{dataType}({row["CHARACTER_MAXIMUM_LENGTH"]})";
                default:
                    return dataType;
            }
        }

        public static void RemoveIdentityProperties(string sqlConnection)
        {
            var removeIdentityPropertiesQuery = new StringBuilder();
            removeIdentityPropertiesQuery.Append(" select \'ALTER TABLE [\'+ s.name +\'].[\'+ t.name +\'] ADD [\'+ c.Name + \'2] \'+ ty.name + \' NULL ; \'+ CHAR(13) + CHAR(10) +  ");
            removeIdentityPropertiesQuery.Append(" \'UPDATE [\'+ s.name +\'].[\'+ t.name +\'] SET [\'+ c.Name + \'2] = [\'+ c.Name +\']; \'+ CHAR(13) + CHAR(10) +  ");
            removeIdentityPropertiesQuery.Append(" \'ALTER TABLE [\'+ s.name +\'].[\'+ t.name +\'] DROP COLUMN [\'+ c.Name +\']; \'+ CHAR(13) + CHAR(10) +  + ");
            removeIdentityPropertiesQuery.Append(" \'exec sp_rename \'\'[\'+ s.name +\'].[\'+ t.name +\'].[\'+ c.Name + \'2]\'\', \'\'\'+ c.Name + \'\'\', \'\'COLUMN\'\';" +
                                                 " \'+ CHAR(13) + CHAR(10) +  + ");
            removeIdentityPropertiesQuery.Append(" \'ALTER TABLE [\'+ s.name +\'].[\'+ t.name +\'] ALTER COLUMN [\'+ c.Name +\'] \'+ ty.name + (CASE c.is_nullable WHEN 1 THEN \' " +
                                                 "NULL\' ELSE \' NOT NULL\' END) +\'; \'+ CHAR(13) + CHAR(10)");
            removeIdentityPropertiesQuery.Append("  from sys.columns c");
            removeIdentityPropertiesQuery.Append("  join sys.types ty on c.user_type_id = ty.system_type_id");
            removeIdentityPropertiesQuery.Append("  join sys.tables t");
            removeIdentityPropertiesQuery.Append("  join sys.schemas s on t.schema_id = s.schema_id on c.object_id = t.object_id");
            removeIdentityPropertiesQuery.Append("  where is_identity = 1;");

            try
            {
                using (var reader = SqlServerUtils.ExecuteCommandReturnReader(removeIdentityPropertiesQuery.ToString(), sqlConnection))
                {
                    if (reader != null && reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var createTableScript = (String)reader.GetValue(0);
                            var removedIdentityScripts = createTableScript.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var removedIdentityScriptStep in removedIdentityScripts)
                            {
                                SqlServerUtils.ExecuteCommandReturnNone(removedIdentityScriptStep, sqlConnection);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                throw;
            }
        }

        public static void RemoveTimestamp(SqlTable table, string sqlConnectionString)
        {
            var removeQuerySb = new StringBuilder();
            removeQuerySb.Append($"IF EXISTS(SELECT * FROM sys.columns WHERE Name = N'Timestamp' AND Object_ID = Object_ID(N'{table.Catalog}.{table.Schema}.{table.Name}')) ");
            removeQuerySb.Append("BEGIN ");
            removeQuerySb.Append($"ALTER TABLE {table.Catalog}.{table.Schema}.{table.Name} DROP COLUMN Timestamp ");
            removeQuerySb.Append("END");
            var removeQuery = removeQuerySb.ToString();

            try
            {
                SqlServerUtils.ExecuteCommandReturnNone(removeQuery, sqlConnectionString);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                throw;
            }
        }

        public static void CreateTable(string restoredTable, string destinationTable, string sourceSqlConnection, string destinationSqlConnection)
        {
            var sqlEngineEdition = SqlServerUtils.GetSqlServerEngineEdition(destinationSqlConnection);
            var systemTypesForColumns = "system_type_name + \' \' + ";
            var createTableStatement = $"SET @sql = N'CREATE TABLE {destinationTable} (' + CHAR(13) + CHAR(10) + @cols + ');' ";
            if (sqlEngineEdition == SqlServerUtils.SqlDataWarehouseEngineEdition)
            {
                systemTypesForColumns = "(CASE system_type_name WHEN \'geography\' THEN \'varchar(255)\' ELSE system_type_name END) + \' \' + ";
                createTableStatement = $"SET @sql = N'CREATE TABLE {destinationTable} (' + CHAR(13) + CHAR(10) + @cols + ') WITH (CLUSTERED INDEX (CIGCopyTime));' ";
            }

            var createTable = new StringBuilder();
            createTable.Append(" DECLARE @sql NVARCHAR(MAX), @cols NVARCHAR(MAX) = N\'\'; ");
            createTable.Append(" SELECT @cols += N\',\' + name + \' \' + ");
            createTable.Append(systemTypesForColumns);
            createTable.Append(" (CASE is_nullable WHEN 1 THEN \'NULL \' ELSE  \'NOT NULL \' END) +CHAR(13) + CHAR(10) ");
            createTable.Append($" FROM sys.dm_exec_describe_first_result_set(N'SELECT * FROM {restoredTable}', NULL, 1);");
            createTable.Append(" SET @cols = STUFF(@cols, 1, 1, N\'\'); ");
            createTable.Append(createTableStatement);
            createTable.Append("  SELECT @sql");

            try
            {
                using (var reader = SqlServerUtils.ExecuteCommandReturnReader(createTable.ToString(), sourceSqlConnection))
                {
                    if (reader != null && reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            var createTableScript = (String)reader.GetValue(0);
                            SqlServerUtils.ExecuteCommandReturnNone(createTableScript, SqlServerUtils.OpenConnection(destinationSqlConnection), null);
                        }
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                throw;
            }
        }
    }
}
