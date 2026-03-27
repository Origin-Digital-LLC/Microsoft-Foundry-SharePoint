#!/bin/bash

#initialization
authAppId="$1";
apiEndpoint="$2";
forceChanges="$3";
destinationRegion="$4";
sourceFoundryName="$5";
destinationFoundrySKU="$6";
destinationFoundryName="$7";
sourceResourceGroupName="$8";
destinationKeyVaultName="$9";
destinationResourceGroupName="${10}";
sourceFoundryName="$sourceResourceGroupName-$sourceFoundryName";
destinationFoundryName="$destinationResourceGroupName-$destinationFoundryName";

#reference utilities
source ./deployment/utilities.sh;
echo "Starting Foundry agent promotion from $sourceFoundryName to $destinationFoundryName.";

#get api access token
apiScope="api://$authAppId/.default";
echo "Acquiring access token for $apiScope.";
accessToken=$(az account get-access-token --scope $apiScope --query "accessToken" --output "tsv");

#ensure destination foundry instance (without projects)
authEnterpriseAppObjectId=$(get_app_registration_enterprise_object_id "$authAppId");
resourceGroupResult=$(ensure_resource_group "$destinationResourceGroupName" "$destinationRegion");
destinationFoundryPortalResult=$(ensure_foundry_project "$destinationResourceGroupName" "$destinationRegion" "$destinationFoundryName" "" "$destinationFoundrySKU" "$authAppId");

#check key vault name
destinationKeyVaultURL="";
if [ -z "$destinationKeyVaultName" ]; then
  #key vault not specified
  echo "Destination Key Vault not provided.";
else
  #get key vault
  destinationKeyVaultName="$destinationResourceGroupName-$destinationKeyVaultName";
  keyVault=$(az keyvault list --resource-group $destinationResourceGroupName --query "[?name == '$destinationKeyVaultName']" --output "tsv");

  #check key vault
  if [ -z "$keyVault" ]; then
    #key vault not found
    echo "Key Vault $destinationKeyVaultName not found.";
  else
    #get key vault url
    destinationKeyVaultURL=$(az keyvault show --resource-group $destinationResourceGroupName --name $destinationKeyVaultName --query "properties.vaultUri" --output "tsv");
  fi
fi

#get sources foundry instance's projects
sourceFoundryProjects=$(az cognitiveservices account project list --resource-group $sourceResourceGroupName --name $sourceFoundryName);
while IFS= read -r item; do
  #get each destination project
  destinationFoundryProjectName=$(echo "$item" | jq -r '.properties.displayName');
  sourceFoundryProjectEndpoint=$(echo "$item" | jq -r '.properties.endpoints."AI Foundry API"');
  echo "===Starting Foundry project $destinationFoundryProjectName migration from $sourceFoundryName$sourceFoundryName.===";

  #ensure destination project (without auth, as that will inherit from the parent Foundry resource)
  destinationFoundryProjectResult=$(ensure_foundry_project "$destinationResourceGroupName" "$destinationRegion" "$destinationFoundryName" "$destinationFoundryProjectName" "$destinationFoundrySKU" "");

  #parse foundry components
  destinationFoundryComponents=(${destinationFoundryProjectResult//|/ });
  destinationFoundryProjectEndpoint=${destinationFoundryComponents[3]};
  
  #build migration payload
  echo "Migrating to $destinationFoundryProjectName via $apiEndpoint.";
  payload='{"forceChanges":'$forceChanges',"sourceResourceGroupName":"'"$sourceResourceGroupName"'","sourceProjectEndpoint":"'"$sourceFoundryProjectEndpoint"'","destinationResourceGroupName":"'"$sourceResourceGroupName"'","destinationProjectEndpoint":"'"$destinationFoundryProjectEndpoint"'","destinationKeyVaultURL":"'"$destinationKeyVaultURL"'"}';

  #call API
  response=$(curl -s -X POST "$apiEndpoint" -H "Content-Type: application/json" -H "Accept: application/json" -H "Authorization: Bearer $accessToken" -d "$payload" -w " (API response code: %{http_code})");
  response="$response" | jq '.';

  #print result
  echo "$destinationFoundryProjectName migration result: $response";
done < <(echo "$sourceFoundryProjects" | jq -c '.[]');

#return
echo "Completed Foundry $destinationFoundryName agent promotion successfully.";
