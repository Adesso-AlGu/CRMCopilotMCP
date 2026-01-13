# MCP Server - Multi-Service Deployment

Diese Lösung enthält drei separate MCP-Server für Lead, Opportunity und Quote Management.

## Projektstruktur

```
src/
├── mcp-csharp-sdk.sln       # Visual Studio Solution für alle Server
├── McpServer.http           # HTTP-Test-Datei
│
├── McpServer.Lead/          # Lead Management Tools
│   ├── Tools/
│   │   └── LeadTools.cs
│   ├── Program.cs
│   ├── McpServer.Lead.csproj
│   └── appsettings.json
│
├── McpServer.Opportunity/   # Opportunity Management Tools
│   ├── Tools/
│   │   └── OpportunityTools.cs
│   ├── Services/
│   │   └── DataverseService.cs
│   ├── Program.cs
│   ├── McpServer.Opportunity.csproj
│   └── appsettings.json
│
└── McpServer.Quote/         # Quote Management Tools
    ├── Tools/
    │   └── QuoteTools.cs
    ├── Program.cs
    ├── McpServer.Quote.csproj
    └── appsettings.json
```

## Deployment-Optionen

### Option 1: Alle Services zusammen deployen

Vom Root-Verzeichnis aus:

```powershell
azd up
```

Dies deployt alle drei Services gleichzeitig basierend auf der [azure.yaml](azure.yaml) im Root-Verzeichnis.

### Option 2: Einzelne Services deployen

#### Lead Service deployen

```powershell
cd src/McpServer.Lead
azd up
```

#### Opportunity Service deployen

```powershell
cd src/McpServer.Opportunity
azd up
```

#### Quote Service deployen

```powershell
cd src/McpServer.Quote
azd up
```

## Lokale Entwicklung

### Alle Projekte bauen

```powershell
cd src
dotnet build mcp-csharp-sdk.sln
```

### Einzelnes Projekt ausführen

#### Lead Server

```powershell
cd src/McpServer.Lead
dotnet run
```

#### Opportunity Server

```powershell
cd src/McpServer.Opportunity
dotnet run
```

#### Quote Server

```powershell
cd src/McpServer.Quote
dotnet run
```

## Azure Resources

Nach dem Deployment werden folgende Azure App Services erstellt:

- `app-{Name}-lead` - Lead Management MCP Server
- `app-{Name}-opportunity` - Opportunity Management MCP Server
- `app-{Name}-quote` - Quote Management MCP Server

Alle Services teilen sich:
- Einen gemeinsamen App Service Plan (`asp-{Name}`)
- Application Insights (`appi-{Name}`)
- Key Vault (`kv-{Name}`)
- Virtual Network für VNET-Integration

## Endpoints

Nach dem Deployment sind die Services unter folgenden URLs verfügbar:

- Lead: `https://app-{Name}-lead.azurewebsites.net/`
- Opportunity: `https://app-{Name}-opportunity.azurewebsites.net/`
- Quote: `https://app-{Name}-quote.azurewebsites.net/`

Status-Checks:
- `https://app-{Name}-lead.azurewebsites.net/status`
- `https://app-{Name}-opportunity.azurewebsites.net/status`
- `https://app-{Name}-quote.azurewebsites.net/status`

## Tools pro Service

### Lead Service Tools

- `getCompanyProfile` - Abruf von Basisinformationen für ein Unternehmen
- `validateLeadData` - Prüfung formaler und qualitativer Kriterien
- `getEngagementHistory` - Zusammenführung bisheriger Interaktionen
- `calculateLeadScore` - Bewertung anhand historischer Muster

### Opportunity Service Tools

- `getPricingInformation` - Live-Abfrage von Preisen und Verfügbarkeiten
- `queryProducts` - Abruf von Produktdetails oder Alternativen
- `searchDocumentsForCustomer` - Integration von SharePoint/D3-Inhalten
- `getOpportunityInsights` - Analyse von Aktivität und Abschlusswahrscheinlichkeit

### Quote Service Tools

- `getProductAvailability` - Live-Verfügbarkeiten aus ERP/CPQ
- `calculateDiscountRange` - Orientierung an historischen Preismodellen
- `searchComplianceDocuments` - Bereitstellung notwendiger Unterlagen
- `generateQuoteSummary` - Strukturierte Vorbereitung des Angebotsinhalts
