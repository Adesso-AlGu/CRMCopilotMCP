@description('Region')
param location string = resourceGroup().location

@description('Der Name für alle Bezeichnungen')
param Name string

// bestehende Ressourcen
resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' existing = {
  name: 'VNET_${Name}'
}

resource peSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-04-01' existing = {
  parent: vnet
  name: 'privateendpoints-subnet'
}

// Referenzen zu den drei App Services
resource appLead 'Microsoft.Web/sites@2023-12-01' existing = {
  name: 'app-${Name}-lead'
}

resource appOpportunity 'Microsoft.Web/sites@2023-12-01' existing = {
  name: 'app-${Name}-opportunity'
}

resource appQuote 'Microsoft.Web/sites@2023-12-01' existing = {
  name: 'app-${Name}-quote'
}

// Private Endpoint für Lead App Service
resource appLeadPe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-app-${Name}-lead'
  location: location
  properties: {
    subnet: {
      id: peSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'app-mcp-lead-ples'
        properties: {
          privateLinkServiceId: appLead.id
          groupIds: [
            'sites'
          ]
          requestMessage: 'Private Endpoint für MCP Lead App Service'
        }
      }
    ]
  }
}

// Private Endpoint für Opportunity App Service
resource appOpportunityPe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-app-${Name}-opportunity'
  location: location
  properties: {
    subnet: {
      id: peSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'app-mcp-opportunity-ples'
        properties: {
          privateLinkServiceId: appOpportunity.id
          groupIds: [
            'sites'
          ]
          requestMessage: 'Private Endpoint für MCP Opportunity App Service'
        }
      }
    ]
  }
}

// Private Endpoint für Quote App Service
resource appQuotePe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-app-${Name}-quote'
  location: location
  properties: {
    subnet: {
      id: peSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'app-mcp-quote-ples'
        properties: {
          privateLinkServiceId: appQuote.id
          groupIds: [
            'sites'
          ]
          requestMessage: 'Private Endpoint für MCP Quote App Service'
        }
      }
    ]
  }
}