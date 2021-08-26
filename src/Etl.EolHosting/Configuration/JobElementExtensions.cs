using System;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public static class SurveyJobElementExtensions
    {
        public static string GetStringOrThrow(this JobElement jobSettings, string configElement)
        {
            var value = jobSettings[configElement];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Jobsetting element '{configElement}' is missing!");
            }
            return value;
        }
    }
}
