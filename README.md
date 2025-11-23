# Microsoft Foundry: SharePoint Knowledge Integration - The Easy Way and The Hard Way

This is the companion code to my [blog series](https://www.origindigital.com/insights/microsoft-foundry-sharepoint-knowledge-integration) all about integrating Microsoft Foundry with SharePoint. I describe "The Easy Way" (using a SharePoint agent tool with an M365 license) and "The Hard Way" (a custom architecture to sync a document library into a Foundry Azure AI Search knowledge base).

## Getting Started

* The solution is written in C# and targets Visual Studio 2026 and .NET 10.
* After finishing your Power Automate implementation for the Hard Way, paste your flow URL into the "TokenFlowURL" setting's value in the appsettings.json file.
* Add an appsetings.Development.json file to the "FoundrySharePointKnowledge.API" project with the following content:
  
  ```
  {
    "KeyVaultURL": "https://your-key-vault-name.vault.azure.net"
  }
  ```

## Azure Configuration

The Azure CLI script used to provision the Azure resource group can't be shared here, but I will list the components in use. The third post in the blog series linked above has a full architecture diagram.

* Foundry
* Foundry Project
* Azure AI Search
* App Service Plan
* App Service (Web App)
* Azure Storage Account
* Key Vault
* Application Insights

### Security

You will need an admin-consented Entra ID app registration. This is used for Microsoft Graph access and Power Automate token acquisition to secure the API's endpoints. All the details are also in the third post in the blog series linked above. I will assume you have at least some flavor of "contribute" rights to SharePoint, Power Platform, Azure, and Entra ID.

To reduce exposure of API keys and connection strings, the following PaaS components have managed identities or other associations to directly communicate with each other:

* Azure AI Search: managed identity with "Storage Blob Data Reader" RBAC to the Azure Storage Account
* Web App: managed identity with a Key Vault access policy granting "get" and "list" secret permissions
* Web App: linked to Application Insights
* Storage Account: linked to Application Insights

### Key Vault

The API's Program.cs file makes several calls to Key Vault to download secrets and bind them to settings objects. Using the access policy mentioned above, the code only needs the URL to key vault (from appsettings*.json) to not only beef up security but also simply the API's configuration and deployment. The following tables lists what you'll need to configure there:

| Secret | Example Value | Description |
| --- | --- | --- |
| search-api-url | https://your-azure-ai-search-instance-name.search.windows.net | The absolute URL to your Azure AI Search PaaS resource. |
| search-admin-key | *** | Your Azure AI Search PaaS resource's primary admin key. |
| storage-account-resource-id | ResourceId=/subscriptions/your-subscription-id/resourceGroups/your-resource-group-name/providers/Microsoft.Storage/storageAccounts/your-storage-account-name; | The "Azure resource path" to your Storage Account PaaS resource (starting with "ResourceId=" and ending with ";"). |
| foundry-account-key | *** | The "Project endpoint" listed on the Microsoft Foundry home page. |
| foundry-open-ai-endpoint | https://your-foundry-name.openai.azure.com/ | The absolute URL to your Foundry's OpenAI endpoint. |
| embedding-model | text-embedding-3-small | The name of the embedding model deployed to Foundry. |
| auth-client-id | 11111111-2222-3333-4444-555555555555 | Your Entra ID app id. |
| auth-client-secret | *** | Your Entra ID app's client secret. |
| tenant-id | 11111111-2222-3333-4444-555555555555 | Your Entra ID tenant's unique identifier. |
| app-insights-connection-string | (Found on the Application Insights Azure Portal home page.) | The full connection string to your Application Insights PaaS resource. |
| storage-account-connection-string | (Found on the "Access Keys" blade of the Storage Account in the Azure Portal.) | The full connection string to your Azure Storage PaaS resource. |

## Power Platform

As mentioned above, I use a Power Platform solution with three Power Automate flows and several environment variables as part the Hard Way architecture. All the details are in the fourth post in the blog series linked above, but I can't export those artifacts. The following table lists the environment variables you'll need:

| Name | Example Value | Description |
| --- | --- | --- |
| API URL | https://your-web-app-name.azurewebsites.net | The domain name of your API published to an Azure Web App. |
| Client Id | 11111111-2222-3333-4444-555555555555 | Your Entra ID app id. |
| Client Secret | *** | Your Entra ID app's client secret. |
| Tenant Id | 11111111-2222-3333-4444-555555555555 | Your Entra ID tenant's unique identifier. |
| SharePoint Site URL | https://your-tenant-name.sharepoint.com/sites/your-site-name | The absolute URL to the target SharePoint site collection. |
| SharePoint Library Name | Foundry Sync | The title of the SharePoint document library to poll. |

