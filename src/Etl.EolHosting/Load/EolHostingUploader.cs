using Eol.Cig.Etl.EolHosting.Configuration;
using Eol.Cig.Etl.EolHosting.Domain;
using Eol.Cig.Etl.Shared.Load;
using Eol.Cig.Etl.Shared.Utils;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using Amazon.Kinesis;
using Eol.Cig.Etl.Kinesis.Producer;

namespace Eol.Cig.Etl.EolHosting.Load
{
    public class EolHostingUploader : SqlServerUploader, IEolHostingUploader
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private const string IncorrectFileFolder = @"\IncorrectFiles";
        private const string ExecutedFileFolder = @"\ExecutedFiles";
        private const string RawSchema = "raw";
        private const string RawTablePrefix = "HOST_";
        public static readonly string CigCopyTimeColumnName = "CIGCopyTime";
        public static readonly string CigProcessedColumnName = "CIGProcessed";
        public static readonly int CigProcessedDefaultVale = 0;
        public static readonly string EnvironmentColumnName = "Environment";
        public static readonly string RownNumberColumnName = "RowNumber";
        public static readonly int BatchSize = 5000;

        private static readonly SchemaChanges newSchema = new SchemaChanges();

        private readonly IAmazonKinesis _kinesis;
        protected readonly IAwsConfiguration _awsConfiguration;

        private readonly List<Table> _config;

        private readonly ILog _logger;
        private readonly IJobConfiguration _configuration;

        public EolHostingUploader(ILog logger, IJobConfiguration configuration, IAwsConfiguration awsConfiguration) : base(logger, configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _awsConfiguration = awsConfiguration ?? throw new ArgumentNullException(nameof(awsConfiguration));
            _kinesis = new AmazonKinesisClient(awsConfiguration.AwsAccessKeyId, awsConfiguration.AwsSecretAccessKey, Amazon.RegionEndpoint.EUWest1);
            if (File.Exists("config.json"))
            {
                _config = JsonConvert.DeserializeObject<List<Table>>(File.ReadAllText(@"config.json"));
            }
            else
            {
                _logger.Error("Cannot find the configuration file in the EOLHosting root directory.");
                throw new FileNotFoundException("Config file not found. Aborting importing EOL data.");
            }
        }

