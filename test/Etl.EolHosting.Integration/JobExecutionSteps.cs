using System;
using Eol.Cig.Etl.EolHosting.Configuration;
using Eol.Cig.Etl.EolHosting.Extract;
using Eol.Cig.Etl.EolHosting.Load;
using Eol.Cig.Etl.EolHosting.Service;
using Eol.Cig.Etl.Shared.Integration;
using Eol.Cig.Etl.Shared.Service;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Quartz;
using StructureMap;
using System.IO;
using TechTalk.SpecFlow;

namespace Eol.Cig.Etl.EolHosting.Integration
{
    [Binding]
    public class JobExecutionSteps
    {
        private IJobConfiguration _configuration;
        private IContainer _container;
        private ILog _logger;
        private string _randomDatabaseName;
        private string _defaultConnectionString;
        private string _randomConnectionString;
        private EtlEolHostingJob _job;

        [BeforeScenario]
        public void BeforeScenario()
        {
            _container = Bootstrapper.CreateContainer();
            _logger = _container.GetInstance<ILog>();
            _configuration = _container.GetInstance<IJobConfiguration>(Bootstrapper.JobName);
            _defaultConnectionString = ConnectionStringHelper.GetDefaultConnectionString("Destination_Default");
            _randomConnectionString = _configuration.DestinationConnectionString;
            _randomDatabaseName = _configuration.DatabaseName;
            try
            {
                SqlHelper.RunSqlScript(SqlHelper.GetDropStatement(_randomDatabaseName), _defaultConnectionString);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
            }

            SqlHelper.RunSqlScript(SqlHelper.GetCreateStatement(_randomDatabaseName), _defaultConnectionString);
            SqlHelper.RunSqlScript(SqlHelper.GetCreateSchemaStatement("raw"), _randomConnectionString);
            SqlHelper.RunSqlScript(SqlHelper.GetCreateSchemaStatement("config"), _randomConnectionString);
            SqlHelper.RunSqlScript(SqlScripts.CreateSummaryTable, _randomConnectionString);

            if (!Directory.Exists(_configuration.StagingFolder))
            {
                Directory.CreateDirectory(_configuration.StagingFolder);
            }
            if (!Directory.Exists(_configuration.TempFolder))
            {
                Directory.CreateDirectory(_configuration.TempFolder);
            }
        }

        [AfterScenario]
        public void AfterScenario()
        {
            SqlHelper.RunSqlScript(SqlScripts.DropSummaryTable, _randomConnectionString);
            SqlHelper.RunSqlScript(SqlHelper.GetDropStatement(_randomDatabaseName), _defaultConnectionString);
            Directory.Delete(_configuration.StagingFolder, true);
            Directory.Delete(_configuration.TempFolder, true);
        }

        [Given(@"I have a mocked source folder returning (.*) backup files")]
        public void GivenIHaveAMockedSourceFolderReturningBackupFiles(int fileNumbers)
        {
            var deployedFiles = Directory.GetFiles(_configuration.SourceFolder,"*.bak");
            Assert.AreEqual(fileNumbers, deployedFiles.Length);
        }

        [Given(@"I create an instance of the EolHostingJob")]
        public void GivenICreateAnInstanceOfTheEolHostingJob()
        {
            var serviceMock = new Mock<IEtlService>();
            _job = new EtlEolHostingJob(_logger, new EolHostingExtractorFactory(_container),
                new EolHostingUploaderFactory(_container), serviceMock.Object);
        }

        [When(@"I call the Execute method")]
        public void WhenICallTheExecuteMethod()
        {
            var contextMock = new Mock<IJobExecutionContext>();
            contextMock.Setup(x => x.Trigger.Description).Returns("Mocked Context");
            contextMock.Setup(x => x.JobDetail.Key).Returns(new JobKey(Bootstrapper.JobName, "Test"));
            contextMock.Setup(x => x.MergedJobDataMap["deltaFullPathToImport"]).Returns(null);
            contextMock.Setup(x => x.MergedJobDataMap["deltaFullPathToDelete"]).Returns(null);
            _job.Execute(contextMock.Object);
        }

        [Then(@"the result should be stored into the database")]
        public void ThenTheResultShouldBeStoredIntoTheDatabase()
        {
            int numberOfRows = SqlHelper.RunSqlScriptAndReturnInt($"select count(*) from raw.HOST_CIG_Accounts"
                                                , _randomConnectionString);
            int numberOftables = SqlHelper.RunSqlScriptAndReturnInt($"SELECT count(*) FROM INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = 'raw'"
                                                            , _randomConnectionString);
            ScenarioContext.Current.Add("newRowsCount", numberOfRows);
            ScenarioContext.Current.Add("newTablesCount", numberOftables);
        }

        [Then(@"I should have (.*) new rows in Account table")]
        public void ThenIShouldHaveNewRowsInAccountTable(int newRows)
        {
            var results = ScenarioContext.Current.Get<int>("newRowsCount");
            Assert.AreEqual(newRows, results);
        }

        [Then(@"I should have (.*) new raw tables")]
        public void ThenIShouldHaveNewRawTables(int newTables)
        {
            var results = ScenarioContext.Current.Get<int>("newTablesCount");
            Assert.AreEqual(newTables, results);
        }

    }
}
