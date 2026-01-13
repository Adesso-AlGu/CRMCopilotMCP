targetScope = 'subscription'

@description('Der Name der Ressourcen-Gruppe')
param resourceGroupName string

@description('Die Azure Region, in der die Ressourcen erstellt werden sollen')
param location string = 'germanywestcentral'

@description('Der Name für alle Bezeichnungen')
param name string

@description('Entra ID Client ID (App-Service ID)')
param clientId string

@description('Entra ID Client ID (Copilot Studio App-Reg-ID)')
param copilotStudioClientId string

@description('Die Principal ID des aktuellen Benutzers für Rollenzuweisung')
param principalId string

@description('Adressbereich des VNET')
param vnetAddressPrefix string = '10.0.0.0/16'

// 01 - Resource Group erstellen
module resourceGroup '01-resource-group.bicep' = {
  name: 'deploy-resource-group'
  params: {
    location: location
    NameRG: resourceGroupName
    Name: name
  }
}

// 02 - Logging & Monitoring
module loggingMonitoring '02-logging-monitoring.bicep' = {
  name: 'deploy-logging-monitoring'
  scope: az.resourceGroup(resourceGroupName)
  params: {
    location: location
    Name: name
  }
  dependsOn: [
    resourceGroup
  ]
}

// 03 - Virtual Network
module vnet '03-vnet.bicep' = {
  name: 'deploy-vnet'
  scope: az.resourceGroup(resourceGroupName)
  params: {
    location: location
    Name: name
    vnetAddressPrefix: vnetAddressPrefix
  }
  dependsOn: [
    resourceGroup
  ]
}

// 04 - Key Vault
module keyVault '04-keyvault.bicep' = {
  name: 'deploy-keyvault'
  scope: az.resourceGroup(resourceGroupName)
  params: {
    location: location
    Name: name
    principalId: principalId
  }
  dependsOn: [
    resourceGroup
  ]
}

// 05 - App Service MCP
module appService '05-appservice-mcp.bicep' = {
  name: 'deploy-appservice-mcp'
  scope: az.resourceGroup(resourceGroupName)
  params: {
    location: location
    Name: name
    clientId: clientId
    copilotStudioClientId: copilotStudioClientId
  }
  dependsOn: [
    vnet
    loggingMonitoring
    keyVault
  ]
}