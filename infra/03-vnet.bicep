@description('Die Azure Region, in der die Ressourcen erstellt werden sollen')
param location string = resourceGroup().location

@description('Der Name für alle Bezeichnungen')
param Name string

@description('Adressbereich des VNET')
param vnetAddressPrefix string = '10.0.0.0/16'

var tags = { 'azd-env-name': Name }

// NSG für APIM-Subnet
resource nsgApim 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: 'nsg-apim-subnet'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowManagementEndpointForAzure'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '3443'
          sourceAddressPrefix: 'ApiManagement'
          destinationAddressPrefix: 'VirtualNetwork'
        }
      }
      {
        name: 'AllowAzureLoadBalancer'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '6390'
          sourceAddressPrefix: 'AzureLoadBalancer'
          destinationAddressPrefix: 'VirtualNetwork'
        }
      }
      {
        name: 'AllowHTTPSInbound'
        properties: {
          priority: 120
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: 'VirtualNetwork'
        }
      }
      {
        name: 'AllowStorageOutbound'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: 'Storage'
        }
      }
      {
        name: 'AllowSqlOutbound'
        properties: {
          priority: 110
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '1433'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: 'Sql'
        }
      }
      {
        name: 'AllowKeyVaultOutbound'
        properties: {
          priority: 120
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: 'AzureKeyVault'
        }
      }
    ]
  }
}

// NSG für AppService-Subnet
resource nsgAppSvc 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: 'nsg-appsvc-subnet'
  location: location
  tags: tags
  properties: {
    securityRules: []
  }
}

// NSG für Private-Endpoints-Subnet
resource nsgPe 'Microsoft.Network/networkSecurityGroups@2023-04-01' = {
  name: 'nsg-pe-subnet'
  location: location
  tags: tags
  properties: {
    securityRules: []
  }
}

// VNET mit drei Subnetzen
resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: 'VNET_${Name}'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      // 1. APIM-Subnet
      {
        name: 'apim-subnet'
        properties: {
          addressPrefix: '10.0.1.0/24'
          networkSecurityGroup: {
            id: nsgApim.id
          }
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
            }
            {
              service: 'Microsoft.Sql'
            }
            {
              service: 'Microsoft.KeyVault'
            }
          ]
        }
      }
      // 2. App Service VNET-Integration
      {
        name: 'appsvc-subnet'
        properties: {
          addressPrefix: '10.0.2.0/24'
          networkSecurityGroup: {
            id: nsgAppSvc.id
          }
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      // 3. Private Endpoints
      {
        name: 'privateendpoints-subnet'
        properties: {
          addressPrefix: '10.0.3.0/24'
          networkSecurityGroup: {
            id: nsgPe.id
          }
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

// Outputs
output vnetId string = vnet.id
output vnetName string = vnet.name
output apimSubnetId string = vnet.properties.subnets[0].id
output appSvcSubnetId string = vnet.properties.subnets[1].id
output privateEndpointsSubnetId string = vnet.properties.subnets[2].id