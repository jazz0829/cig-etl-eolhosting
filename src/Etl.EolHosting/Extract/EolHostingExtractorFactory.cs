using StructureMap;

namespace Eol.Cig.Etl.EolHosting.Extract
{
    public class EolHostingExtractorFactory : IEolHostingExtractorFactory
    {
        private readonly IContainer _container;

        public EolHostingExtractorFactory(IContainer container)
        {
            _container = container;
        }

        public IEolHostingExtractor Create(string jobName)
        {
            return _container.GetInstance<IEolHostingExtractor>(jobName);
        }
    }
}
