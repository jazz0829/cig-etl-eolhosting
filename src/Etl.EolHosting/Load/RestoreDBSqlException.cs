using System;
using System.Runtime.Serialization;

namespace Eol.Cig.Etl.EolHosting.Load
{
    [Serializable]
    internal class RestoreDbSqlException : Exception
    {
        public RestoreDbSqlException()
        {
        }

        public RestoreDbSqlException(string message) : base(message)
        {
        }

        public RestoreDbSqlException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected RestoreDbSqlException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}