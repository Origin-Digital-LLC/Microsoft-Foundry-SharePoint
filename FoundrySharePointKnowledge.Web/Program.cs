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

            //this API is its own app registration hosted on a different origin than the Blazor app, so the API's
            //own "access_as_user" scope must be requested explicitly rather than relying on the base address handler
            string apiClientId = builder.Configuration.GetValue<string>($"{FSPKConstants.Settings.EntraId}:ClientId");
            string apiScope = $"{FSPKConstants.Security.TokenValidation.APIAudience}{apiClientId}/{FSPKConstants.Security.TokenValidation.Scope}";

            //configure authentication (the api scope is requested separately via the handler below, not here,
            //since AAD rejects a single token request that spans scopes from more than one resource)
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

            //add API http client (a plain AuthorizationMessageHandler is required, rather than BaseAddressAuthorizationMessageHandler,
            //because the API is hosted on a different origin than this Blazor app; that handler only attaches tokens to requests
            //made to the app's own origin)
            builder.Services.AddHttpClient(api, (client) =>
            {
                //configure client
                client.BaseAddress = apiURL;
            }).AddHttpMessageHandler(serviceProvider =>
            {
                //authorize only calls to the api origin, using the api's own scope
                AuthorizationMessageHandler handler = serviceProvider.GetRequiredService<AuthorizationMessageHandler>();
                handler.ConfigureHandler(new string[] { apiURL.ToString() }, new string[] { apiScope });
                return handler;
            });

            //return
            await builder.Build().RunAsync();
        }
        #endregion
    }
}
