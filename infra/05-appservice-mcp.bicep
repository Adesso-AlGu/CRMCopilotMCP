@description('Region')
param location string = resourceGroup().location

@description('Der Name für alle Bezeichnungen')
param Name string

@description('Entra ID Client ID (App-Service ID)')
param clientId string

@description('Entra ID Client ID (Copilot Studio App-Reg-ID)')
param copilotStudioClientId string

//@description('Create new role assignment (set to false if already exists)')
//param createRoleAssignment bool = false

var tags = { 'azd-env-name': Name }
var tenantId = tenant().tenantId

// VNET + Subnet für VNET-Integration
resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' existing = {
  name: 'VNET_${Name}'
}

resource appSvcSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-04-01' existing = {
  parent: vnet
  name: 'appsvc-subnet'
}

// Application Insights (vorhandene Resource)
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: 'appi-${Name}'
}

// Key Vault (vorhandene Resource)
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: 'kv-${Name}'
}

// Secret für Client Secret
resource clientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'entra-client-secret'
}

// App Service Plan (z.B. P1v3 für Demo reicht auch B1)
resource asp 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${Name}'
  location: location
  tags: tags
  sku: {
    name: 'F1'
    tier: 'Free'
    capacity: 1
  }
  properties: {
    reserved: false
  }
}

// Web App (MCP-Server Lead)
resource appLead 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${Name}-lead'
  location: location
  tags: union(tags, { 'azd-service-name': 'lead' })
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: asp.id
    httpsOnly: true
    clientAffinityEnabled: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: false
      windowsFxVersion: 'DOTNET|9.0'
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnet'
        }
      ]
    }
  }
  
  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      SCM_DO_BUILD_DURING_DEPLOYMENT: 'true'
      WEBSITE_HTTPLOGGING_RETENTION_DAYS: '3'
      MCP_TRANSPORT: 'streaming-http'
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
      // Azure AD Authentication Configuration - using Key Vault reference
      MICROSOFT_PROVIDER_AUTHENTICATION_SECRET: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${clientSecretSecret.name})'
      WEBSITE_AUTH_AAD_ACL: '{"allowed_token_audiences":["api://${clientId}","api://${copilotStudioClientId}"],"allowed_client_applications":["${clientId}","${copilotStudioClientId}"]}'
      // Additional security settings
      AZURE_CLIENT_ID: clientId
      AZURE_TENANT_ID: tenantId
      KEY_VAULT_NAME: keyVault.name
    }
  }
  
  resource authSettings 'config' = {
    name: 'authsettingsV2'
    properties: {
      globalValidation: {
        requireAuthentication: true
        unauthenticatedClientAction: 'RedirectToLoginPage'
        redirectToProvider: 'azureActiveDirectory'
        excludedPaths: [
          '/health'
        ]
      }
      identityProviders: {
        azureActiveDirectory: {
          enabled: true
          registration: {
            openIdIssuer: 'https://sts.windows.net/${tenantId}/'
            clientId: clientId
            clientSecretSettingName: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET'
          }
          validation: {
            allowedAudiences: [
              clientId
              'api://${clientId}'
            ]
            defaultAuthorizationPolicy: {
              allowedApplications: [
                clientId
              ]
            }
          }
          login: {
            loginParameters: [
              'response_type=code id_token'
              'scope=openid profile email'
            ]
          }
        }
      }
      httpSettings: {
        requireHttps: true
        routes: {
          apiPrefix: '/.auth'
        }
        forwardProxy: {
          convention: 'NoProxy'
        }
      }
      login: {
        routes: {
          logoutEndpoint: '/.auth/logout'
        }
        tokenStore: {
          enabled: true
          tokenRefreshExtensionHours: 72
        }
        preserveUrlFragmentsForLogins: true
      }
    }
  }
}

