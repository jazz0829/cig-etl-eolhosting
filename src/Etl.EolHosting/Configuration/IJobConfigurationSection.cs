namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public interface IJobConfigurationSection 
    {
        JobConfigurationCollection Instances { get; set; }
    }
}
