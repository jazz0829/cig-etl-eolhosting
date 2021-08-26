using System.Net.Mail;
using System.Text;

namespace Eol.Cig.Etl.EolHosting
{
    public static class Utility
    {
        public static void Notify(object changes)
        {
            var client = new SmtpClient("smtp.exact.nl", 25);
            MailAddress from = new MailAddress("SQLCorp@exact.com", "EOL Hosting ETL Job");
            MailAddress to = new MailAddress("cig@exact.com", "Customer Intelligence Group");
            MailMessage message = new MailMessage(from, to)
            {
                Body = changes.ToString()
            };

            message.BodyEncoding = Encoding.UTF8;
            message.Priority = MailPriority.High;
            message.Subject = "Schema changes were detected in EOL backup files";
            message.SubjectEncoding = Encoding.UTF8;

            client.Send(message);
        }
    }
}
