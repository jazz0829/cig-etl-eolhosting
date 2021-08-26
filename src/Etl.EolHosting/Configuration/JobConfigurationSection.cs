using System.Configuration;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public class JobConfigurationSection : ConfigurationSection, IJobConfigurationSection
    {
        [ConfigurationProperty("", IsRequired = true, IsDefaultCollection = true)]
        public JobConfigurationCollection Instances
        {
            get => (JobConfigurationCollection)this[""];
            set => this[""] = value;
        }
    }
}
