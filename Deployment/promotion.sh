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

#get api access token and ensure the source app has access to the destination foundry instance (no provisioning)
accessToken=$(acquire_access_token "$authAppId");
sourceFoundryPortalResult=$(ensure_foundry_project "$sourceResourceGroupName" "" "$sourceFoundryName" "" "" "$authAppId");

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
destinationFoundryProjectEndpoints="";
sourceFoundryProjects=$(az cognitiveservices account project list --resource-group $sourceResourceGroupName --name $sourceFoundryName);

#migrate projects
while IFS= read -r item; do
  #get each destination project
  destinationFoundryProjectName=$(echo "$item" | jq -r '.properties.displayName');
  sourceFoundryProjectEndpoint=$(echo "$item" | jq -r '.properties.endpoints."AI Foundry API"');
  echo "===Starting Foundry project $destinationFoundryProjectName migration from $sourceFoundryName$sourceFoundryName.===";

  #ensure destination project (without auth, as that will inherit from the parent Foundry resource)
  destinationFoundryProjectResult=$(ensure_foundry_project "$destinationResourceGroupName" "$destinationRegion" "$destinationFoundryName" "$destinationFoundryProjectName" "$destinationFoundrySKU" "");

  #parse desination project components
  destinationFoundryComponents=(${destinationFoundryProjectResult//|/ });
  destinationFoundryProjectEndpoint=${destinationFoundryComponents[3]};
  
  #build migration payload and call API
  payload='{"forceChanges":'$forceChanges',"sourceResourceGroupName":"'"$sourceResourceGroupName"'","sourceProjectEndpoint":"'"$sourceFoundryProjectEndpoint"'","destinationResourceGroupName":"'"$destinationResourceGroupName"'","destinationProjectEndpoint":"'"$destinationFoundryProjectEndpoint"'","destinationKeyVaultURL":"'"$destinationKeyVaultURL"'"}';
  response=$(post_to_api "$apiEndpoint" "$payload" "$accessToken");
  
  #collect destination project endpoints
  echo "$destinationFoundryProjectName migration result: $response";
  destinationFoundryProjectEndpoints="$destinationFoundryProjectEndpoints$destinationFoundryProjectEndpoint,";
done < <(echo "$sourceFoundryProjects" | jq -c '.[]');

#check  destination foundry project endpoints
if [ ! -z "$destinationKeyVaultName" ] && [ ! -z "$destinationFoundryProjectEndpoints" ]; then
  #update destination foundry project endpoints in key vault
  destinationFoundryProjectEndpoints="${destinationFoundryProjectEndpoints%,}";
  destinationFoundryProjectEndpointsResult=$(ensure_key_vault_secret "$destinationKeyVaultName" "foundry-project-endpoint" "$destinationFoundryProjectEndpoints");
fi

#return
echo "Completed Foundry $destinationFoundryName agent promotion successfully.";
