using Eol.Cig.Etl.Shared.Service;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Eol.Cig.Etl.EolHosting.Service
{
    [ServiceContract]
    public interface IEtlEolHostingServiceApi : IEtlServiceApi
    {
        [OperationContract(Name = "ExecuteSingleDelta")]
        [WebGet]
        string ExecuteSingleDelta(string deltaFullPathToImport);


        [OperationContract(Name = "DeleteSingleDelta")]
        [WebGet]
        string DeleteSingleDelta(string deltaFullPathToImport);
    }
}
