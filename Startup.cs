using _425show.Msal.Extensions;
using _425show.SecretManager;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;

[assembly: FunctionsStartup(typeof(secret_rotator.Startup))]

namespace secret_rotator
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var services = builder.Services;
            services.AddOptions<SecretCredentialConfiguration>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("AzureAd:Credential").Bind(settings);
                });
            services.AddOptions<AzureAdConfiguration>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("AzureAd").Bind(settings);
                });
            services.AddOptions<TableCredStoreConfiguration>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("CredStoreConnection").Bind(settings);
                });
            services.AddOptions<AppSecretManagerServiceConfiguration>()
                .Configure<IConfiguration>((settings, config) =>
                {
                    config.GetSection("SecretManager").Bind(settings);
                });
            services.AddSingleton<ICredentialStore, AzureTableCredStore>();
            services.AddSingleton<IMsalCredential, SecretCredential>();
            services.AddSingleton<MsalBuilder>();
            services.AddSingleton<IAuthenticationProvider, MsalBuilderCredentialAuthenticationProvider>();
            services.AddSingleton<GraphServiceClient>();
            services.AddSingleton<AppSecretManager>();
            //services.AddHostedService<SecretManagerService>();
        }
    }
}