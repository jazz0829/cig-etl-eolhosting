using Xunit;
using Eol.Cig.Etl.EolHosting.Configuration;
using System.Configuration;

namespace Eol.CIg.Etl.EOLHosting.Test
{
    public class JobConfigurationTest
    {
        JobElement _jobSettings;
        ConnectionStringSettingsCollection _connectionStringSettings;

        public JobConfigurationTest()
        {
            _jobSettings = new JobElement();
            _jobSettings.Add("name", "jobName");
            _jobSettings.Add("sourceFolder", "sourceFolderValue");
            _jobSettings.Add("stagingFolder", "\\Server\\StagingFolder");
            _jobSettings.Add("tempFolder", "tempFolderValue");
            _jobSettings.Add("summaryTableName", "summaryTableNameValue");
            _jobSettings.Add("sourceConnectionStringName", "Source");
            _jobSettings.Add("destinationConnectionStringName", "Destination");


            _connectionStringSettings = new ConnectionStringSettingsCollection
            {
                new ConnectionStringSettings("Source", "Data Source=server;Initial Catalog=db"),
                new ConnectionStringSettings("Destination", "Data Source=server;Initial Catalog=db")
            };
        }

        [Fact]
        public void ShouldCreateConfigurationInstance()
        {
            //Arrange
            var conf = new JobConfiguration(_jobSettings, _connectionStringSettings);

            //Act

            //Assert
            Assert.IsType<JobConfiguration>(conf);
        }

        [Fact]
        public void ShouldThrowAnErrorInvalidSourceFolder()
        {
            //Arrange
            _jobSettings = new JobElement();
            _jobSettings.Add("sourceFolder", "");

            Assert.Throws<System.NullReferenceException>(() => _jobSettings.GetStringOrThrow(_jobSettings.SourceFolder));
        }


        [Fact]
        public void ShouldThrowAnErrorInvalidStagingFolder()
        {
            //Arrange
            _jobSettings = new JobElement();
            _jobSettings.Add("tempFolder", "");

            Assert.Throws<System.NullReferenceException>(() => _jobSettings.GetStringOrThrow(_jobSettings.TempFolder));
        }

        [Fact]
        public void ShouldThrowAnErrorInvalidSummaryTableName()
        {
            //Arrange
            _jobSettings = new JobElement();
            _jobSettings.Add("summaryTableName", "");

            Assert.Throws<System.NullReferenceException>(() => _jobSettings.GetStringOrThrow(_jobSettings.SummaryTableName));
        }

    }
}
