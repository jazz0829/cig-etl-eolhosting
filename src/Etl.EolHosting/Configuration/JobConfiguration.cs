using Eol.Cig.Etl.Shared.Extensions;
using System.Configuration;
using System.Data.SqlClient;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public class JobConfiguration : IJobConfiguration
    {
        private readonly SqlConnectionStringBuilder _builder;
        public JobConfiguration(JobElement jobSettings, ConnectionStringSettingsCollection connectionStringSettings)
        {
            Name = jobSettings.GetStringOrThrow("name");

            SourceConnectionStringName = jobSettings["sourceConnectionStringName"];
            SourceConnectionString = connectionStringSettings.GetConnectionStringOrThrow(SourceConnectionStringName);

            DestinationConnectionStringName = jobSettings["destinationConnectionStringName"];
            DestinationConnectionString = connectionStringSettings.GetConnectionStringOrThrow(DestinationConnectionStringName);

            SourceFolder = jobSettings.GetStringOrThrow("sourceFolder");
            StagingFolder = jobSettings.GetStringOrThrow("stagingFolder");
            TempFolder = jobSettings.GetStringOrThrow("tempFolder");
            SummaryTableName = jobSettings.GetStringOrThrow("summaryTableName");
            _builder = new SqlConnectionStringBuilder(DestinationConnectionString);

        }
        public string Name
        {
            get;
        }

        public string SourceFolder
        {
            get;
        }
        public string StagingFolder
        {
            get;
        }
        public string TempFolder
        {
            get;
        }

        public string SummaryTableName
        {
            get;
        }

        public char SqlCsvDelimiter
        {
            get;
        }


        public string ServerName => _builder.DataSource;
        public string DatabaseName => _builder.InitialCatalog;

        public string DestinationTable
        {
            get;
        }

        public string SourceConnectionStringName
        {
            get;
            set;
        }
        public string SourceConnectionString
        {
            get;
        }

        public string DestinationConnectionStringName
        {
            get;
            set;
        }
        public string DestinationConnectionString
        {
            get;

        }
    }
}

