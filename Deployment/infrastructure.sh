#!/bin/bash

#initialization
appURL="$4";
subDomain="$5";
ownerEmail="$8";
localhostURL="$9";
foundrySKU="${10}";
primaryRegion="$6";
authRedirectURLs="";
secondaryRegion="$7";
resourceGroupName="$2";
environmentNormalized="${1,,}";
sharepointWebhookSecret="${11}";
deploymentEnterpriseAppObjectId="$3";

#these values should also be inputs, but github only allows 10 parameters :(
searchSKU="standard";
keyVaultSKU="standard";
appServicePlankSKU="B1";
storageKind="StorageV2";
storageSKU="Standard_LRS";
timezone="Central Standard Time";
foundryProjectName="fspk-agent-pool";

#infrastructure constants
authCallback="";
imageVersion="1";
llmCapacity="501";
imageCapacity="5";
llmModel="gpt-4.1";
llmFormat="OpenAI";
imageFormat="Cohere";
embeddingVersion="2";
llmVersion="2025-04-14";
embeddingCapacity="511";
imageModel="embed-v-4-0";
embeddingFormat="OpenAI";
dotNetCoreRuntime="dotnet:10";
foundryAPIVersion="2024-05-01-preview";
embeddingModel="text-embedding-3-small";
swaggerCallback="/swagger/oauth2-redirect.html";
resourceGroupName="$resourceGroupName-$environmentNormalized";

#entra id constants
emailScope="email";
signInScope="openid";
profileScope="profile";
userReadScope="User.Read";
apiScopeName="access_as_user";
sitesReadAllScope="Sites.Read.All";
selectedSitesScope="Sites.Selected";
m365AppId="00000003-0000-0ff1-ce00-000000000000";
apiScopeId="3e57e494-4514-4155-bb1d-6ea84a8ceB5e";
azureCLIAppId="04b07795-8ddb-461a-bbee-02f9e1bf7b46";
cognitiveServicesUserPermission="Cognitive Services User";
storageBlobDataReaderPermission="Storage Blob Data Reader";
graphAPIPermissionId="00000003-0000-0000-c000-000000000000";
emailPermissionScopeId="64a6cdd6-aab1-4aaf-94b8-3cc8405e90d0=Scope";
storageBlobDataContributorPermission="Storage Blob Data Contributor";
signInPermissionScopeId="37f7f235-527c-4136-accd-4a02d197296e=Scope";
sharePointOnlineWebClientAppId="08e18876-6177-487e-b8b5-cf950c1e598c";
profilePermissionScopeId="14dad69e-099b-42c9-810b-d002981feec1=Scope";
userReadPermissionScopeId="e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope";
storageTableDataContributorPermission="Storage Table Data Contributor";
selectedSitesPermissionScopeId="883ea226-0bf2-4a8f-9f9d-92c9162a727d=Role";
sitesReadAllPermissionScopeId="205e70e5-aba6-4c52-a976-6d2d46c48043=Scope";
preAuthorizedAppIds="$m365AppId|$sharePointOnlineWebClientAppId|$azureCLIAppId";

#reference utilities
source ./deployment/utilities.sh;
echo "Starting Foundry SharePoint Knowledge $resourceGroupName infrastructure deployment.";

#add extensions
add_extension "application-insights";

#configure environment-specific values
case "$environmentNormalized" in   
    "uat")
        #uat resource parameters
        localhostURL="";
        subDomain="$subDomain-uat";
        ;;
    "prd")
        #prod resource parameters
        localhostURL="";
        ;;
    *)
        #default to dev resource parameters
        subDomain="$subDomain-$environmentNormalized";
        authRedirectURLs="'$localhostURL$authCallback','$localhostURL$swaggerCallback'";
        ;;
esac

#ensure resource group
tenantId=$(get_tenant_id);
subscriptionId=$(get_subscription_id);
resourceGroupResult=$(ensure_resource_group "$resourceGroupName" "$primaryRegion");

