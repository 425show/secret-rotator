using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos.Table;
using _425show.SecretManager;
using System;

namespace secret_rotator
{
    public class AppSecretManagerServiceConfiguration
    {
        public string FullSyncRecurrence { get; set; }
        public string ScanRecurrence { get; set; }
    }

    public class Rotator
    {
        private readonly AppSecretManager _manager;
        private readonly ILogger _logger;
        public Rotator(AppSecretManager manager, ILogger<Rotator> logger)
        {
            _manager = manager;
            _logger = logger;
        }

        [FunctionName("scan")]
        public async Task Scan([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, [Table("%CredStoreTableName%", Connection = "CredStoreConnection")] CloudTable credTable)
        {
            _logger.LogInformation($"{DateTime.UtcNow:o}: Looking for today's expiring credentials from storage");
            var todayKey = CredentialEntity.DerivePartitionKey(DateTime.UtcNow);
            _logger.LogInformation($"Looking for key {todayKey}");
            var creds = await _manager.GetExpiringCredentials(todayKey.ToString());
        }

        [FunctionName("Sync")]
        public async Task Sync([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, [Table("%CredStoreTableName%", Connection = "CredStoreConnection")] CloudTable credTable)
        {
            var creds = await _manager.GetCredentialMetadata();
            await _manager.PersistCredentialMetadata(creds);
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
}