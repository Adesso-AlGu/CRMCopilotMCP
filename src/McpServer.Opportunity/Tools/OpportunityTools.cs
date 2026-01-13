using ModelContextProtocol.Server;
using System.ComponentModel;
using McpServer.Opportunity.Services;

namespace McpServer.Opportunity.Tools;

[McpServerToolType]
public sealed class OpportunityTools
{
    private readonly ILogger<OpportunityTools> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataverseService _dataverseService;

    public OpportunityTools(
        ILogger<OpportunityTools> logger, 
        IHttpContextAccessor httpContextAccessor,
        IDataverseService dataverseService)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _dataverseService = dataverseService;
    }

    [McpServerTool, Description("Live-Abfrage von Preisen, Rabatten und Verfügbarkeiten von Produkten und Dienstleistungen in Echtzeit. Parameter: opportunityId (GUID oder String-ID). Rückgabe: JSON mit Produktpreisen, Rabatten und Verfügbarkeitsstatus.")]
    public async Task<string> getPricingInformation(string opportunityId)
    {
        _logger.LogInformation("MCP Tool aufgerufen: getPricingInformation mit opportunityId={OpportunityId}", opportunityId);

        try
        {
            // Input-Validierung
            if (string.IsNullOrWhiteSpace(opportunityId))
            {
                var errorResult = new
                {
                    success = false,
                    error = "Ungültiger Parameter: OpportunityId darf nicht leer sein.",
                    timestamp = DateTime.UtcNow
                };
                _logger.LogWarning("MCP Tool getPricingInformation: Ungültige OpportunityId");
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // Simulierte Logik zur Abfrage von Preis- und Verfügbarkeitsinformationen
            // In einer echten Implementierung würde hier eine Datenbankabfrage oder ein externer API-Aufruf erfolgen
            await Task.Delay(100); // Simuliere Async-Operation

            var products = new[]
            {
                new { name = "Produkt A", price = 100.00m, currency = "EUR", available = true, stock = 50, discount = 0 },
                new { name = "Produkt B", price = 200.00m, currency = "EUR", available = false, stock = 0, discount = 10 }
            };

            var result = new
            {
                success = true,
                opportunityId = opportunityId,
                products = products,
                totalValue = products.Sum(p => p.price),
                timestamp = DateTime.UtcNow
            };

            var jsonResult = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("MCP Tool getPricingInformation erfolgreich ausgeführt für opportunityId={OpportunityId}", opportunityId);

            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen von Preisinformationen für Opportunity {OpportunityId}", opportunityId);
            var errorResult = new
            {
                success = false,
                error = $"Fehler beim Abrufen von Preisinformationen: {ex.Message}",
                timestamp = DateTime.UtcNow
            };
            return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool, Description("Abruf von Produktdetails oder Alternativen basierend auf Kundenanforderungen aus CRM/Dataverse. Parameter: opportunityId (GUID oder String-ID). Rückgabe: JSON mit Produktliste, Preisen und Mengen.")]
    public async Task<string> queryProducts(string opportunityId)
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        _logger.LogInformation("MCP Tool aufgerufen: queryProducts | User={UserName} | UserId={UserId} | OpportunityId={OpportunityId}", userName, userId, opportunityId);

        try
        {
            // Input-Validierung
            if (string.IsNullOrWhiteSpace(opportunityId))
            {
                var errorResult = new
                {
                    success = false,
                    error = "Ungültiger Parameter: OpportunityId darf nicht leer sein.",
                    timestamp = DateTime.UtcNow
                };
                _logger.LogWarning("MCP Tool queryProducts: Ungültige OpportunityId");
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // WhoAmI-Abfrage durchführen, um den CRM-Benutzer zu identifizieren
            var whoAmI = await _dataverseService.WhoAmIAsync();
            
            _logger.LogInformation(
                "CRM Benutzer identifiziert | Dataverse UserId={DataverseUserId} | BusinessUnitId={BusinessUnitId}",
                whoAmI.UserId,
                whoAmI.BusinessUnitId
            );

            // Produkte aus CRM abfragen
            var products = await _dataverseService.QueryProductsAsync(opportunityId);

            // Produktliste als strukturiertes Array aufbauen
            var productList = new List<object>();
            foreach (var product in products.Entities)
            {
                var productName = product.Contains("productdescription") 
                    ? product["productdescription"].ToString() 
                    : "Unbekanntes Produkt";
                var priceValue = product.Contains("priceperunit") 
                    ? product.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("priceperunit")?.Value 
                    : (decimal?)null;
                var quantityValue = product.Contains("quantity") 
                    ? product.GetAttributeValue<decimal?>("quantity") 
                    : null;

                productList.Add(new
                {
                    name = productName,
                    price = priceValue,
                    quantity = quantityValue,
                    currency = "EUR"
                });
            }

            var result = new
            {
                success = true,
                opportunityId = opportunityId,
                crmUserId = whoAmI.UserId.ToString(),
                businessUnitId = whoAmI.BusinessUnitId.ToString(),
                totalProducts = products.Entities.Count,
                products = productList,
                timestamp = DateTime.UtcNow
            };

            var jsonResult = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("MCP Tool queryProducts erfolgreich ausgeführt | User={UserName} | OpportunityId={OpportunityId}", userName, opportunityId);

            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen von CRM-Daten für Opportunity {OpportunityId}", opportunityId);
            var errorResult = new
            {
                success = false,
                error = $"Fehler beim Abrufen von Produktdaten: {ex.Message}",
                timestamp = DateTime.UtcNow
            };
            return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool, Description("Integration von SharePoint/D3-Inhalten und Suche nach relevanten Kundendokumenten. Parameter: opportunityId (GUID oder String-ID). Rückgabe: JSON mit Liste gefundener Dokumente inkl. Metadaten.")]
    public async Task<string> searchDocumentsForCustomer(string opportunityId)
    {
        _logger.LogInformation("MCP Tool aufgerufen: searchDocumentsForCustomer mit opportunityId={OpportunityId}", opportunityId);

        try
        {
            // Input-Validierung
            if (string.IsNullOrWhiteSpace(opportunityId))
            {
                var errorResult = new
                {
                    success = false,
                    error = "Ungültiger Parameter: OpportunityId darf nicht leer sein.",
                    timestamp = DateTime.UtcNow
                };
                _logger.LogWarning("MCP Tool searchDocumentsForCustomer: Ungültige OpportunityId");
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // Simulierte Logik zur Suche von Dokumenten in SharePoint/D3
            // In einer echten Implementierung würde hier eine API-Abfrage an SharePoint/D3 erfolgen
            await Task.Delay(100); // Simuliere Async-Operation

            var documents = new[]
            {
                new { name = "Angebot_2026.pdf", type = "PDF", size = "2.5 MB", modified = DateTime.UtcNow.AddDays(-5), url = "/documents/angebot.pdf" },
                new { name = "Produktkatalog.xlsx", type = "Excel", size = "1.2 MB", modified = DateTime.UtcNow.AddDays(-10), url = "/documents/katalog.xlsx" },
                new { name = "Kundenreferenz.docx", type = "Word", size = "0.8 MB", modified = DateTime.UtcNow.AddDays(-15), url = "/documents/referenz.docx" }
            };

            var result = new
            {
                success = true,
                opportunityId = opportunityId,
                totalDocuments = documents.Length,
                documents = documents,
                sources = new[] { "SharePoint", "D3" },
                timestamp = DateTime.UtcNow
            };

            var jsonResult = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("MCP Tool searchDocumentsForCustomer erfolgreich ausgeführt für opportunityId={OpportunityId}, Anzahl Dokumente={Count}", opportunityId, documents.Length);

            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Suchen von Dokumenten für Opportunity {OpportunityId}", opportunityId);
            var errorResult = new
            {
                success = false,
                error = $"Fehler bei der Dokumentensuche: {ex.Message}",
                timestamp = DateTime.UtcNow
            };
            return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool, Description("Analyse von Aktivität, Historie und Abschlusswahrscheinlichkeit einer Opportunity mit KI-gestützten Empfehlungen. Parameter: opportunityId (GUID oder String-ID). Rückgabe: Detaillierte Insights mit Finanzmetriken, Status und Handlungsempfehlungen.")]
    public async Task<string> getOpportunityInsights(string opportunityId)
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        _logger.LogInformation("MCP Tool aufgerufen: getOpportunityInsights | User={UserName} | UserId={UserId} | OpportunityId={OpportunityId}", userName, userId, opportunityId);

        try
        {
            // Input-Validierung
            if (string.IsNullOrWhiteSpace(opportunityId))
            {
                var errorResult = new
                {
                    success = false,
                    error = "Ungültiger Parameter: OpportunityId darf nicht leer sein.",
                    timestamp = DateTime.UtcNow
                };
                _logger.LogWarning("MCP Tool getOpportunityInsights: Ungültige OpportunityId");
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // WhoAmI-Abfrage durchführen, um den CRM-Benutzer zu identifizieren
            var whoAmI = await _dataverseService.WhoAmIAsync();
            
            _logger.LogInformation(
                "CRM Benutzer identifiziert | Dataverse UserId={DataverseUserId} | BusinessUnitId={BusinessUnitId}",
                whoAmI.UserId,
                whoAmI.BusinessUnitId
            );

            // Opportunity-Daten aus CRM abfragen
            var opportunity = await _dataverseService.QueryOpportunityAsync(opportunityId);

            // Produkte der Opportunity abfragen für Gesamtwert-Berechnung
            var products = await _dataverseService.QueryProductsAsync(opportunityId);

            // Daten extrahieren
            var opportunityName = opportunity.Contains("name") ? opportunity["name"].ToString() : "Unbekannte Opportunity";
            var estimatedValue = opportunity.Contains("estimatedvalue") 
                ? opportunity.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("estimatedvalue")?.Value ?? 0 
                : 0;
            var closeProbability = opportunity.Contains("closeprobability") 
                ? opportunity.GetAttributeValue<int>("closeprobability") 
                : 0;
            var stepName = opportunity.Contains("stepname") ? opportunity["stepname"].ToString() : "Nicht definiert";
            var estimatedCloseDate = opportunity.Contains("estimatedclosedate") 
                ? opportunity.GetAttributeValue<DateTime>("estimatedclosedate").ToString("dd.MM.yyyy") 
                : "Nicht festgelegt";
            var statusCode = opportunity.Contains("statuscode") 
                ? opportunity.FormattedValues.Contains("statuscode") 
                    ? opportunity.FormattedValues["statuscode"] 
                    : opportunity["statuscode"].ToString()
                : "Unbekannt";
            var createdOn = opportunity.Contains("createdon") 
                ? opportunity.GetAttributeValue<DateTime>("createdon").ToString("dd.MM.yyyy HH:mm") 
                : "Unbekannt";
            var modifiedOn = opportunity.Contains("modifiedon") 
                ? opportunity.GetAttributeValue<DateTime>("modifiedon").ToString("dd.MM.yyyy HH:mm") 
                : "Unbekannt";

            // Produkt-Gesamtwert berechnen
            decimal totalProductValue = 0;
            foreach (var product in products.Entities)
            {
                if (product.Contains("baseamount"))
                {
                    totalProductValue += product.GetAttributeValue<Microsoft.Xrm.Sdk.Money>("baseamount")?.Value ?? 0;
                }
            }

            // Empfehlungen basierend auf Daten generieren
            var recommendations = new List<string>();
            if (closeProbability >= 70)
            {
                recommendations.Add("Hohe Abschlusswahrscheinlichkeit - priorisieren Sie diese Opportunity!");
                recommendations.Add("Empfehlung: Abschluss zeitnah anstreben, alle Hindernisse beseitigen.");
            }
            else if (closeProbability >= 40)
            {
                recommendations.Add("Mittlere Abschlusswahrscheinlichkeit - verstärkte Betreuung empfohlen.");
                recommendations.Add("Empfehlung: Kundenkontakt intensivieren, individuelle Lösung präsentieren.");
            }
            else
            {
                recommendations.Add("Niedrige Abschlusswahrscheinlichkeit - kritische Prüfung erforderlich.");
                recommendations.Add("Empfehlung: Kundenbedarfe neu evaluieren, ggf. Angebot anpassen.");
            }
            
            if (estimatedValue > 100000)
            {
                recommendations.Add("Hochwertige Opportunity - Management-Attention empfohlen.");
            }
            
            if (products.Entities.Count == 0)
            {
                recommendations.Add("Keine Produkte hinterlegt - bitte Angebot vervollständigen!");
            }

            // Strukturiertes JSON-Objekt erstellen
            var result = new
            {
                success = true,
                opportunityId = opportunityId,
                opportunityName = opportunityName,
                crmUser = new
                {
                    userId = whoAmI.UserId.ToString(),
                    businessUnitId = whoAmI.BusinessUnitId.ToString()
                },
                financials = new
                {
                    estimatedValue = estimatedValue,
                    productValue = totalProductValue,
                    closeProbability = closeProbability,
                    currency = "EUR"
                },
                status = new
                {
                    statusCode = statusCode,
                    salesPhase = stepName,
                    estimatedCloseDate = estimatedCloseDate
                },
                activity = new
                {
                    createdOn = createdOn,
                    modifiedOn = modifiedOn,
                    productCount = products.Entities.Count
                },
                recommendations = recommendations,
                timestamp = DateTime.UtcNow
            };

            var jsonResult = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("MCP Tool getOpportunityInsights erfolgreich ausgeführt | User={UserName} | OpportunityId={OpportunityId}", userName, opportunityId);

            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen von Opportunity-Insights für {OpportunityId}", opportunityId);
            var errorResult = new
            {
                success = false,
                error = $"Fehler beim Abrufen von Opportunity-Insights: {ex.Message}",
                timestamp = DateTime.UtcNow
            };
            return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }
}
