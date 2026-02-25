# Microsoft Foundry: SharePoint Knowledge Integration - The Easy Way and The Hard Way

This is the companion code to my [blog series](https://www.origindigital.com/insights/microsoft-foundry-sharepoint-knowledge-integration) all about integrating Microsoft Foundry with SharePoint. I describe "The Easy Way" (using a SharePoint agent tool with an M365 license) and "The Hard Way" (a custom architecture to sync a document library into a Foundry Azure AI Search knowledge base).

## Change Log
| Version | Description |
| --- | --- |
| 1.0 | Initial release |
| 2.0 | Major refactor: simplified authentication, included sample infrastructure deployment GitHub actions and scripts, added API support for frontend clients, implemented Foundry Project APIs for agent/workflow integration, and added Azure AI Search skills for image extraction & verbalization, entity recognition, and custom web APIs (for image vectorization). |

## Getting Started

* The solution is written in C#, targeting Visual Studio 2026 and .NET 10.
* After finishing your Power Automate implementation for the Hard Way, paste your flow URL into the "TokenFlowURL" setting's value in the appsettings.json file. See the fourth post in the blog series linked above for details.
* The only other app settings are API versions and default ASP.NET Core logging configurations.
* Add an appsetings.Development.json file to the "FoundrySharePointKnowledge.API" project with the following content:
  
  ```
  {
    "AZURE_KEY_VAULT_URL": "https://<your-key-vault-name>.vault.azure.net",
    "CORS_ALLOWED_ORIGINS": [ "http://localhost:<your-local-dev-react-port>", "https://localhost:<your-local-dev-blazor-port>" ]
  }
  ```

## Azure Configuration

The Azure CLI script used to provision the Azure resource group can't be shared here, but I will list the components in use. The third post in the blog series linked above has a full architecture diagram.

* Foundry
* Foundry Project
* Azure AI Search
* App Service Plan (Windows)
* App Service (Web App)
* Azure Storage Account
* Key Vault
* Application Insights

### Security

You will need an admin-consented Entra ID app registration. This is used for Microsoft Graph access and Power Automate token acquisition to secure the API's endpoints. All the details are also in the third post in the blog series linked above. I will assume you have at least some flavor of "contribute" rights to SharePoint, Power Platform, Azure, and Entra ID.

To reduce exposure of API keys and connection strings, the following PaaS components have associations to one another:

* Azure AI Search: managed identity with Storage Blob Data Reader, Storage Blob Data Contributor, and Storage Table Data Contributor RBAC to the Azure Storage Account
* Azure AI Search: Cognitive Services User RBAC to Foundry
* Web App: managed identity with a Key Vault access policy granting "get" and "list" secret permissions
* Web App: managed identity with Storage Blob Data Reader and Storage Blob Data Contributor
* Web App: linked to Application Insights
* Web App: CORS for the frontend client app and Power Automate
* Storage Account: linked to Application Insights
* Storage Account: CORS for the frontend client app
* Entra ID auth app: Contributor, Azure AI Developer, Azure AI User, Cognitive Services Contributor, and Cognitive Services User RBAC to Foundry
* Entra ID auth app: a Key Vault access policy granting "get" and "list" secret permissions (not currently used)
* Entra ID auth app: Read access to a target SharePoint site collection (via Grant-PnPAzureADAppSitePermission)
* Entra ID deployment app: Contributor and Role Based Access Control Administrator RBAC to the Azure subscription
* Entra ID deployment app: a Key Vault access policy with full permissions
* Entra ID PnP.PowerShell app: add the Sites.FullControl.All Graph application permission

***Now in V2, the "infrastructure.sh" script will provision an Entra ID auth app as well as the entire resource group and configure security automatically!***

### Key Vault

The API's Program.cs file makes several calls to Key Vault to download secrets and bind them to settings objects. Using the access policy mentioned above, the code only needs the URL to key vault (from appsettings*.json) to not only beef up security but also simply the API's configuration and deployment. The following tables lists what you'll need to configure there:

| Secret | Example Value | Description |
| --- | --- | --- |
| search-api-url | https://your-azure-ai-search-name.search.windows.net | The absolute URL to your Azure AI Search PaaS resource. |
| search-admin-key | *** | Your Azure AI Search PaaS resource's primary admin key. |
| storage-account-resource-id | ResourceId=/subscriptions/your-subscription-id/resourceGroups/your-resource-group-name/providers/Microsoft.Storage/storageAccounts/your-storage-account-name; | The "Azure resource path" to your Storage Account PaaS resource (starting with "ResourceId=" and ending with ";"). |
| foundry-open-ai-endpoint | https://your-foundry-name.openai.azure.com/ | The "Project endpoint" listed on the Microsoft Foundry home page. |
| foundry-account-key | *** | The "Project API key" listed on the Microsoft Foundry home page. |
| embedding-model | text-embedding-3-small | The name of the embedding model deployed to Foundry. |
| auth-client-id | 11111111-2222-3333-4444-555555555555 | Your Entra ID app id. |
| auth-client-secret | *** | Your Entra ID app's client secret. |
| tenant-id | 11111111-2222-3333-4444-555555555555 | Your Entra ID tenant's unique identifier. |
| app-insights-connection-string | (Found on the Application Insights Azure Portal home page.) | The full connection string to your Application Insights PaaS resource. |
| storage-account-connection-string | (Found on the "Access Keys" blade of the Storage Account in the Azure Portal.) | The full connection string to your Azure Storage PaaS resource. |

***Now in V2, the "infrastructure.sh" script will provision all Key Vault secrets automatically!***

## Power Platform

As mentioned above, I use a Power Platform solution with three Power Automate flows and several environment variables as part the Hard Way architecture. All the details are in the fourth post in the blog series linked above, but I can't export those artifacts. The following table lists the environment variables you'll need:

| Name | Example Value | Description |
| --- | --- | --- |
| API URL | https://your-web-app-name.azurewebsites.net | The domain-only portion of the absolute URL to your API published to an Azure Web App. |
| Client Id | 11111111-2222-3333-4444-555555555555 | Your Entra ID app id. |
| Client Secret | *** | Your Entra ID app's client secret. |
| Tenant Id | 11111111-2222-3333-4444-555555555555 | Your Entra ID tenant's unique identifier. |
| SharePoint Site URL | https://your-tenant-name.sharepoint.com/sites/your-site-name | The absolute URL to the target SharePoint site collection. |
| SharePoint Library Name | Documents | The title of the SharePoint document library to be reasoned over by your Foundry agent. |

## Deployment

V2 adds sample GitHub actions for backend CI/CD, Azure provisioning, and granting an Entra ID app least permissive access to an M365 site collection. Here are the secrets you'll need to add to your GitHub repository:

| Name | Example Value | Description |
| --- | --- | --- |
| AZURE_DEPLOYMENT_CREDS | See [this](https://cdn.prod.website-files.com/656df9fa4598e805a50a1d26/6987d04ea3e0829a25d54a0b_Azure%20CLI%20Credentials%20In%20GitHub.png)  | A JSON string that holds the Entra app registration credential information used to authenticate Azure CLI. |
| PNP_CLIENT_ID | 11111111-2222-3333-4444-555555555555 | The guid of the Entra app registration installed by PnP (named PnP.PowerShell by default). |
| PNP_PFX_BASE64 | Base 64 string | The full base 64-encoded content of the PFX certificate used to authenticate the PnP.PowerShell Entra app registration. |
| PNP_PFX_PASSWORD | *** | The password of the PFX certificate. |
| PNP_TENANT_ID | 11111111-2222-3333-4444-555555555555 | The guid of your Entra tenant. |

