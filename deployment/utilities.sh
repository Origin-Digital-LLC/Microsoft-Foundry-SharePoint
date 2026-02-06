#!/bin/bash

#Installs an extension if its not already available.
function add_extension()
{
	#initialization
	local name=$1;
	local extension=$(az extension list --query "[?name == '$name']" --output "tsv");

 	#check extension
	if [ -z "$extension" ]; then
		#install extension
		echo "Installing $name extension." >&2;
		extension=$(az extension add --name $name --upgrade --only-show-errors);

		#return
		echo "Installed $name extension successfully." >&2;
	else
		#extension already installed
		echo "$name extension is already installed." >&2;
	fi
}

#Gets the first tenant id for the current user. [Returns: tenantId]
function get_tenant_id()
{
	#initialization
 	echo "Getting tenant id." >&2;

  	#return
   	local tenantId=$(az account list --query "[0].tenantId" --output "tsv");
	echo "$tenantId";
}

#Gets the first subscription id for the current user. [Returns: subscriptionId]
function get_subscription_id()
{
	#initialization
 	echo "Getting subscription id." >&2;

  	#return
   	local subscriptionId=$(az account list --query "[0].id" --output "tsv");
	echo "$subscriptionId";
}

#Creates a resourec group by name if it doesn't already exist. [Returns: nothing]
function ensure_resource_group()
{
	#initialization
 	local name=$1;
	local region=$2;

  	#check existing
    echo "Checking resource group $name." >&2;
	local resourceGroupExists=$(az group exists --resource-group $name);
	if [ "$resourceGroupExists" != "true" ]; then
		#create resource group
		echo "Creating resource group $name in $region." >&2;
		local resourceGroupResult=$(az group create --name $name --location $region);
	
		#wait for provisioning to complete
		local resourceGroupWait=$(az group wait --resource-group $name --created);
		echo "Resource group $name created successfully." >&2;
	else
		#resource group already exists
		echo "Resource group $name already exists." >&2;
	fi
}