#ensure key vault
keyVaultName="$resourceGroupName-vault01";
keyVaultResult=$(ensure_key_vault "$resourceGroupName" "$primaryRegion" "$keyVaultName" "$keyVaultSKU" "$deploymentEnterpriseAppObjectId" "" "$ownerEmail");

#ensure app insights
appInsightsName="$resourceGroupName-telemetry01";
appInsightsResult=$(ensure_app_insights "$resourceGroupName" "$primaryRegion" "$appInsightsName");

#parse app insights components
appInsightsComponents=(${appInsightsResult//|/ });
appInsightsConnectionString=${appInsightsComponents[0]};
appInsightsInstramentationKey=${appInsightsComponents[1]};

#ensure storage account
storageAccountName="$resourceGroupName-storage01";
storageAccountName="${storageAccountName//-/}";
storageAccountResult=$(ensure_storage_account "$resourceGroupName" "$primaryRegion" "$storageAccountName" "$storageSKU" "$storageKind" "$appInsightsName");

#parse storage account components
storageAccountComponents=(${storageAccountResult//|/ });
storageAccountConnectionString=${storageAccountComponents[0]};
storageAccountKey=${storageAccountComponents[1]};
storageAccountId=${storageAccountComponents[2]};

#configure storage account
blobServices=$(enable_storage_account_blob_soft_delete "$resourceGroupName" "$storageAccountName");
webURL=$(ensure_storage_account_static_website "$resourceGroupName" "$storageAccountName" "$storageAccountKey");

#ensure app service plan
appServicePlanName="$resourceGroupName-farm01";
appPlanResult=$(ensure_app_service_plan "$resourceGroupName" "$primaryRegion" "$appServicePlanName" "$appServicePlankSKU" "false");

#ensure api app
apiName="$resourceGroupName-api01";
apiWebAppResult=$(ensure_web_app "$resourceGroupName" "$appServicePlanName" "$apiName" "$dotNetCoreRuntime" "$timezone" "$appInsightsName" "$keyVaultName" "" "");

#parse api app components
apiComponents=(${apiWebAppResult//|/ });
apiPrincipalId=${apiComponents[0]};
apiURL=${apiComponents[1]};

#build api app cors rules
apiCORSOrigins=$webURL;
if [ ! -z "$localhostURL" ]; then
    apiCORSOrigins="$apiCORSOrigins $localhostURL";
fi 

#allow power automate flows
apiCORSOrigins="$apiCORSOrigins https://make.powerautomate.com";

#configure api app
sleep 5;
apiCORSResult=$(ensure_web_app_cors "$resourceGroupName" "$apiName" "$apiCORSOrigins" "true");
staticWebAppStorageRBACBlobDataReader=$(ensure_rbac_access "$apiPrincipalId" "$storageAccountId" "$storageBlobDataReaderPermission");
staticWebAppStorageRBACBlobDataContributor=$(ensure_rbac_access "$apiPrincipalId" "$storageAccountId" "$storageBlobDataContributorPermission");

#check app URL
appURLLeadingCharacter="${appURL:0:1}";
if [[ "$appURLLeadingCharacter" != "." ]]; then
    appURL=".$appURL";
fi

#update entra id auth app URLs
appURL="https://$subDomain$appURL";
webURLCallback="$webURL$authCallback";
appRedirectURL="$appURL$authCallback";

#update redirect URLs
if [ -z "$authRedirectURLs" ]; then
    authRedirectURLs="'$webURLCallback','$appRedirectURL'";
else
    authRedirectURLs="$authRedirectURLs,'$webURLCallback','$appRedirectURL'";
fi

#ensure entra id app
appName="$resourceGroupName-app01";
appResult=$(ensure_entra_id_app_registration "$appName" "$authRedirectURLs" "$ownerEmail");

#parse entra id auth app components
appComponents=(${appResult//|/ });
authClientId=${appComponents[0]};
authClientSecret=${appComponents[1]};

#expose app api scope
appAPIScope=$(expose_entra_id_app_scope "$authClientId" "$apiScopeId" "$apiScopeName" "$preAuthorizedAppIds");

#ensure "user" key vault acccess policy for the entra id auth app
authEnterpriseAppObjectId=$(get_app_registration_enterprise_object_id "$authClientId");
authAppAccessPolicyResult=$(ensure_key_vault_user_access_policy "$keyVaultName" "$authEnterpriseAppObjectId");

#ensure entra id auth app permissions
emailPermission=$(assign_app_permission "$authClientId" "$graphAPIPermissionId" "$emailPermissionScopeId" "$emailScope");
profilePermission=$(assign_app_permission "$authClientId" "$graphAPIPermissionId" "$profilePermissionScopeId" "$profileScope");
userReadPermission=$(assign_app_permission "$authClientId" "$graphAPIPermissionId" "$userReadPermissionScopeId" "$userReadScope");
signInPermissionPermission=$(assign_app_permission "$authClientId" "$graphAPIPermissionId" "$signInPermissionScopeId" "$signInScope");
sitesReadAllPermission=$(assign_app_permission "$authClientId" "$graphAPIPermissionId" "$sitesReadAllPermissionScopeId" "$sitesReadAllScope");
selectedSitesPermission=$(assign_app_permission "$authClientId" "$graphAPIPermissionId" "$selectedSitesPermissionScopeId" "$selectedSitesScope");

#ensure foundry
foundryName="$resourceGroupName-foundry01";
foundryResult=$(ensure_foundry_project "$resourceGroupName" "$secondaryRegion" "$foundryName" "$foundryProjectName" "$foundrySKU" "$authEnterpriseAppObjectId");

#parse foundry components
foundryComponents=(${foundryResult//|/ });
foundryAccountKey=${foundryComponents[0]};
foundryResourceId=${foundryComponents[4]};
foundryOpenAIEndpoint=${foundryComponents[1]};
foundryProjectEndpoint=${foundryComponents[3]};
foundryInferenceEndpoint=${foundryComponents[5]};
foundryDocumentIntelligenceEndpoint=${foundryComponents[2]};

#ensure foundry model deployments
llmModelResult=$(ensure_foundry_model_deployment "$resourceGroupName" "$foundryName" "$llmModel" "$llmVersion" "$llmCapacity" "$llmFormat");
imageModelResult=$(ensure_foundry_model_deployment "$resourceGroupName" "$foundryName" "$imageModel" "$imageVersion" "$imageCapacity" "$imageFormat");
embeddingModelResult=$(ensure_foundry_model_deployment "$resourceGroupName" "$foundryName" "$embeddingModel" "$embeddingVersion" "$embeddingCapacity" "$embeddingFormat");

#ensure search
searchName="$resourceGroupName-search01";
searchProvisioningResult=$(ensure_azure_search "$resourceGroupName" "$secondaryRegion" "$searchName" "$searchSKU");

#parse search components
searchComponents=(${searchProvisioningResult//|/ });
searchPrincipalId=${searchComponents[2]};
searchAdminKey=${searchComponents[1]};
searchQueryKey=${searchComponents[0]};

#grant search RBAC access to other resources
sleep 5;
searchStorageRBACBlobDataReader=$(ensure_rbac_access "$searchPrincipalId" "$storageAccountId" "$storageBlobDataReaderPermission");
searchFoundryRBACCognitiveServicesUser=$(ensure_rbac_access "$searchPrincipalId" "$foundryResourceId" "$cognitiveServicesUserPermission");
searchStorageRBACBlobDataContributor=$(ensure_rbac_access "$searchPrincipalId" "$storageAccountId" "$storageBlobDataContributorPermission");
searchStorageRBACTableDataContributor=$(ensure_rbac_access "$searchPrincipalId" "$storageAccountId" "$storageTableDataContributorPermission");

#configure storage account CORS
clearCORS=$(clear_storage_account_blob_cors_rules "$storageAccountName" "$storageAccountKey");
addWebCORSRule=$(add_storage_account_blob_cors_rule "$storageAccountName" "$storageAccountKey" "$webURL");
addAPPCORSRule=$(add_storage_account_blob_cors_rule "$storageAccountName" "$storageAccountKey" "$appURL");
if [ -z "$localhostURL" ]; then
    echo "Not adding localhost CORS rule to $storageAccountName for the $resourceGroupName environment.";
else    
    addLocalhostCORSRule=$(add_storage_account_blob_cors_rule "$storageAccountName" "$storageAccountKey" "$localhostURL");
fi

#ensure url secrets
webURLResult=$(ensure_key_vault_secret "$keyVaultName" "web-url" "$webURL");
appURLResult=$(ensure_key_vault_secret "$keyVaultName" "app-url" "$appURL");
apiURLResult=$(ensure_key_vault_secret "$keyVaultName" "api-url" "$apiURL");

#ensure security secrets
tenantIdResult=$(ensure_key_vault_secret "$keyVaultName" "auth-tenant-id" "$tenantId");
authClientIdResult=$(ensure_key_vault_secret "$keyVaultName" "auth-client-id" "$authClientId");
subscriptionIdResult=$(ensure_key_vault_secret "$keyVaultName" "subscription-id" "$subscriptionId");
apiPrincipalIdResult=$(ensure_key_vault_secret "$keyVaultName" "api-principal-id" "$apiPrincipalId");
authClientSecretResult=$(ensure_key_vault_secret "$keyVaultName" "auth-client-secret" "$authClientSecret");
sharepointWebhookSecretResult=$(ensure_key_vault_secret "$keyVaultName" "sharepoint-webhook-secret" "$sharepointWebhookSecret");

#ensure storage account secrets
storageAccountKeyResult=$(ensure_key_vault_secret "$keyVaultName" "storage-account-key" "$storageAccountKey");
storageAccountNameResult=$(ensure_key_vault_secret "$keyVaultName" "storage-account-name" "$storageAccountName");
storageAccountIdResult=$(ensure_key_vault_secret "$keyVaultName" "storage-account-resource-id" "ResourceId=$storageAccountId;");
storageAccountConnectionStringResult=$(ensure_key_vault_secret "$keyVaultName" "storage-account-connection-string" "$storageAccountConnectionString");

#ensure foundry secrets
imageModelResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-image-model" "$imageModel");
foundryllmModelResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-llm-model" "$llmModel");
imageModelResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-image-version" "$imageVersion");
imageModelResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-image-capacity" "$imageCapacity");
foundryllmVersionResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-llm-version" "$llmVersion");
foundryllmCapacityResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-llm-capacity" "$llmCapacity");
foundryAccountKeyResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-account-key" "$foundryAccountKey");
foundryAPIVersionlResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-api-version" "$foundryAPIVersion");
foundryEmbeddingModelResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-embedding-model" "$embeddingModel");
foundryEmbeddingVersionResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-embedding-version" "$embeddingVersion");
foundryOpenAIEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-open-ai-endpoint" "$foundryOpenAIEndpoint");
foundryEmbeddingCapacityResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-embedding-capacity" "$embeddingCapacity");
foundryProjectEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-project-endpoint" "$foundryProjectEndpoint");
foundryInferenceEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-inference-endpoint" "$foundryInferenceEndpoint");
foundryDocumentIntelligenceEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-document-intelligence-endpoint" "$foundryDocumentIntelligenceEndpoint");

#ensure app insights secrets
appInsightsConnectionStringResult=$(ensure_key_vault_secret "$keyVaultName" "app-insights-connection-string" "$appInsightsConnectionString");
appInsightsInstramentationKeyResult=$(ensure_key_vault_secret "$keyVaultName" "app-insights-instramentation-key" "$appInsightsInstramentationKey");

#ensure search secrets
searchQueryKeyResult=$(ensure_key_vault_secret "$keyVaultName" "search-query-key" "$searchQueryKey");
searchAdminKeyResult=$(ensure_key_vault_secret "$keyVaultName" "search-admin-key" "$searchAdminKey");
searchAPIURLResult=$(ensure_key_vault_secret "$keyVaultName" "search-api-url"  "https://$searchName.search.windows.net");

#return
echo "Completed $resourceGroupName infrastructure deployment.";