// Web App (MCP-Server Opportunity)
resource appOpportunity 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${Name}-opportunity'
  location: location
  tags: union(tags, { 'azd-service-name': 'opportunity' })
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: asp.id
    httpsOnly: true
    clientAffinityEnabled: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: false
      windowsFxVersion: 'DOTNET|9.0'
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnet'
        }
      ]
    }
  }
  
  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      SCM_DO_BUILD_DURING_DEPLOYMENT: 'true'
      WEBSITE_HTTPLOGGING_RETENTION_DAYS: '3'
      MCP_TRANSPORT: 'streaming-http'
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
      // Azure AD Authentication Configuration - using Key Vault reference
      MICROSOFT_PROVIDER_AUTHENTICATION_SECRET: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${clientSecretSecret.name})'
      WEBSITE_AUTH_AAD_ACL: '{"allowed_token_audiences":["api://${clientId}","api://${copilotStudioClientId}"],"allowed_client_applications":["${clientId}","${copilotStudioClientId}"]}'
      // Additional security settings
      AZURE_CLIENT_ID: clientId
      AZURE_TENANT_ID: tenantId
      KEY_VAULT_NAME: keyVault.name
    }
  }
  
  resource authSettings 'config' = {
    name: 'authsettingsV2'
    properties: {
      globalValidation: {
        requireAuthentication: true
        unauthenticatedClientAction: 'RedirectToLoginPage'
        redirectToProvider: 'azureActiveDirectory'
        excludedPaths: [
          '/health'
        ]
      }
      identityProviders: {
        azureActiveDirectory: {
          enabled: true
          registration: {
            openIdIssuer: 'https://sts.windows.net/${tenantId}/'
            clientId: clientId
            clientSecretSettingName: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET'
          }
          validation: {
            allowedAudiences: [
              clientId
              'api://${clientId}'
            ]
            defaultAuthorizationPolicy: {
              allowedApplications: [
                clientId
              ]
            }
          }
          login: {
            loginParameters: [
              'response_type=code id_token'
              'scope=openid profile email'
            ]
          }
        }
      }
      httpSettings: {
        requireHttps: true
        routes: {
          apiPrefix: '/.auth'
        }
        forwardProxy: {
          convention: 'NoProxy'
        }
      }
      login: {
        routes: {
          logoutEndpoint: '/.auth/logout'
        }
        tokenStore: {
          enabled: true
          tokenRefreshExtensionHours: 72
        }
        preserveUrlFragmentsForLogins: true
      }
    }
  }
}

// Web App (MCP-Server Quote)
resource appQuote 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-${Name}-quote'
  location: location
  tags: union(tags, { 'azd-service-name': 'quote' })
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: asp.id
    httpsOnly: true
    clientAffinityEnabled: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: false
      windowsFxVersion: 'DOTNET|9.0'
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnet'
        }
      ]
    }
  }
  
  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      SCM_DO_BUILD_DURING_DEPLOYMENT: 'true'
      WEBSITE_HTTPLOGGING_RETENTION_DAYS: '3'
      MCP_TRANSPORT: 'streaming-http'
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
      // Azure AD Authentication Configuration - using Key Vault reference
      MICROSOFT_PROVIDER_AUTHENTICATION_SECRET: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${clientSecretSecret.name})'
      WEBSITE_AUTH_AAD_ACL: '{"allowed_token_audiences":["api://${clientId}","api://${copilotStudioClientId}"],"allowed_client_applications":["${clientId}","${copilotStudioClientId}"]}'
      // Additional security settings
      AZURE_CLIENT_ID: clientId
      AZURE_TENANT_ID: tenantId
      KEY_VAULT_NAME: keyVault.name
    }
  }
  
  resource authSettings 'config' = {
    name: 'authsettingsV2'
    properties: {
      globalValidation: {
        requireAuthentication: true
        unauthenticatedClientAction: 'RedirectToLoginPage'
        redirectToProvider: 'azureActiveDirectory'
        excludedPaths: [
          '/health'
        ]
      }
      identityProviders: {
        azureActiveDirectory: {
          enabled: true
          registration: {
            openIdIssuer: 'https://sts.windows.net/${tenantId}/'
            clientId: clientId
            clientSecretSettingName: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET'
          }
          validation: {
            allowedAudiences: [
              clientId
              'api://${clientId}'
            ]
            defaultAuthorizationPolicy: {
              allowedApplications: [
                clientId
              ]
            }
          }
          login: {
            loginParameters: [
              'response_type=code id_token'
              'scope=openid profile email'
            ]
          }
        }
      }
      httpSettings: {
        requireHttps: true
        routes: {
          apiPrefix: '/.auth'
        }
        forwardProxy: {
          convention: 'NoProxy'
        }
      }
      login: {
        routes: {
          logoutEndpoint: '/.auth/logout'
        }
        tokenStore: {
          enabled: true
          tokenRefreshExtensionHours: 72
        }
        preserveUrlFragmentsForLogins: true
      }
    }
  }
}

// VNET-Integration für Lead App (Outbound in das VNET, Subnet appsvc-subnet)
resource appLeadVnetIntegration 'Microsoft.Web/sites/virtualNetworkConnections@2023-12-01' = {
  parent: appLead
  name: vnet.name
  properties: {
    vnetResourceId: appSvcSubnet.id
    isSwift: true
  }
}

// VNET-Integration für Opportunity App (Outbound in das VNET, Subnet appsvc-subnet)
resource appOpportunityVnetIntegration 'Microsoft.Web/sites/virtualNetworkConnections@2023-12-01' = {
  parent: appOpportunity
  name: vnet.name
  properties: {
    vnetResourceId: appSvcSubnet.id
    isSwift: true
  }
}

// VNET-Integration für Quote App (Outbound in das VNET, Subnet appsvc-subnet)
resource appQuoteVnetIntegration 'Microsoft.Web/sites/virtualNetworkConnections@2023-12-01' = {
  parent: appQuote
  name: vnet.name
  properties: {
    vnetResourceId: appSvcSubnet.id
    isSwift: true
  }
}