#Creates a key vault instance by name if it doesn't already exist and optionally ensures access policies for a user and an admin. [Returns: nothing]
function ensure_key_vault()
{
	#initialization
   	local sku=$4;
	local name=$3;  	
 	local region=$2;
  	local ownerEmail=$7;
  	local authAppClientId=$6;
 	local resourceGroupName=$1;
  	local deploymentAppClientId=$5;

  	#check existing
   	echo "Ensuring key vault $name." >&2;
	local keyVault=$(az keyvault list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$keyVault" ]; then
		#create key vault
		echo "Creating key vault $name." >&2;
		keyVault=$(az keyvault create --resource-group $resourceGroupName --name $name --location $region --sku $sku --enabled-for-template-deployment "true" --enable-rbac-authorization "false");

		#done
  		echo "Created key vault $name successfully." >&2;
	else
		#return
		echo "Key vault $name already exists." >&2;
	fi	

 	#ensure "admin" acccess policy for the deployment app
  	if [ -z "$deploymentAppClientId" ]; then
   		#skip deployment app access policy
   		echo "No deployment app admin access policy was requested." >&2;
   	else
		#ensure deployment app access policy    		
		local deploymentAccessPolicyResult=$(ensure_key_vault_admin_access_policy "$name" "$deploymentAppClientId");
	fi

 	#ensure "admin" acccess policy for the owner user
  	if [ -z "$ownerEmail" ]; then
   		#skip owner user access policy
   		echo "No owner user admin access policy was requested." >&2;
   	else
		#ensure owner user access policy
		local ownerObjectId=$(get_user_object_id $ownerEmail);
		local ownerAccessPolicyResult=$(ensure_key_vault_admin_access_policy "$name" "$ownerObjectId");
	fi

 	#ensure "user" acccess policy for the auth app
  	if [ -z "$authAppClientId" ]; then
   		#skip auth app access policy
   		echo "No auth app user access policy was requested." >&2;
   	else
		#ensure auth app access policy    		
		local authAccessPolicyResult=$(ensure_key_vault_user_access_policy "$name" "$authAppClientId");
	fi     
}

#Creates a key vault access policy for an admin (full permissions). [Returns: nothing]
function ensure_key_vault_admin_access_policy()
{
	#initialization
 	local name=$1;
 	local enterpriseAppObjectId=$2;
	echo "Granting admin $enterpriseAppObjectId access to key vault $name." >&2;

	#don't use quotes in the permission parameters
	local result=$(az keyvault set-policy --name $name --object-id $enterpriseAppObjectId --secret-permissions all --certificate-permissions all --key-permissions all --storage-permissions all);

 	#return
	echo "Granted admin $enterpriseAppObjectId access to key vault $name successfully." >&2;
}

#Creates a key vault access policy for a user (list and read secrets only). [Returns: nothing]
function ensure_key_vault_user_access_policy()
{
	#initialization
 	local name=$1;
 	local enterpriseAppObjectId=$2;
	echo "Granting principal $enterpriseAppObjectId access to key vault $name." >&2;

	#don't use quotes in the permission parameters
	local result=$(az keyvault set-policy --name $name --object-id $enterpriseAppObjectId --secret-permissions get list);

 	#return
	echo "Granted user $enterpriseAppObjectId access to key vault $name successfully." >&2;
}

#Creates or updates (only if the value has changed) a key vault secret. [Returns: nothing]
function ensure_key_vault_secret()
{
	#initialization
	local name=$1;
  	local value=$3;
   	local secret=$2;
	local contentType=$4;
     
 	#check value
	if [ -z "$value" ]; then
		#return
		echo "Skipping key vault $name secret $secret since no value was given." >&2;
	else		
		#check existing
		secretResult=$(az keyvault secret list --vault-name $name --query "[?name == '$secret']" --output "tsv");
		if [ -z "$secretResult" ]; then
			#set key vault secret
			secretResult=$(az keyvault secret set --vault-name $name --name $secret --value $value);
			echo "Set key vault $name secret $secret successfully." >&2;
		else
			#check if key vault secret has changed   
			currentValue=$(az keyvault secret show --vault-name $name --name $secret --query "value" --output "tsv" --only-show-errors);
			if [ "$currentValue" != "$value" ]; then
				#set key vault secret
				secretResult=$(az keyvault secret set --vault-name $name --name $secret --value $value);
				echo "Updated key vault $name secret $secret successfully." >&2;
			else
				#key vault secret is unchanged
				echo "Not updating key vault $name secret $secret because its value has not changed." >&2;
			fi
		fi

		#check content type
		if [ ! -z "$contentType" ]; then
			#set content type
			$secretResult=$(az keyvault secret set-attributes --vault-name $name --name $secret --content-type $contentType);
			echo "Set key vault $name secret $secret content type to $contentType." >&2;
		fi
	fi
}

#Creates an application insights instance by name if it doesn't already exist. [Returns: connectionString|instramentationKey]
function ensure_app_insights()
{
	#initialization
	local name=$3;
 	local region=$2;
 	local resourceGroupName=$1;
 
 	#check existing
	echo "Ensuring app insights $name." >&2;
	local appInsights=$(az monitor app-insights component show --resource-group $resourceGroupName --app $name --output "tsv");
   	if [ -z "$appInsights" ]; then
		#create
		echo "Creating $name." >&2;
		appInsights=$(az monitor app-insights component create --resource-group $resourceGroupName --app $name --location $region);
		echo "Created $name successfully." >&2;
  	else
		#already exists
		echo "$name already exists." >&2;
	fi

	#get connection string
	local connectionString=$(az monitor app-insights component show --resource-group $resourceGroupName --app $name --query "connectionString" --output "tsv");
	
	#parse out instramentation key
 	local splitOnEquals=(${connectionString//=/ });
 	local partialConnectionString=${splitOnEquals[1]};
  	local splitOnSemicolon=(${partialConnectionString//;/ });

   	#return    	
   	local instramentationKey=${splitOnSemicolon[0]};
	echo "$connectionString|$instramentationKey";
}

#Creates a storage account instance by name if it doesn't already exist. [Returns: connectionString|accessKey|resourceId]
function ensure_storage_account()
{
	#initialization
   	local sku=$4;
	local name=$3;
  	local kind=$5;
 	local region=$2;
  	local appInsightsName=$6;
 	local resourceGroupName=$1;

  	#check existing
   	echo "Ensuring storage account $name." >&2;
	local storageAccount=$(az storage account list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$storageAccount" ]; then
		#create storage account
		echo "Creating storage account $name." >&2;
		storageAccount=$(az storage account create --resource-group $resourceGroupName --location $region --name $name --sku $storageSKU --kind $storageKind --min-tls-version "TLS1_2" --allow-blob-public-access "true");
	
		#link storage account to app insights
		local outputSupression=$(az monitor app-insights component linked-storage link --resource-group $resourceGroupName --app $appInsightsName --storage-account $name);
		echo "Created storage account $name successfully." >&2;
	else
		#storage account already exists
		echo "Storage account $name already exists." >&2;
	fi
	
	#get storage account metadata
	local resourceId=$(az storage account show --resource-group $resourceGroupName --name $name --query "id" --output "tsv");
	local connectionString=$(az storage account show-connection-string --resource-group $resourceGroupName --name $name --output "tsv");
	local accessKey=$(az storage account keys list --resource-group $resourceGroupName --account-name $name --query "[0].value" --output "tsv");

 	#return
  	echo "$connectionString|$accessKey|$resourceId";
}

#Creates a storage account container by name if it doesn't already exist. [Returns: nothing]
function ensure_storage_account_container()
{
	#initialization
	local name=$3;
 	local publicAccess=$4;
 	local storageAccountKey=$2;
 	local storageAccountName=$1;

  	#check existing
   	echo "Ensuring storage account container $name in $storageAccountName." >&2;
	local containerExists=$(az storage container exists --account-name $storageAccountName --account-key $storageAccountKey --name $name --output "tsv");
	if [ "$containerExists"="False" ]; then
		#create container
		echo "Creating storage account container $name in $storageAccountName." >&2;
		local container=$(az storage container create --account-name $storageAccountName --account-key $storageAccountKey --name $name --public-access $publicAccess);

		#return
		echo "Created storage account container $name in $storageAccountName successfully." >&2;
	else
		#container already exists
		echo "Storage account container $name already exists in $storageAccountName." >&2;
	fi
}

#Clears a storage account's blob CORS rules. [Returns: nothing]
function clear_storage_account_blob_cors_rules()
{
	#initialization
 	local storageAccountKey=$2;
 	local storageAccountName=$1;

  	#clear CORS rules
   	echo "Clearing storage account $storageAccountName blob CORS." >&2;
   	local clearCORS=$(az storage cors clear --account-name $storageAccountName --account-key $storageAccountKey --services "b");

	#return
	echo "Cleared storage account $storageAccountName blob CORS successfully." >&2;
}

#Adds a blob CORS rule to a storage account. [Returns: nothing]
function add_storage_account_blob_cors_rule()
{
	#initialization
 	local origins=$3;
 	local storageAccountKey=$2;
 	local storageAccountName=$1;

	#add CORS rule
 	echo "Adding storage account $storageAccountName blob CORS rule to $origins." >&2;
 	local cors=$(az storage cors add --account-name $storageAccountName --account-key $storageAccountKey --origins "$origins" --max-age "3600" --allowed-headers "*" --exposed-headers "*" --methods "OPTIONS" "GET" "HEAD" "PUT" --services "b");
  
  	#return
   	echo "Added storage account $storageAccountName blob CORS rule to $origins successfully." >&2;
}

#Enables blob soft delete for a storage account. [Returns: nothing]
function enable_storage_account_blob_soft_delete()
{
	#initialization
	local resourceGroupName=$1;
 	local storageAccountName=$2;

	#return
	local blobServices=$(az storage account blob-service-properties update --resource-group $resourceGroupName --account-name $storageAccountName --enable-delete-retention "true"  --delete-retention-days "7");
	echo "Enabled blob soft delete for $storageAccountName successfully." >&2;
}

#Ensures a storage account has a static website configured. [Returns: url]
function ensure_storage_account_static_website()
{
	#initialization
	local resourceGroupName=$1;
	local storageAccountKey=$3;
 	local storageAccountName=$2;
	
	#ensure storage static website
	staticWebsite=$(az storage blob service-properties show --account-name $storageAccountName --account-key $storageAccountKey --query "staticWebsite.indexDocument");
	if [ -z "$staticWebsite" ]; then
		#create storage static website
		echo "Creating storage static website $storageAccountName." >&2;
		staticWebsite=$(az storage blob service-properties update --account-name $storageAccountName --account-key $storageAccountKey --static-website "true" --index-document "index.html" --404-document "error.html");
		
		#storage static website created
		echo "Created storage static website $storageAccountName successfully." >&2;
	else
		#storage static website already exists
		echo "Storage static website $storageAccountName already exists." >&2;
	fi

	#return
	local url=$(az storage account show --resource-group $resourceGroupName --name $storageAccountName --query "primaryEndpoints.web" --output "tsv");
	url="${url,,}";
	url="${url%/}";
	echo "$url";
}

#Creates a microsoft foundry instance by name if it doesn't already exist. [Returns: accountKey|openAIEndpoint|documentIntelligenceEndpoint|projectEndpoint|resourceId|inferenceEndpoint]
function ensure_foundry()
{
	#initialization
   	local sku=$5;
	local name=$3;  	
 	local region=$2;
	local project=$4;
  	local principalId=$6;
 	local resourceGroupName=$1; 
	local projectURL="/projects/$project";
	local aiUserRoleId="53ca6127-db72-4b80-b1b0-d745d6d5456d";
   	local aiDeveloperRoleId="64702f94-c441-49e6-a78b-ef80e0188fee";
   	local contributorRoleId="b24988ac-6180-42a0-ab88-20f7382dd24c";
   	local cognitiveServicesUserRoleId="a97b65f3-24c7-4388-baec-2e87135dc908";
	local cognitiveServicesContributorRoleId="25fbc0a9-bd7c-42a3-aa1a-3b75d497ee68";

  	#check existing
   	echo "Ensuring foundry $name." >&2;
	local foundry=$(az cognitiveservices account list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$foundry" ]; then
		#create foundry
		echo "Creating foundry $name." >&2;
  		local foundryId=$(az cognitiveservices account create --resource-group $resourceGroupName --name $name --location $region --custom-domain $name --kind "AIServices" --sku $sku --query "id" --output "tsv" --assign-identity --yes);

   		#update foundry to allow project creation (not currently supported by azure cli)
  		local queryString="?api-version=2025-06-01";
	 	local contentType="Content-Type=application/json";
  		echo "Enabling foundry $foundryId project creation."  >&2;
		local managmentURL="https://management.azure.com$foundryId";
   		foundry=$(az rest --method PATCH --uri "$managmentURL$queryString" --headers $contentType --body "{'properties': {'allowProjectManagement': true, }}");

  		#wait for update to finish (check every 10 seconds for up to 1 minute)
		local projectResult=$(wait_for_az_rest_command "$foundryId" "10" "60");			
	  	if [ "$projectResult" != "0" ]; then
	    	echo "Foundry $foundryId project enablement failed." >&2;
	    	exit;
	  	fi
	
	 	#create a default project
	   	echo "Foundry updated successfully. Creating default project." >&2;
		local project=$(az rest --method PUT --uri "$managmentURL$projectURL$queryString" --headers $contentType --body "{'location': '$region', 'properties': {'description': 'This project holds your $project agents.', 'displayName': '$project'}, 'identity': {'type': 'SystemAssigned'}}");
	 	echo "Default foundry project created successfully." >&2;    
	else
		#foundry already exists
		echo "Foundry $name already exists." >&2;
	fi

 	#assign foundry permissions to the given principal (if provided)
  	if [ -z "$principalId" ]; then
   		#no principal provided
		echo "No principal id was provided to receive foundry roles." >&2;
   	else
		#get foundry's scope (id)
		echo "Granting principal $principalId foundry roles." >&2;
		local scope=$(az cognitiveservices account show --resource-group $resourceGroupName --name $name --query "id" --output "tsv");

		#add the principal to the roles
		$(ensure_rbac_access "$principalId" "$scope" "$aiUserRoleId");
		$(ensure_rbac_access "$principalId" "$scope" "$aiDeveloperRoleId");
		$(ensure_rbac_access "$principalId" "$scope" "$contributorRoleId");
		$(ensure_rbac_access "$principalId" "$scope" "$cognitiveServicesUserRoleId");
		$(ensure_rbac_access "$principalId" "$scope" "$cognitiveServicesContributorRoleId");
		echo "Granted principal $principalId foundry roles successfully." >&2;
	fi
  	
 	#get foundry metadata
	local projectAPI="api$projectURL";
	local resourceId=$(az cognitiveservices account show --resource-group $resourceGroupName --name $name --query "id" --output "tsv");
  	local accountKey=$(az cognitiveservices account keys list --resource-group $resourceGroupName --name $name --query "key1" --output "tsv");
	local projectEndpoint=$(az cognitiveservices account show --resource-group $resourceGroupName --name $name --query 'properties.endpoints."AI Foundry API"' --output "tsv");
	local documentIntelligenceEndpoint=$(az cognitiveservices account show --resource-group $resourceGroupName --name $name --query "properties.endpoints.FormRecognizer" --output "tsv");
	local openAIEndpoint=$(az cognitiveservices account show --resource-group $resourceGroupName --name $name --query 'properties.endpoints."OpenAI Language Model Instance API"' --output "tsv");
	local inferenceEndpoint="$(az cognitiveservices account show --resource-group $resourceGroupName --name $name --query 'properties.endpoints."Azure AI Model Inference API"' --output "tsv")models";

	#return
	projectEndpoint="$projectEndpoint$projectAPI";
  	echo "$accountKey|$openAIEndpoint|$documentIntelligenceEndpoint|$projectEndpoint|$resourceId|$inferenceEndpoint";
}

#Deploys a microsoft foundry model by name if it doesn't already exist. [Returns: nothing]
function ensure_foundry_model_deployment()
{
	#initialization
 	local name=$2;
	local model=$3;	
   	local version=$4;
   	local capacity=$5;
 	local resourceGroupName=$1;

  	#check existing
   	echo "Ensuring Foundry $name model deployment $model." >&2;
	local deployment=$(az cognitiveservices account deployment list --resource-group $resourceGroupName --name $name --query "[?name == '$model']" --output "tsv");
	if [ -z "$deployment" ]; then
		#deploy model
		echo "Deploying $model to Foundry $name." >&2;
		deployment=$(az cognitiveservices account deployment create --resource-group $resourceGroupName --name $name --deployment-name $model --model-name $model --model-version $version --sku-capacity $capacity --model-format "OpenAI" --sku-name "GlobalStandard");
	
		#done
		echo "Model $model deployed to Foundry $name successfully." >&2;
	else
		#return
		echo "Model $model has already been deployed to Foundry $name." >&2;
	fi
}

#Creates an azure container registry, environment, and instance by name if they don't already exist. [Returns: username|password|serverName|backendAppId|backendAppURL|frontendAppID|frontendAppURL|backendAppServicePrincipalId|frontendAppServicePrincipalId]
function ensure_acr()
{
	#initialization
	local sku=$5;
 	local name=$3;
	local port=$6;
   	local region=$2;       
	local imageName=$7;
	local minReplicas=$4;
	local keyVaultName=$8;
	local resourceGroupName=$1;
  	local sharedResourceGroupName=${10};
	local appInsightsConnectionString=$9;
 	local environmentName="$resourceGroupName-acr-env01";

	#build server and image names
  	local serverName="$name.azurecr.io"; 
	local backendImage="$serverName/$imageName-backend";
 	local frontendImage="$serverName/$imageName-frontend";
 	local backendAppName="$resourceGroupName-acr-backend";
 	local frontendAppName="$resourceGroupName-acr-frontend";

	#check existing acr
 	echo "Ensuring acr $name." >&2;
	local acr=$(az acr list --resource-group $sharedResourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$acr" ]; then
		#create acr
		echo "Creating acr $name." >&2;
		acr=$(az acr create --resource-group $sharedResourceGroupName --location $region --name $name --sku $sku --admin-enabled);

  		#authenticate to acr
		echo "Authenticating to acr $name." >&2;
		local acrAuth=$(az acr login --name $name --expose-token);
			
	 	#build backend container
		echo "Building container $backendImage for $name." >&2;
		local backendBuild=$(az acr build --resource-group $sharedResourceGroupName --registry $name --image $backendImage ./backend);
	
		#build frontend  container
		echo "Building container $frontendImage for $name." >&2;
		local frontendBuild=$(az acr build --resource-group $sharedResourceGroupName --registry $name --image $frontendImage ./frontend);
	else 
		#azure container registry already exists
		echo "ACR $name already exists." >&2;
	fi 	

 	#authenticate to acr
	echo "Authenticating to acr $name." >&2;
	local auth=$(az acr login --name $name --expose-token);
	local un=$(az acr credential show --name $name --query "username" --output "tsv");
	local pw=$(az acr credential show --name $name --query "passwords[0].value" --output "tsv");

	#check existing environment
 	local env=$(az containerapp env list --resource-group $resourceGroupName --query "[?name == '$environmentName']" --output "tsv");
  	if [ -z "$env" ]; then
		#create environment
		echo "Creating acr environment $environmentName." >&2;
		local env=$(az containerapp env create --resource-group $resourceGroupName --name $environmentName --location $region --logs-destination "azure-monitor");
		local telemetry=$(az containerapp env telemetry app-insights set --resource-group $resourceGroupName --name $environmentName --connection-string $appInsightsConnectionString --enable-open-telemetry-traces "true" --enable-open-telemetry-logs "true");
 
		#create backend container app
		echo "Creating acr app $backendAppName." >&2;
		local backendApp=$(az containerapp create --resource-group $resourceGroupName --name $backendAppName --image $backendImage --environment $environmentName --min-replicas $minReplicas --target-port $port --registry-server $serverName --registry-username $un --registry-password $pw --env-vars "KV_URL=https://$keyVaultName.vault.azure.net" --ingress "external");

		#create frontend container app
		echo "Creating acr app $frontendAppName." >&2;
		local frontendApp=$(az containerapp create --resource-group $resourceGroupName --name $frontendAppName --image $frontendImage --environment $environmentName --min-replicas $minReplicas --target-port $port --registry-server $serverName --registry-username $un --registry-password $pw --env-vars "KV_URL=https://$keyVaultName.vault.azure.net" --ingress "external");
 	else 
		#environment already exists
		echo "ACR environment $environmentName already exists." >&2;
	fi
 
	#ensure backend app system identity
	echo "Creating acr app $backendAppName managed identity." >&2;
	local backendAppServicePrincipalId=$(az containerapp identity assign --resource-group $resourceGroupName --name $backendAppName --system-assigned --query "principalId" --output "tsv");

 	#ensure frontend app system identity
	echo "Creating acr app $frontendAppName managed identity." >&2;
	local frontendAppServicePrincipalId=$(az containerapp identity assign --resource-group $resourceGroupName --name $frontendAppName --system-assigned --query "principalId" --output "tsv");

	#grant backend app identity key vault access
 	local backendAuthAccessPolicyResult=$(ensure_key_vault_user_access_policy "$keyVaultName" "$backendAppServicePrincipalId");
	echo "Created acr app $backendAppName managed identity $backendAppServicePrincipalId successfully with access to key vault $keyVaultName." >&2;

 	#grant frontend  app identity key vault access
 	local frontendAuthAccessPolicyResult=$(ensure_key_vault_user_access_policy "$keyVaultName" "$frontendAppServicePrincipalId");
	echo "Created acr app $frontendAppName managed identity $frontendAppServicePrincipalId successfully with access to key vault $keyVaultName." >&2;
 	
	#get container app metadata
 	local username=$(az acr credential show --name $name --query "username" --output "tsv");
	local password=$(az acr credential show --name $name --query "passwords[0].value" --output "tsv");
	local backendAppId=$(az containerapp show --resource-group $resourceGroupName --name $backendAppName --query "id" --output "tsv");
	local frontendAppId=$(az containerapp show --resource-group $resourceGroupName --name $frontendAppName --query "id" --output "tsv");
	local backendAppURL=$(az containerapp show --resource-group $resourceGroupName --name $backendAppName --query "properties.configuration.ingress.fqdn" --output "tsv");
	local frontendAppURL=$(az containerapp show --resource-group $resourceGroupName --name $frontendAppName --query "properties.configuration.ingress.fqdn" --output "tsv");

 	#return
  	echo "$username|$password|$serverName|$backendAppId|$backendAppURL|$frontendAppId|$frontendAppURL|$backendAppServicePrincipalId|$frontendAppServicePrincipalId";
}

#Creates a Cosmos DB Postgres server and database if they don't already exists. [Returns: dbHost|dbPassword]
function ensure_postgres_cluster()
{
	#initialization
 	local name=$3;
	local vCores=$5;
   	local region=$2;
	local counter=1;
 	local dbPassword="";
	local rootPassword=$4;
	local resourceGroupName=$1;
   
	#check existing cluster
 	echo "Ensuring Postgres cluster $name." >&2;
	local cluster=$(az cosmosdb postgres cluster list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$cluster" ]; then
 		#create cluster
  		dbPassword="$rootPassword$(date +%s)";
		echo "Creating Postgres cluster $name." >&2;
  		local cluster=$(az cosmosdb postgres cluster create --resource-group $resourceGroupName --name $name --location $region --enable-ha "false" --coordinator-v-cores $vCores --coordinator-server-edition "BurstableMemoryOptimized" --coord-public-ip-access "true" --coordinator-storage 32768 --enable-shards-on-coordinator "true" --node-count 0 --node-storage 524288 --node-server-edition "MemoryOptimized" --node-enable-public-ip-access "true" --postgresql-version "16" --citus-version "12.1" --administrator-login-password "$dbPassword");

		#allow azure services
		echo "Configuring Postgres cluster $name firewall." >&2;
		local azureFirewallRule=$(az cosmosdb postgres firewall-rule create --resource-group $resourceGroupName --cluster-name $name --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0");
	else 
		#cluster already exists
		echo "Postgres cluster $name already exists." >&2;
 	fi
  
  	#return
   	local dbHost=$(az cosmosdb postgres cluster show --resource-group $resourceGroupName --name $name --query "serverNames[0].fullyQualifiedDomainName" --output "tsv");
	echo "$dbHost|$dbPassword";
}

#Gets a user's object by email. [Returns: objectId]
function get_user_object_id()
{	
	#initialization
 	local email=$1;
	echo "Getting user object id via email $email." >&2;

	#return
	local objectId=$(az ad user show --id $email --query "id" --output "tsv" --only-show-errors);
 	echo "Got an object id of $objectId for $email." >&2;
 	echo "$objectId";
}

#Gets the enterprise app object id from an app registration's app id. [Returns: objectId]
function get_app_registration_enterprise_object_id()
{
	#initialization
 	local appId=$1;
  	echo "Getting enterprise application object id from app registration $appId" >&2;

   	#return
	objectId=$(az ad sp show --id $appId --query "id" --output "tsv" --only-show-errors);
 	echo "$objectId";
}

#Creates an Entra Id app registration by name if it doesn't already exist. [Returns: clientId|clientSecret]
function ensure_entra_id_app_registration()
{
	#initialization
 	local name=$1;
 	local ownerEmail=$3;
   	local redirectURLs=$2;
	local clientSecret="";
 
	#check existing app
	echo "Ensuring Entra Id app $name." >&2;
	local objectId=$(az ad app list --query "[?displayName == '$name'].id" --output "tsv" --only-show-errors);
 	if [ -z "$objectId" ]; then
  		#create app
		local appId="";
  		local clientSecretEndDate=$(date -d "+2 years" +"%Y-%m-%d");
		echo "Creating Entra Id app $name with secret expiration date $clientSecretEndDate." >&2;
		appId=$(az ad app create --display-name $name --query "appId" --output "tsv" --only-show-errors);	

  		#create service principal
		echo "Created Entra Id app $name successfully." >&2;
		echo "Creating SPN for Entra Id app $name ($appId)." >&2;
		local appSPNObjectId=$(az ad sp create --id $appId --query "id" --output "tsv" --only-show-errors);
  		objectId=$(az ad app list --query "[?displayName == '$name'].id" --output "tsv" --only-show-errors);
  
  		#create client secret
		clientSecret=$(az ad app credential reset --id $appId --display-name "Client Secret" --end-date $clientSecretEndDate --query "password" --output "tsv" --only-show-errors);
		echo "Created SPN ($appSPNObjectId) for Entra Id app $name successfully." >&2;
 	else 
		#app already exists
		echo "Entra Id app $name ($objectId) already exists." >&2;
 	fi

	#check redirect URLs
   	local clientId=$(az ad app show --id $objectId --query "appId" --output "tsv" --only-show-errors);
	if [ -z "$redirectURLs" ]; then
		echo "Not setting redirect URLs for app $clientId.";
	else
		redirectURLsResult=$(az ad app update --id $clientId --set "spa={'redirectUris':[$redirectURLs]}");
	fi

	#configure claims
 	echo "Configuring claims for Entra Id app $name ($objectId)." >&2;
  	local claims=$(az ad app update --id $objectId --optional-claims '{"idToken":[{"name":"email","source":null,"essential":false,"additionalProperties":[]},{"name":"family_name","source":null,"essential":false,"additionalProperties":[]},{"name":"given_name","source":null,"essential":false,"additionalProperties":[]}]}' --output "tsv" --only-show-errors);

	#check owner
 	if [ -z "$ownerEmail" ]; then
  		echo "No app owner email was provided." >&2;
  	else
	  	#ensure owner
	   	local ownerId=$(get_user_object_id $ownerEmail);
		local owner=$(az ad app owner add --id $objectId --owner-object-id $ownerId);
	 	echo "Ensured $ownerEmail ($ownerId) as an owner of Entra Id app $name successfully." >&2;
   	fi

  	#return
	echo "$clientId|$clientSecret";
}

#Grants and admin consents a permission to an app if it doesn't already exist. [Returns: Nothing]
function assign_app_permission()
{
	#initialization
 	local appId=$1;
  	local scope=$3;
   	local scopeName=$4;
  	local apiPermissionId=$2;
	local message="app $appId the $scope $scopeName scope of the $apiPermissionId API permission";

	#parse scope id
	echo  "Granting and admin consenting $message." >&2;
 	local scopeComponents=(${scope//=/ });
	local scopeId=${scopeComponents[0]};

	#check permissions
	local authPermission=$(az ad app permission list --id $appId --query "[?resourceAccess[?id == '$scopeId']]" --output "tsv" --only-show-errors);
	if [ -z "$authPermission" ]; then
		#grant pemission
		local authPermission=$(az ad app permission add --id $appId --api $apiPermissionId --api-permissions $scope --only-show-errors);
		local authPermissionGrant=$(az ad app permission grant --id $appId --api $apiPermissionId --scope $scopeName --only-show-errors);

		#admin consent (pause needed below to wait for the above permission grant to propagate)
		sleep 10;
		authPermissionAdminConsent=$(az ad app permission admin-consent --id $appId --only-show-errors);

  		#return
		echo "Granted and admin consented $message successfully." >&2;
	else
		#auth permission already exists
		echo "Already granted and admin consented the $message." >&2;
	fi
}

#Grants a service principal access to an Azure source under the given role. [Returns nothing]
function ensure_rbac_access()
{
	#initialization
	local roleName=$3;
	local resourceId=$2;
	local principalId=$1;
	
	#return
	local role=$(az role assignment create --assignee "$principalId" --scope "$resourceId" --role "$roleName");
	echo "Granted $roleName access for $principalId to $resourceId successfully." >&2;
}

#Creates an Entra ID app and client secret to use for GitHub actions that deploy an Azure web app. If the app already exists, the client secret is overwritten. [Returns: Nothing, but outputs the credentials to the screen.]
function get_web_app_deployment_credential()
{
	#initialization
	local role=$3;
 	local webAppName=$2;
	local webAppScope=$1;
	echo "Creating $role deployment credentials for web app $webAppName." >&2;

	#create credentials
	local credentials=$(az ad sp create-for-rbac --name "$webAppName" --role "$role" --scopes "$webAppScope" --years "2" --output "json");
	echo "Deployment credentials for web app $webAppName generated successfully:" >&2;
	echo $credentials >&2;
}

#Polls the given resource's provisioning status. [Returns: 0 (Succeeded)  or 1 (Failed)]
function wait_for_az_rest_command()
{
	#initialization
 	local resourceId=$1;
	local maxSeconds=$3;
  	local totalSeconds="0";
 	local provisioningState="";
  	local pollingIntervalSeconds=$2;

 	#wait for success
	while [ "$provisioningState" != "Succeeded" ]; do
 		#pause
  		sleep $pollingIntervalSeconds;
  		totalSeconds=$((totalSeconds + pollingIntervalSeconds));

 		#poll
 		provisioningState=$(az resource show --id "$resourceId" --query "properties.provisioningState" --output "tsv");
  		echo "Current provisioning status for $resourceId after $totalSeconds seconds: $provisioningState."  >&2;

  		#check result
	  	if [ "$provisioningState" == "Failed" ] || [ "$totalSeconds" -ge "$maxSeconds" ]; then
	    	echo "Provisioning $resourceId failed after $totalSeconds seconds." >&2;
	  		echo "1";
	    	exit;
	  	fi
	done

  	#return
   	echo "Provisioning $resourceId succeeded after $totalSeconds seconds." >&2;
	echo "0";
}

#Creates an Azure Search instance if one doesn't already exist. [Returns: queryKey|adminKey|principalId]
function ensure_azure_search()
{
	#initialization
	local sku=$4;
 	local name=$3;
	local region=$2;
	local resourceGroupName=$1;

	#check existing
   	echo "Ensuring search instance $name." >&2;
	local search=$(az search service list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$search" ]; then
		#create search instance
		echo "Creating search instance $name." >&2;
		search=$(az search service create --resource-group $resourceGroupName --location $region --name $name --sku $sku);

		#search instance created
		echo "Search instance $name created successfully." >&2;
	else
		#storage account already exists
		echo "Search instance $name already exists." >&2;
	fi
	
	#ensure search keys
	adminKey=$(az search admin-key show --resource-group $resourceGroupName --service-name $name --query "primaryKey" --output "tsv");
	queryKey=$(az search query-key list --resource-group $resourceGroupName --service-name $name --query "[0].key" --output "tsv");
	if [ -z "$queryKey" ]; then
		#create query key
		echo "Creating search query key for $name." >&2;
		queryKey=$(az search query-key create --resource-group $resourceGroupName --service-name $name --name "query-key" --query "key" --output "tsv");
		echo "Created search query key for $name successfully." >&2;
	else
		#query key already exists
		echo "Search query key for $searchName already exists." >&2;
	fi

	#create managed identity
	local principalId=$(az search service update --resource-group "$resourceGroupName" --name "$name" --identity-type "SystemAssigned" --query "identity.principalId" --output "tsv");

 	#return
  	echo "$queryKey|$adminKey|$principalId";
}

#Creates an Azure App Service Plan if one doesn't already exist. [Returns: Nothing]
function ensure_app_service_plan()
{
	#initialization
	local sku=$4;
 	local name=$3;
	local region=$2;
	local isLinux=$5;
	local resourceGroupName=$1;

	#check existing
	plan=$(az appservice plan list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$plan" ]; then
		#create app service plan
		echo "Creating app service plan $name..." >&2;
		if [ "$isLinux" == "true" ]; then
			plan=$(az appservice plan create --resource-group $resourceGroupName --location $region --name $name --sku $sku --is-linux);
		else
			plan=$(az appservice plan create --resource-group $resourceGroupName --location $region --name $name --sku $sku);
		fi
		echo "Created app service plan $name successfully." >&2;
	else
		#app service plan already exists
		echo "App service plan $name already exists." >&2;
	fi
}

#Creates an Azure Web App if one doesn't already exist. [Returns: principalId|url]
function ensure_web_app()
{
	#initialization
 	local name=$3;
	local runtime=$4;
	local timezone=$5;
	local appPlanName=$2;
	local keyVaultName=$7;
	local startUpCommand=$8;
	local appInsightsName=$6;
	local resourceGroupName=$1;
	local healthCheckEndpointPath=$9;

	#check existing
	webApp=$(az webapp list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$webApp" ]; then
		#create web app
		echo "Creating web app $name." >&2;
		local id=$(az webapp create --resource-group $resourceGroupName --plan $appPlanName --name $name --runtime $runtime --https-only --query "id" --output "tsv");

		#configure app service
		appServiceConfigResult=$(az webapp config set --resource-group $resourceGroupName --name $name --always-on "true" --http20-enabled "true" --min-tls-version "1.2" --use-32bit-worker-process "false" --remote-debugging-enabled "false");
		appServiceUpdateResult=$(az webapp update --resource-group $resourceGroupName --name $name --client-affinity-enabled "false");

		#link to app insights
		appInsightsResult=$(az monitor app-insights component connect-webapp --resource-group $resourceGroupName --app $appInsightsName --web-app $name --enable-snapshot-debugger "true" --enable-profiler "true");
		echo "Web app $name created successfully." >&2;
	else
		#web app already exists
		echo "Web app $name already exists." >&2;
	fi

	#ensure general app settings
	appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "AZURE_KEY_VAULT_URL=https://$keyVaultName.vault.azure.net");
	appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "WEBSITE_TIME_ZONE=$timezone");
	echo "Configured $name app settings." >&2;

	#ensure platform-specfic app settings
	if [[ "$runtime" == "NODE"* ]]; then
		#disable node oryx builds
		appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "ENABLE_ORYX_BUILD=false");
		appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "SCM_DO_BUILD_DURING_DEPLOYMENT=false");

		#configure node environment
		appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "WEBSITES_CONTAINER_START_TIME_LIMIT=600");
		appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "NODE_ENV=production");
		appServiceSettingResult=$(az webapp config appsettings set --resource-group $resourceGroupName --name $name --settings "PORT=80");
		echo "Configured $name node app settings." >&2;		
	fi

	#set startup command
	if [ ! -z "$startUpCommand" ]; then
		appServiceSettingResult=$(az webapp config set --resource-group $resourceGroupName --name $name --startup-file "$startUpCommand");
		echo "Set startup command '$startUpCommand' for web app $name." >&2;
	fi

	#set health check
	if [ ! -z "$healthCheckEndpointPath" ]; then
		appServiceSettingResult=$(az webapp config set --resource-group $resourceGroupName --name $name --generic-configurations "{'healthCheckPath': '$healthCheckEndpointPath'}");
		echo "Set health check endpoint path to '$healthCheckEndpointPath' for web app $name." >&2;
	fi

	#ensure managed identity and grant "user" key vault access
	local principalId=$(az webapp identity assign --resource-group $resourceGroupName --name $name --query "principalId" --output "tsv");
	local accessPolicyResult=$(ensure_key_vault_user_access_policy "$keyVaultName" "$principalId");	

	#get url
	local url=$(az webapp show --resource-group $resourceGroupName --name $name --query "defaultHostName" --output "tsv");
	
	#return
	echo "Assigned managed identity $principalId to web app $name and granted access to key vault $keyVaultName." >&2;
	echo "$principalId|https://$url";
}

#Sets the CORS rules for a websie. [Returns: nothing]
function ensure_web_app_cors()
{
	#initialization
	local name=$2;
	local origins=$3;
	local resourceGroupName=$1;
	local supportCredentials=$4;
	
	#check credentials
	echo "Configuring CORS for $name." >&2; 
	if [ "$supportCredentials" != "true" ]; then
		supportCredentials="false";
	fi

	#set credentials
	local corsCredentialsResult=$(az resource update --resource-group $resourceGroupName --name "web" --namespace "Microsoft.Web" --resource-type "config" --parent "sites/$name" --set "properties.cors.supportCredentials=$supportCredentials");
	echo "Set CORS allowed credentials to $supportCredentials for $name." >&2; 

	#return
	local corsOriginsResult=$(az webapp cors add --resource-group $resourceGroupName --name $name --allowed-origins $origins);
	echo "Set CORS allowed origins to $origins for $name." >&2; 
}

#Creates an Azure Static Web App if one doesn't already exist. [Returns: principalId|url]
function ensure_static_web_app()
{
	#initialization
	local sku=$4;
 	local name=$2;
	local region=$3;
	local timezone=$5;
	local keyVaultName=$6;
	local resourceGroupName=$1;
	local appInsightsConnectionString=$7;

	#check existing
	staticWebApp=$(az staticwebapp  list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$staticWebApp" ]; then
		#create static web app
		echo "Creating static web app $name." >&2;
		staticWebApp=$(az staticwebapp create --resource-group $resourceGroupName --name $name --sku $sku --location $region --output "tsv");

		#connect to app insights
		staticWebAppSettingsResult=$(az staticwebapp appsettings set --resource-group $resourceGroupName --name $name --setting-names "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString");

		#static web app created
		staticWebAppSettingsResult=$(az staticwebapp appsettings set --resource-group $resourceGroupName --name $name --setting-names "WEBSITE_TIME_ZONE=$timezone");
		echo "Static web app $name created successfully." >&2;
	else
		#static web app  already exists
		echo "Static web app $name already exists." >&2;
	fi

	#ensure managed identity and grant "user" key vault access
	local principalId=$(az staticwebapp identity assign --resource-group $resourceGroupName --name $name --query "principalId" --output "tsv");
	local accessPolicyResult=$(ensure_key_vault_user_access_policy "$keyVaultName" "$principalId");	
	
	#return
	local hostName=$(az staticwebapp show --resource-group $resourceGroupName --name $name --query "defaultHostname" --output "tsv");
	echo "$principalId|https://$hostName";
}

#Creates an Azure Fluid Relay if one doesn't already exist. [Returns: principalId|tenantId|endpoint|key|id]
function ensure_fluid_relay()
{
	#initialization
	local sku=$4;
	local name=$2;
	local region=$3;
	local principalId="";
	local resourceGroupName=$1;

	#check existing
	fluidRelay=$(az fluid-relay server list --resource-group $resourceGroupName --query "[?name == '$name']" --output "tsv");
	if [ -z "$fluidRelay" ]; then
		#create fluid relay
		echo "Creating fluid relay $name." >&2;
		principalId=$(az fluid-relay server create --resource-group $resourceGroupName --name $name --sku $sku --location $region --identity "type='SystemAssigned'" --query "identity.principalId" --output "tsv");

		#fluid relay created
		echo "Fluid relay $name created successfully." >&2;
	else
		#fluid relay  already exists
		echo "Fluid relay $name already exists." >&2;
		principalId=$(az fluid-relay server show --resource-group $resourceGroupName --name $name --query "identity.principalId" --output "tsv");
	fi

	#get connection properties
	local id=$(az fluid-relay server show --resource-group $resourceGroupName --name $name --query "id" --output "tsv");
	local key=$(az fluid-relay server list-key --resource-group $resourceGroupName --server-name $name --query "key1" --output "tsv");
	local tenantId=$(az fluid-relay server show --resource-group $resourceGroupName --name $name --query "frsTenantId" --output "tsv");
	local endpoint=$(az fluid-relay server show --resource-group $resourceGroupName --name $name --query "fluidRelayEndpoints.serviceEndpoints" --output "tsv");

	#return
	echo "$principalId|$tenantId|$endpoint|$key|$id";
}
