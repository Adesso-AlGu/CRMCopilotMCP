@description('Region')
param location string = resourceGroup().location

@description('Der Name für alle Bezeichnungen')
param Name string

@description('Publisher E-Mail für APIM')
param publisherEmail string = 'admin@example.com'

@description('Publisher Name')
param publisherName string = 'adesso'

var tags = { 'azd-env-name': Name }

@description('APIM SKU (Developer/Premium)')
@allowed([
  'Developer'
  'Premium'
])
param apimSkuName string = 'Developer'

@description('APIM SKU Kapazität')
param apimSkuCapacity int = 1

// bestehendes VNET + Subnet referenzieren
resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' existing = {
  name: 'VNET_${Name}'
}

resource apimSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-04-01' existing = {
  parent: vnet
  name: 'apim-subnet'
}

// API Management im Internal Modus
resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: 'apim-${Name}'
  location: location
  tags: tags
  sku: {
    name: apimSkuName
    capacity: apimSkuCapacity
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName

    // Wichtig: Internal VNET Mode
    virtualNetworkType: 'Internal'
    virtualNetworkConfiguration: {
      subnetResourceId: apimSubnet.id
    }
  }
}