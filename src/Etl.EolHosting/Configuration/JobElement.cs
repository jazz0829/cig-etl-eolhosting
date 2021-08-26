using System.Configuration;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public class JobElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name => (string)base["name"];

        [ConfigurationProperty("connectionStringName", IsRequired = false)]

        public string ConnectionStringName => (string)base["connectionStringName"];

        [ConfigurationProperty("sourceConnectionStringName", IsRequired = true)]

        public string SourceConnectionStringName => (string)base["sourceConnectionStringName"];

        [ConfigurationProperty("destinationConnectionStringName", IsRequired = true)]

        public string DestinationConnectionStringName => (string)base["destinationConnectionStringName"];

        [ConfigurationProperty("sourceFolder", IsRequired = true)]

        public string SourceFolder => (string)base["sourceFolder"];

        [ConfigurationProperty("stagingFolder", IsRequired = true)]
        public string StagingFolder => (string)base["stagingFolder"];

        [ConfigurationProperty("tempFolder", IsRequired = true)]
        public string TempFolder => (string)base["stagingFolder"];

        [ConfigurationProperty("summaryTableName", IsRequired = true)]
        public string SummaryTableName => (string)base["summaryTableName"];


        public new string this[string propertyName] => (string)base[propertyName];

        public void Add(string propertyName, string propertyValue)
        {
            base[propertyName] = propertyValue;
        }
    }
}
