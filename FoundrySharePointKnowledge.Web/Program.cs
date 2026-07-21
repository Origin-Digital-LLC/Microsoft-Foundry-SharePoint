using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Web
{
    /// <summary>
    /// This is the test harness UI.
    /// </summary>
    public class Program
    {
        #region Initialization
        /// <summary>
        /// This is the main entry point to the app.
        /// </summary>
        public static async Task Main(string[] args)
        {
            //initialization
            string api = nameof(FSPKConstants.Settings.Blazor.API);
            WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
            Uri apiURL = new Uri(builder.Configuration.GetValue<string>($"{api}:{FSPKConstants.Settings.Blazor.API.URL}"));

            //configure app root components
            builder.RootComponents.Add<App>(FSPKConstants.Blazor.ApplicationRoot);
            builder.RootComponents.Add<HeadOutlet>(FSPKConstants.Blazor.HeadOutlet);

            //configure authentication
            builder.Services.AddMsalAuthentication(options =>
            {
                //add foundry scope
                builder.Configuration.Bind(FSPKConstants.Settings.EntraId, options.ProviderOptions.Authentication);
                options.ProviderOptions.DefaultAccessTokenScopes.Add(FSPKConstants.Foundry.Scope);
            });

            //require authentication for every route by default
            builder.Services.AddAuthorizationCore(options =>
            {
                //return
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            });

            //add API http client
            builder.Services.AddHttpClient(api, (client) =>
            {
                //configure client
                client.BaseAddress = apiURL;
            }).AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

            //return
            await builder.Build().RunAsync();
        }
        #endregion
    }
}
