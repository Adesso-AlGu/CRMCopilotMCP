# CRM Copilot MCP

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## 📋 Überblick

CRM Copilot MCP ist ein auf .NET 9 basierender MCP-Server (Model Context Protocol). Das Repository enthält sowohl die Server-Implementierung als auch Azure-Bicep-Vorlagen für eine vollständige Cloud-Infrastruktur.

### ✨ Features

- 🔧 **MCP-Server**: Vollständige Implementierung des Model Context Protocol
- ☁️ **Azure-Ready**: Vorkonfigurierte Bicep-Templates für Cloud-Deployment
- 🚀 **Hot Reload**: Entwicklungsfreundlich mit automatischem Neuladen

## 🛠️ Voraussetzungen

### Lokale Entwicklung
- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) oder neuer
- **IDE (eines davon):**
  - [Visual Studio 2022](https://visualstudio.microsoft.com/) oder
  - [VS Code](https://code.visualstudio.com/) mit [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

### Azure Cloud-Deployment
- **Erforderlich:**
  - [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (neueste Version)
  - [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
  - [PowerShell 7+](https://github.com/PowerShell/PowerShell)
  - Aktives Azure-Abonnement mit Contributor-Rechten

### Azure AD App Registration (für Authentifizierung)
- Azure AD-Berechtigung zum Erstellen von App Registrations

## 🚀 Schnellstart

### Lokal starten

```pwsh
# Repository klonen
git clone https://github.com/IHR_USERNAME/CRMCopilotMCP.git
cd CRMCopilotMCP

# Abhängigkeiten wiederherstellen (für alle Projekte)
dotnet restore src/mcp-csharp-sdk.sln

# Entwicklungsserver starten (z.B. Lead-Server)
dotnet run --project src/McpServer.Lead/McpServer.Lead.csproj
# Oder Opportunity-Server:
# dotnet run --project src/McpServer.Opportunity/McpServer.Opportunity.csproj
# Oder Quote-Server:
# dotnet run --project src/McpServer.Quote/McpServer.Quote.csproj

# (Optional) HTTP-Endpunkte testen
dotnet tool install --global Microsoft.dotnet-httprepl
httprepl http://localhost:5000
```

## ⚙️ Konfiguration

### Lokale Entwicklung
Anwendungseinstellungen liegen in den jeweiligen Projektverzeichnissen:
- `src/McpServer.Lead/appsettings.json`
- `src/McpServer.Opportunity/appsettings.json`
- `src/McpServer.Quote/appsettings.json`

Entwicklungs-spezifische Werte überschreiben Sie in den entsprechenden `appsettings.Development.json` Dateien.

### Azure Key Vault (Produktion)
In der Produktionsumgebung werden alle Secrets automatisch in Azure Key Vault gespeichert und über Managed Identity abgerufen. Die Infrastruktur wird durch die Bicep-Module im `infra/` Verzeichnis bereitgestellt:

- Client Secret wird in Key Vault Secret gespeichert
- App Service verwendet System-Assigned Managed Identity
- Automatische RBAC-Rollenzuweisung: "Key Vault Secrets User"

## Projektstruktur
- `src/` – MCP-Server-Implementierungen (C#)
  - `mcp-csharp-sdk.sln` – Visual Studio Solution für alle Server
  - `McpServer.http` – HTTP-Test-Datei für alle Endpunkte
  - `McpServer.Lead/` – Lead-Management Server mit LeadTools
  - `McpServer.Opportunity/` – Opportunity-Management Server mit OpportunityTools und DataverseService (inkl. Azure AD Authentifizierung)
  - `McpServer.Quote/` – Quote-Management Server mit QuoteTools
- `infra/` – Azure-Infrastruktur (Bicep-Module)
  - `01-resource-group.bicep` – Resource Group Definition
  - `02-logging-monitoring.bicep` – Logging und Monitoring
  - `03-vnet.bicep` – Virtual Network
  - `04-keyvault.bicep` – Key Vault
  - `05-appservice-mcp.bicep` – App Service
  - `06-apim-internal.bicep` – API Management
  - `07-appservice-pe.bicep` – Private Endpoints
  - `main.bicep` – Haupt-Deployment-Template
  - `main.parameters.json` – Parameter-Datei für Deployment
- `helper/` – Hilfsdateien und Beispiele
- `azure.yaml` – Deployment-Konfiguration für Azure Developer CLI

## Entwicklung
- Linting/Formatierung via `dotnet format`
- Tests (sofern vorhanden) via `dotnet test`
- Debugging: `dotnet watch run` ermöglicht Hot Reload

## 🚀 Deployment

### Voraussetzungen für Deployment
1. **Azure CLI authentifiziert**:
   ```pwsh
   az login
   azd auth login
   ```
2. **Parameter konfiguriert** in `infra/main.parameters.json`

### Azure Developer CLI (Empfohlen)
```pwsh
# Initialisierung
azd init

# Deployment
azd up
```

### Manuelle Bicep-Bereitstellung
Nutzen Sie `infra/main.bicep` für manuelle Bereitstellung:

```pwsh
az deployment sub create `
  --name crmcopilot-$(Get-Date -Format yyyyMMddHHmm) `
  --location "West Europe" `
  --template-file infra/main.bicep `
  --parameters @infra/main.parameters.json
```

### Weitere Informationen
- **Deployment-Details**: Siehe [DEPLOYMENT.md](DEPLOYMENT.md)

## 🐛 Troubleshooting

### Build-Fehler
```pwsh
# Cache löschen und neu bauen
dotnet clean
dotnet restore
dotnet build
```

### Port bereits in Verwendung
```pwsh
# Anderen Port verwenden
dotnet run --project src/McpServer.Lead/McpServer.Lead.csproj --urls "http://localhost:5001"
# oder für einen anderen Server entsprechend anpassen
```

### Azure Deployment-Probleme
```pwsh
# Deployment-Logs anzeigen
azd deploy --debug

# App Service Logs streamen
az webapp log tail --name app-<token> --resource-group <env>-rg
```

## 🔒 Sicherheit & Best Practices

### Entwicklung
- ✅ Verwenden Sie `appsettings.Development.json` für lokale Einstellungen
- ✅ Nutzen Sie .NET User Secrets für lokale Entwicklung: `dotnet user-secrets set "Key" "Value"`
- ✅ Aktivieren Sie GitHub Secret Scanning
- ❌ Committen Sie niemals Secrets in Git

### Produktion
- ✅ Alle Secrets in Azure Key Vault speichern
- ✅ Managed Identity für Service-zu-Service-Authentifizierung
- ✅ TLS 1.2+ erzwingen (bereits in Bicep konfiguriert)
- ✅ Application Insights für Monitoring aktivieren
- ✅ Regular Security Audits durchführen

### Compliance
- GDPR: Stellen Sie sicher, dass personenbezogene Daten DSGVO-konform verarbeitet werden
- Logging: Loggen Sie keine sensiblen Daten (Secrets, PII)
- Audit: Aktivieren Sie Azure AD Audit Logs für Compliance-Nachweise

## 📚 Weiterführende Dokumentation

- [Deployment Guide](DEPLOYMENT.md) - Detaillierte Deployment-Anleitung
- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/) - MCP-Standard
- [Azure App Service Security](https://learn.microsoft.com/azure/app-service/overview-security) - Azure Best Practices

## 📄 Lizenz

Dieses Projekt ist unter der MIT-Lizenz lizenziert - siehe [LICENSE](LICENSE) für Details.

## 🤝 Contributing

Beiträge sind willkommen! Bitte beachten Sie:
1. Fork des Repositories erstellen
2. Feature Branch erstellen (`git checkout -b feature/AmazingFeature`)
3. Änderungen committen (`git commit -m 'Add some AmazingFeature'`)
4. Branch pushen (`git push origin feature/AmazingFeature`)
5. Pull Request öffnen

## 📧 Support

Bei Fragen oder Problemen erstellen Sie bitte ein [Issue](https://github.com/Adesso-AlGu/CRMCopilotMCP/issues) im Repository.
