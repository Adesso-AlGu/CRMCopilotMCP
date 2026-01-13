@description('Die Azure Region, in der die Ressourcen erstellt werden sollen')
param location string

@description('Der Name Resourcen-Grupppe')
param NameRG string

@description('Der Name für alle Bezeichnungen')
param Name string

targetScope = 'subscription'
var tags = { 'azd-env-name': Name }

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: NameRG
  location: location
  tags: tags
}