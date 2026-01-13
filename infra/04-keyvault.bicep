@description('Die Azure Region, in der die Ressourcen erstellt werden sollen')
param location string = resourceGroup().location

@description('Der Name für alle Bezeichnungen')
param Name string

@description('Die Principal ID des aktuellen Benutzers für Rollenzuweisung')
param principalId string

var tags = { 'azd-env-name': Name }

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${Name}'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

// Rollenzuweisung: Geheimnisbenutzer für Schlüsseltresore
resource kvSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, principalId, 'Key Vault Secrets User')
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '00482a5a-887f-4fb3-b363-3b7fe8e74483')
    principalId: principalId
    principalType: 'User'
  }
}