        public void HandleDelta(CigFile cigFile, bool overwriteAppendedBackup)
        {
            var lastSummaryUpdate = GetLastSummaryUpdate(cigFile);

            if (overwriteAppendedBackup || lastSummaryUpdate["BackupDateTime"] == null || cigFile.DateTime > lastSummaryUpdate["BackupDateTime"])
            {
                cigFile.RestoredDatabaseName = cigFile.FileNameNoExtension + "_Restored_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                try
                {
                    var sourceConnectionString = RestoreDatabase(cigFile);

                    AlterTables(cigFile, sourceConnectionString);
                    SyncDatabaseDataModel(cigFile, sourceConnectionString, _configuration.DestinationConnectionString);
                    AddRowNumberColumn(sourceConnectionString);
                    BulkImportDelta(sourceConnectionString, _configuration.DestinationConnectionString);

                    var backupDateTime = cigFile.DateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                    var restoreDateTime = DateTime.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                    UpdateSummaryTable(cigFile.Environment, backupDateTime, restoreDateTime);

                    DropDatabase(sourceConnectionString);

                    FileUtils.MoveFile(_configuration.StagingFolder, cigFile.FileName, _configuration.StagingFolder + ExecutedFileFolder);
                    _logger.Info($"The file {cigFile.FileName} has been processed");

                    try
                    {
                        if (newSchema.Changes.Count > 0)
                        {
                            Utility.Notify(newSchema);
                            newSchema.Changes.Clear(); 
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error occured while sending an alert email to the Customer Intelligence team about a DB schema changes in the backup file:\r\n {ex.ToString()}");
                    }
                }
                catch (RestoreDbSqlException ex)
                {
                    _logger.Error($"The database {cigFile.FileName} has not been restored", ex);
                }
                catch (Exception ex)
                {
                    DeleteDelta(cigFile);
                    UpdateSummaryTable(cigFile.Environment, lastSummaryUpdate["BackupDateTime"]?.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
                        lastSummaryUpdate["RestoredDateTime"]?.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                    FileUtils.MoveFile(_configuration.StagingFolder, cigFile.FileName, _configuration.StagingFolder + IncorrectFileFolder);
                    _logger.Error($"The file {cigFile.FileName} has not been processed", ex);
                }
            }
            else
            {
                _logger.Info($"The file {cigFile.FileName} has been processed in the past");
            }
        }

        private void AddRowNumberColumn(string sqlConnectionString)
        {
            var tables = GetSchema(sqlConnectionString);
            foreach (var table in tables)
            {
                var addRowColumn = $"ALTER TABLE[{ table.Catalog}].[{table.Schema}].[{table.Name}] Add [{RownNumberColumnName}] BIGINT IDENTITY(1,1) ";
                try
                {
                    SqlServerUtils.ExecuteCommandReturnNone(addRowColumn, sqlConnectionString, true);

                }
                catch (Exception ex)
                {
                    _logger.Error($"Error occured when altering table {table.Catalog}.{table.Schema}.{table.Name}", ex);
                    throw;
                }
                _logger.InfoFormat($"Added RowNumber Column to table {table.Catalog}.{table.Schema}.{table.Name}");
            }
        }

        private List<SqlTable> GetSchema(string sqlConnectionString)
        {
            try
            {
                var schema = EolHostingUploaderHelper.GetSchema(_logger, sqlConnectionString);
                _logger.InfoFormat($"Retrieved schema information from database  {sqlConnectionString.GetServerName()}:{sqlConnectionString.GetDatbaseName()}");
                return schema;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured when retrieving schema information from database {sqlConnectionString.GetDatbaseName()} on server {sqlConnectionString.GetServerName()}", ex);
                throw;
            }
        }

        private void SyncDatabaseDataModel(CigFile cigFile, string sourceConnectionString, string destinationConnectionString)
        {
            var tables = GetSchema(sourceConnectionString);

            foreach (var sqlTable in tables)
            {
                var restoredTable = cigFile.RestoredDatabaseName + "." + sqlTable.Schema + "." + sqlTable.Name;
                var destinationTable = RawSchema + "." + RawTablePrefix + sqlTable.Name;
                try
                {
                    if (!TableExist(sqlTable, destinationConnectionString))
                    {
                        EolHostingUploaderHelper.CreateTable(restoredTable, destinationTable, sourceConnectionString, destinationConnectionString);
                        _logger.InfoFormat($"Created table {destinationTable} in {destinationConnectionString.GetServerName()}:{destinationConnectionString.GetDatbaseName()} " +
                                           $"based on the data model of table {restoredTable} in  {sourceConnectionString.GetServerName()}:{sourceConnectionString.GetDatbaseName()}");
                    }
                    else
                    {
                        _logger.InfoFormat($"Table {destinationTable} already exists");
                        var missedColumns = new List<KeyValuePair<string, string>>();

                        //Check if all columns from the backup file exists already, if not create the missing ones

                        var table = _config.SingleOrDefault(t => t.Name.ToLower() == sqlTable.Name.ToLower());

                        if (table != null && table.AutoAddColumns == true)
                        {
                            foreach (var col in sqlTable.Columns)
                            {
                                if (!ColumnExist(sqlTable, col.Key, destinationConnectionString))
                                {
                                    _logger.WarnFormat($"Column {col.Key} doesn't exists in {destinationTable}. Adding it now ...");

                                    AddMissingColumn(sqlTable, col.Key, col.Value, destinationConnectionString);

                                    missedColumns.Add(new KeyValuePair<string, string>(col.Key, col.Value));
                                }
                                else
                                {
                                    _logger.InfoFormat($"Column {col.Key} already exists in {destinationTable}");
                                }
                            }

                            if (missedColumns.Count > 0)
                            {
                                newSchema.Changes.Add(destinationTable, missedColumns);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error during the creation of the table {destinationTable} in {destinationConnectionString.GetServerName()}:{destinationConnectionString.GetDatbaseName()}" +
                                  $" based on the data model of table {restoredTable} in  {sourceConnectionString.GetServerName()}:{sourceConnectionString.GetDatbaseName()} ", ex);
                    throw;
                }
            }
            _logger.InfoFormat($"Data model synchronization between {sourceConnectionString.GetServerName()}:{sourceConnectionString.GetDatbaseName()} " +
                               $"and {destinationConnectionString.GetServerName()}:{destinationConnectionString.GetDatbaseName()} successfully completed");
        }

        private void AddMissingColumn(SqlTable sqlTable, string columnName, string dataType, string connectionString)
        {
            var query = $"Alter table [{RawSchema}].[{RawTablePrefix}{sqlTable.Name}] ADD [{columnName}] {dataType} NULL;";
            SqlServerUtils.ExecuteCommandReturnNone(query, connectionString);
        }

        private void BulkImportDelta(string sourceSqlConnectionString, string destinationSqlConnectionString)
        {
            var tables = GetSchema(sourceSqlConnectionString);

            foreach (var sqlTable in tables)
            {
                int rowCount;
                var targetTableConfig = _config.SingleOrDefault(c => c.Name == sqlTable.Name);
                try
                {
                    rowCount = GetTableRowCount(sqlTable, sourceSqlConnectionString);
                    for (var i = 0; i < rowCount; i += BatchSize)
                    {
                        var table = new DataTable();
                        using (var sqlConn = new SqlConnection(sourceSqlConnectionString))
                        {
                            var sqlQuery = $"SELECT * FROM {sqlTable.Name} WHERE {RownNumberColumnName} BETWEEN {i + 1} and {i + BatchSize}";
                            using (var cmd = new SqlCommand(sqlQuery, sqlConn))
                            {
                                var ds = new SqlDataAdapter(cmd);
                                ds.Fill(table);

                                if (SqlServerUtils.GetSqlServerEngineEdition(destinationSqlConnectionString) == SqlServerUtils.SqlDataWarehouseEngineEdition)
                                {
                                    SqlServerUtils.ConvertTypesNotSupportedByAzureDw(table);
                                }
                            }
                        }
                        table.Columns.Remove(RownNumberColumnName);
                        RemoveExtraColumns(table, sqlTable.Name);
                        UpdateSqlTableFromDataTable(table, true, destinationSqlConnectionString, $"{"raw." + RawTablePrefix + sqlTable.Name}");

                        if (_awsConfiguration.IsStreamingEnabled && 
                            targetTableConfig?.StreamingEnabled != null && 
                            targetTableConfig.StreamingEnabled.Value)
                        {
                            var kinesisProducer = new KinesisWriter(_logger, _kinesis, _awsConfiguration.AwsKinesisStreamName,_awsConfiguration.S3Prefix, RawTablePrefix + sqlTable.Name);

                            kinesisProducer.IngestData(table);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat($"Error occured when importing data into {sqlTable.Name}", ex);
                    throw;
                }
                _logger.InfoFormat($"Imported SQL table {sqlTable.Name} from {sourceSqlConnectionString.GetServerName()}:{sourceSqlConnectionString.GetDatbaseName()} " +
                                   $"to {destinationSqlConnectionString.GetServerName()}:{destinationSqlConnectionString.GetDatbaseName()}. Number of records: {rowCount}");
            }
        }

        private void RemoveExtraColumns(DataTable table, string tableName)
        {

            var targetTable = _config.SingleOrDefault(c => c.Name == tableName);

            var list = new List<string>();

            if (targetTable != null)
            {
                if (targetTable.AutoAddColumns == null || targetTable.AutoAddColumns.Value == false)
                {
                    foreach (DataColumn item in table.Columns)
                    {
                        if (!targetTable.Columns.Contains(item.ColumnName))
                        {
                            list.Add(item.ColumnName);
                            _logger.Warn($"{item.ColumnName} is an extra column and will be ignored !!!");
                        }
                    } 
                }
            }
            else
            {
                _logger.Warn($"The table {tableName} wasn't found in the configuration file but it's going to be imported !");
            }

            list.ForEach(c => table.Columns.Remove(c));
        }

        private int GetTableRowCount(SqlTable sqlTable, string sqlConnectionName)
        {
            int count;
            var rowCount = $"SELECT COUNT (*) FROM {sqlTable.Name}";

            try
            {
                count = SqlServerUtils.ExecuteCommandReturnInt(rowCount, sqlConnectionName);
            }
            catch (Exception ex)
            {
                _logger.Error("Error occured during counting row number of table" + sqlTable.Name, ex);
                throw;
            }
            return count;
        }

        private void DeleteAppendDelta(CigFile cigFile)
        {
            var tables = GetSchema(_configuration.DestinationConnectionString);

            foreach (var sqlTable in tables)
            {
                if (sqlTable.Schema == RawSchema && sqlTable.Name.StartsWith(RawTablePrefix))
                {
                    DeleteRecords(_configuration.DatabaseName, sqlTable, cigFile.Environment, cigFile.DateTime);
                }
            }
        }

        private void DeleteRecords(string databaseName, SqlTable table, string environment, DateTime dateTime)
        {
            var deleteQuery = new StringBuilder();
            deleteQuery.Append($"DELETE FROM {databaseName}.{RawSchema}.{table.Name} WHERE {EnvironmentColumnName}='{environment}' AND {CigCopyTimeColumnName} = " +
                               $"'{dateTime.ToString(DateTimeFormat)}' AND {CigProcessedColumnName}={CigProcessedDefaultVale}");

            try
            {
                SqlServerUtils.ExecuteCommandReturnNone(deleteQuery.ToString(), _configuration.DestinationConnectionString);
            }
            catch (Exception ex)
            {
                _logger.Error("Error occured during deletion " + table.Name + " in database " + databaseName, ex);
                throw;
            }
            _logger.InfoFormat($"Deleted records from table {databaseName}.{RawSchema}.{table.Name}");
        }

        private void AlterTables(CigFile cigFile, string sqlConnectionString)
        {
            RemoveIdentityProperties(sqlConnectionString);

            var copyTime = cigFile.DateTime.ToString(DateTimeFormat);
            var environment = cigFile.Environment;

            var tables = GetSchema(sqlConnectionString);
            foreach (var sqlTable in tables)
            {
                RemoveTimestamp(sqlTable, sqlConnectionString);
                AddColumns(sqlTable, copyTime, environment, sqlConnectionString);
                _logger.InfoFormat($"Altered table {sqlTable.Name} on server: {sqlConnectionString.GetServerName()} in database: {sqlConnectionString.GetDatbaseName()}");
            }
        }

        private void RemoveIdentityProperties(string sqlConnectionString)
        {
            try
            {
                EolHostingUploaderHelper.RemoveIdentityProperties(sqlConnectionString);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during the removal of identity properties. On server: {sqlConnectionString.GetServerName()} in database: {sqlConnectionString.GetDatbaseName()}", ex);
                throw;
            }
        }

        private void RemoveTimestamp(SqlTable table, string sqlConnectionString)
        {
            try
            {
                EolHostingUploaderHelper.RemoveTimestamp(table, sqlConnectionString);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured when removing Timestamp column from table {table.Catalog}.{table.Schema}.{table.Name}", ex);
                throw;
            }
        }

        private void AddColumns(SqlTable table, string copytime, string environment, string sqlConnectionString)
        {
            var alterQuery = new StringBuilder();
            alterQuery.Append($"ALTER TABLE [{table.Catalog}].[{table.Schema}].[{table.Name}] Add {CigCopyTimeColumnName} datetime ");
            alterQuery.Append($"ALTER TABLE[{ table.Catalog}].[{table.Schema}].[{table.Name}] Add [{EnvironmentColumnName}] nchar(3) ");
            alterQuery.Append($"ALTER TABLE[{ table.Catalog}].[{table.Schema}].[{table.Name}] Add [{CigProcessedColumnName}] BIT ");

            var updateQuery = new StringBuilder();
            updateQuery.Append($" UPDATE [{table.Catalog}].[{table.Schema}].[{table.Name}] SET [{CigCopyTimeColumnName}] = '{copytime}'");
            updateQuery.Append($" UPDATE [{table.Catalog}].[{table.Schema}].[{table.Name}] SET [{EnvironmentColumnName}] = '{environment}'");
            updateQuery.Append($" UPDATE [{table.Catalog}].[{table.Schema}].[{table.Name}] SET [{CigProcessedColumnName}] = {CigProcessedDefaultVale}");

            try
            {
                SqlServerUtils.ExecuteCommandReturnNone(alterQuery.ToString(), sqlConnectionString, true);
                SqlServerUtils.ExecuteCommandReturnNone(updateQuery.ToString(), sqlConnectionString, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured durring the addition of the following columns {CigCopyTimeColumnName}, {EnvironmentColumnName}, {CigProcessedColumnName} in " +
                              $"{table.Catalog}.{table.Schema}.{table.Name}", ex);
                throw;
            }
        }

        private string RestoreDatabase(CigFile cigFile)
        {
            try
            {
                _logger.InfoFormat($"Restoring database: {cigFile.RestoredDatabaseName}");
                SqlServerUtils.RestoreSqlDatabase(cigFile.RestoredDatabaseName, cigFile.FullPath, _configuration.SourceConnectionString, _configuration.TempFolder);
            }
            catch (SqlException ex) when (ex.Message.Contains("is incorrectly formed and can not be read."))
            {
                _logger.Error($"Backup file is corrupted: {cigFile.FileNameNoExtension}", ex);
                throw new RestoreDbSqlException();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured when restoring database {cigFile.FileNameNoExtension}", ex);
                throw;
            }
            _logger.InfoFormat("Database {0} is restored", cigFile.FileNameNoExtension);

            return SqlServerUtils.ChangeInitialCatalogConnectionString(cigFile.RestoredDatabaseName, _configuration.SourceConnectionString);
        }


        private bool TableExist(SqlTable sqlTable, string connectionString)
        {
            var checkExistsQuery = $"SELECT CASE WHEN EXISTS((select * from information_schema.tables where TABLE_SCHEMA = '{RawSchema}' and" +
                                   $" table_name = '{RawTablePrefix}{sqlTable.Name}')) then 1 else 0 end";
            var exist = SqlServerUtils.ExecuteCommandReturnInt(checkExistsQuery, connectionString);
            return exist > 0;
        }

        private bool ColumnExist(SqlTable sqlTable, string columnName, string connectionString)
        {
            var checkExistsQuery = $"SELECT CASE WHEN EXISTS((select * from information_schema.COLUMNS where TABLE_SCHEMA = '{RawSchema}' and" +
                                   $" column_name='{columnName}' and table_name = '{RawTablePrefix}{sqlTable.Name}')) then 1 else 0 end";
            var exist = SqlServerUtils.ExecuteCommandReturnInt(checkExistsQuery, connectionString);
            return exist > 0;
        }

        private void UpdateSummaryTable(string environment, string backupDateTime, string restoreDateTime)
        {
            try
            {
                var countQuery = $"SELECT COUNT(*) FROM {_configuration.SummaryTableName} WHERE {EnvironmentColumnName} = '{environment}' AND BackupDateTime='{backupDateTime}'";
                var numRecords = SqlServerUtils.ExecuteCommandReturnInt(countQuery, _configuration.DestinationConnectionString);
                if (numRecords > 0)
                {
                    var updateQuery = $"UPDATE {_configuration.SummaryTableName} SET RestoredDateTime='{restoreDateTime}' where {EnvironmentColumnName} = '{environment}' AND " +
                                      $"BackupDateTime='{backupDateTime}'";
                    SqlServerUtils.ExecuteCommandReturnNone(updateQuery, _configuration.DestinationConnectionString);
                }
                else
                {
                    var insertQuery = $"INSERT INTO {_configuration.SummaryTableName} VALUES ('{environment}','{backupDateTime}', '{restoreDateTime}')";
                    SqlServerUtils.ExecuteCommandReturnNone(insertQuery, _configuration.DestinationConnectionString);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error occured when updating table " + _configuration.SummaryTableName, ex);
                throw;
            }
            _logger.InfoFormat("Table {0} updated", _configuration.SummaryTableName);
        }

        private void DeleteRecordSummaryTable(string environment, string backupDateTime)
        {
            try
            {
                var deletionQuery = $"DELETE FROM {_configuration.SummaryTableName} WHERE {EnvironmentColumnName} = '{environment}' AND BackupDateTime='{backupDateTime}'";
                SqlServerUtils.ExecuteCommandReturnNone(deletionQuery, _configuration.DestinationConnectionString);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured while deleting the record {environment},{backupDateTime} from the  table {_configuration.SummaryTableName}", ex);
                throw;
            }
            _logger.Warn($"Table {_configuration.SummaryTableName} updated");
        }


        private void DropDatabase(string sourceConnectionString)
        {
            try
            {
                SqlServerUtils.DropSqlDatabase(sourceConnectionString);
                _logger.InfoFormat($"Dropped database {sourceConnectionString.GetDatbaseName()} on server {sourceConnectionString.GetServerName()} (if it existed).");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error occured when dropping the database {sourceConnectionString.GetDatbaseName()} on server {sourceConnectionString.GetServerName()}", ex);
                throw;
            }
        }

        private Dictionary<string, DateTime?> GetLastSummaryUpdate(CigFile cigFile)
        {
            var lastSummaryTableUpdate = new Dictionary<string, DateTime?>
            {
                { "BackupDateTime", null },
                { "RestoredDateTime", null }
            };
            try
            {
                var query = $"SELECT TOP 1 * FROM {_configuration.SummaryTableName} where {EnvironmentColumnName}='{cigFile.Environment}'ORDER BY BackupDateTime DESC";
                using (var reader = SqlServerUtils.ExecuteCommandReturnReader(query, _configuration.DestinationConnectionString))
                {
                    while (reader.Read())
                    {
                        lastSummaryTableUpdate["BackupDateTime"] = (DateTime)reader.GetValue(1);
                        lastSummaryTableUpdate["RestoredDateTime"] = (DateTime)reader.GetValue(2);
                    }

                }
                return lastSummaryTableUpdate;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error when reading from table {_configuration.SummaryTableName}", ex);
                throw;
            }
        }

        public void DeleteDelta(CigFile cigFileSingleDelta)
        {
            DeleteAppendDelta(cigFileSingleDelta);

            var backupDateTime = cigFileSingleDelta.DateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            DeleteRecordSummaryTable(cigFileSingleDelta.Environment, backupDateTime);
        }
    }
}
