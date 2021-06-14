using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using WAS = Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure.Cosmos.Table;
// using Microsoft.WindowsAzure.Storage.Table;
// using Microsoft.WindowsAzure.Storage;

namespace secret_rotator
{

    

    public static class Rotator
    {
        [FunctionName("rotator")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log, [Table("%CredStoreTableName%", Connection = "CredStoreConnection")] CloudTable credTable)
        {
            // there is no filter on keyCredentials or passwordCredentials, so we can't filter there
            // there is also no deltaQuery that seems to track changes on passwordCredential or keyCredential - only create/delete
            // SOOOOOOOO that means ........ we're gonna have to do it ourselves jamesharden.gif
            // https://graph.microsoft.com/v1.0/applications?$count=true&$select=id,appDisplayName,keyCredentials&$filter=keyCredential/any(x:x/endDateTime lt '2021-12-31')
            var a = new GraphServiceClient(new MsalClientCredentialAuthenticationProvider());
            var apps = await a.Applications
                .Request()
                .Top(10) // force max page size to reduce the number of requests
                .Select("id,keyCredentials,passwordCredentials,appDisplayName")
                .GetAsync();

            log.LogInformation($"Got {apps.Count} apps");

            var creds = new List<CredentialEntity>();

            var appIterator = PageIterator<Application>.CreatePageIterator(a, apps, app =>
            {
                var keyCreds = app.KeyCredentials.Select(k => new CredentialEntity()
                {
                    PartitionKey = CredentialEntity.DerivePartitionKey(k.EndDateTime.Value.UtcDateTime).ToString(),
                    RowKey = k.KeyId.ToString(),
                    ParentAppDisplayName = app.DisplayName,
                    ParentAppObjectId = app.Id,
                    ParentAppId = app.AppId,
                    DisplayName = k.DisplayName,
                    EndDateTime = k.EndDateTime.Value.UtcDateTime,
                    StartDateTime = k.StartDateTime.Value.UtcDateTime,
                    KeyId = k.KeyId.ToString(),
                    Type = k.Type,
                    Usage = k.Usage,
                    SecretType = SecretType.Certificate
                }).ToList(); //prevent multitple evaluations

                creds.AddRange(keyCreds);

                var secretCreds = app.PasswordCredentials.Select(p => new CredentialEntity()
                {
                    PartitionKey = CredentialEntity.DerivePartitionKey(p.EndDateTime.Value.UtcDateTime).ToString(),
                    // todo: is keyid valid for pw creds? I don't think so
                    RowKey = p.KeyId.ToString(),
                    ParentAppDisplayName = app.DisplayName,
                    ParentAppObjectId = app.Id,
                    ParentAppId = app.AppId,
                    DisplayName = p.DisplayName,
                    EndDateTime = p.EndDateTime.Value.UtcDateTime,
                    StartDateTime = p.StartDateTime.Value.UtcDateTime,
                    KeyId = p.KeyId.ToString(),
                    SecretType = SecretType.SecretString
                }).ToList(); //prevent multitple evaluations

                creds.AddRange(secretCreds);

                return true;
            });

            await appIterator.IterateAsync();

            // group by partition key, then batch into 100 entity requests
            var groups = creds.GroupBy(x => x.PartitionKey);
            foreach (var g in groups)
            {
                foreach (var batch in g.Batch(100))
                {
                    var batchOp = new TableBatchOperation();
                    foreach (var e in batch)
                    {
                        batchOp.Add(TableOperation.InsertOrReplace(e));
                    }
                    await credTable.ExecuteBatchAsync(batchOp);
                }
            }
        }
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> collection, int batchSize)
        {
            var nextbatch = new List<T>(batchSize);
            foreach (T item in collection)
            {
                nextbatch.Add(item);
                if (nextbatch.Count == batchSize)
                {
                    yield return nextbatch;
                    nextbatch = new List<T>(batchSize);
                }
            }

            if (nextbatch.Count > 0)
            {
                yield return nextbatch;
            }
        }

        // private Task SendEmail(GraphServiceClient client, List<CredentialEntity> creds)
        // {
        //     var bodyTemplate = @"<html><head/><body><h1>Hello, some secrets are expiring:</h1><table>{0}</table></body></html>";

        //     var message = new Microsoft.Graph.Message()
        //     {
        //         ToRecipients = new List<Recipient>() { new Recipient() { EmailAddress = new EmailAddress() { Address = "john@jpd.ms" } } },
        //         Subject = "Secret Agent Man - things are expiring",
        //         Body = new ItemBody()
        //         {
        //             Content = string.Format(bodyTemplate, ""),
        //             ContentType = BodyType.Html
        //         }
        //     };
        //     return Task.CompletedTask;
        // }
    }


    public class MsalClientCredentialAuthenticationProvider : IAuthenticationProvider
    {
        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var msal = ConfidentialClientApplicationBuilder
              .Create("2032982c-48b3-48a6-9421-2dad7fdf2ee0")
              .WithTenantId("jpda.onmicrosoft.com")
              .WithClientSecret("1m.NBznv~Ytc6j7~rtTINH_.1_Ej55g053")
              .Build();

            var tokenRequest = await msal.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" }).ExecuteAsync();

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenRequest.AccessToken);
        }
    }
}
