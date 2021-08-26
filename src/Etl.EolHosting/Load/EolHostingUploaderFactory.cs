using StructureMap;

namespace Eol.Cig.Etl.EolHosting.Load
{
    public class EolHostingUploaderFactory : IEolHostingUploaderFactory
    {
        private readonly IContainer _container;

        public EolHostingUploaderFactory(IContainer container)
        {
            _container = container;
        }

        public IEolHostingUploader Create(string jobName)
        {
            return _container.GetInstance<IEolHostingUploader>(jobName);
        }
    }
}
