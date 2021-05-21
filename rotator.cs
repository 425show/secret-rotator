using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Linq;

namespace secret_rotator
{
    public static class rotator
    {
        [FunctionName("rotator")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        {
            var a = new GraphServiceClient(new MsalClientCredentialAuthenticationProvider());
            var expiringSecrets = await a.ServicePrincipals
                .Request()
                .Select("id,keyCredentials,appDisplayName")
                .Filter("") //todo: filter to only non-empty arrays
                .GetAsync();

                

            //expiringSecrets.Select(x=> new { x.Id, x.AppDisplayName, x.KeyCredentials })


            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        private Task SendEmail(GraphServiceClient client, List<)
        {
            var bodyTemplate = @"<html><head/><body><h1>Hello, some secrets are expiring:</h1><table>{0}</table></body></html>";



            var message = new Microsoft.Graph.Message()
            {
                ToRecipients = new List<Recipient>() { new Recipient() { EmailAddress = new EmailAddress() { Address = "john@jpd.ms" } } },
                Subject = "Secret Agent Man - things are expiring",
                Body = new ItemBody()
                {
                    Content = string.Format(bodyTemplate, ""),
                    ContentType = BodyType.Html
                }
            };
        }
    }


    public class MsalClientCredentialAuthenticationProvider : IAuthenticationProvider
    {
        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var msal = ConfidentialClientApplicationBuilder
              .Create("")
              .WithTenantId("jpda.onmicrosoft.com")
              .WithClientSecret("")
              .Build();

            var tokenRequest = await msal.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" }).ExecuteAsync();

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenRequest.AccessToken);
        }
    }
}
