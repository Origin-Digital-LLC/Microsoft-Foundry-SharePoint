# Microsoft-Foundry-SharePoint-Knowledge

This is the companion code to my [blog series](https://www.origindigital.com/insights/microsoft-foundry-sharepoint-knowledge-integration) all about integrating Microsoft Foundry with SharePoint. I describe "The Easy Way" (using a SharePoint agent tool with an M365 license) and "The Hard Way" (a custom architecture to sync a document library into a Foundry Azure AI Search knowledge base).

## Project Details

* The solution targets Visual Studio 2026 and .NET 10
* Add an appsetings.Development.json file to the "FoundrySharePointKnowledge.API" project with the following content:

  ```
  {
    "KeyVaultURL": "https://<key vault name>.vault.azure.net"
  }
  ```



