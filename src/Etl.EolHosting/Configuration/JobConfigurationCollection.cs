using System.Configuration;
using System.Linq;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public class JobConfigurationCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new JobElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((JobElement)element).Name;
        }

        public new JobElement this[string elementName]
        {
            get
            {
                return this.OfType<JobElement>().FirstOrDefault(item => item.Name == elementName);
            }
        }
    }
}
