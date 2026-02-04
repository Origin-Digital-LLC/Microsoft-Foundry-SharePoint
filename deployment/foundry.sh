#!/bin/bash

#initialization
environmentNormalized="${1,,}";
resourceGroupName="$2";
region="$3";
appId="$4";

#foundry settings
foundrySKU="$5";
foundryPortalName="$6";
foundryProjectName="$7";

#model deployment settings
llmModel="$8";
llmCapacity="$9";
llmVersion="${10}";

#apply naming conventions
resourceGroupName="${resourceGroupName,,}-$environmentNormalized";
foundryProjectName="$resourceGroupName-${foundryProjectName,,}";
foundryPortalName="$resourceGroupName-${foundryPortalName,,}";

#reference utilities
source ./deployment/utilities.sh;
echo "Starting Foundry $foundryPortalName infrastructure deployment.";

#ensure foundry
resourceGroupResult=$(ensure_resource_group "$resourceGroupName" "$region");
foundryResult=$(ensure_foundry "$resourceGroupName" "$region" "$foundryPortalName" "$foundryProjectName" "$foundrySKU" "$appId");

#ensure LLM model deployment
llmModelResult=$(ensure_foundry_model_deployment "$resourceGroupName" "$foundryPortalName" "$llmModel" "$llmVersion" "$llmCapacity");

#parse foundry components
foundryComponents=(${foundryResult//|/ });
foundryAccountKey=${foundryComponents[0]};
foundryResourceId=${foundryComponents[4]};
foundryOpenAIEndpoint=${foundryComponents[1]};
foundryProjectEndpoint=${foundryComponents[3]};
foundryInferenceEndpoint=${foundryComponents[5]};
foundryDocumentIntelligenceEndpoint=${foundryComponents[2]};

#SAMPLE NEXT STEP: grant azure ai search RBAC access to foundry for search-based tools
#searchPrincipalId=$(az search service list --resource-group "$resourceGroupName" --query "[0].id" --output "tsv");
#searchFoundryRBACCognitiveServicesUser=$(ensure_rbac_access "$searchPrincipalId" "$foundryResourceId" "Cognitive Services User");

#SAMPLE NEXT STEP: ensure foundry key vault secrets
#keyVaultName=$(az keyvault list --resource-group "$resourceGroupName" --query "[0].name" --output "tsv");
#foundryAccountKeyResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-account-key" "$foundryAccountKey");
#foundryOpenAIEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-open-ai-endpoint" "$foundryOpenAIEndpoint");
#foundryProjectEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-project-endpoint" "$foundryProjectEndpoint");
#foundryInferenceEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-inference-endpoint" "$foundryInferenceEndpoint");
#foundryDocumentIntelligenceEndpointResult=$(ensure_key_vault_secret "$keyVaultName" "foundry-document-intelligence-endpoint" "$foundryDocumentIntelligenceEndpoint");

#return
echo "Completed $foundryPortalName Foundry deployment.";
