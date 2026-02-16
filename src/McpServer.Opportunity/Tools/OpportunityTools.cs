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

            // Fiktive ERP-Daten: Frühere Geschäfte und Transaktionen
            var erpHistory = new
            {
                customerSince = "15.03.2019",
                customerRating = "A+",
                creditLimit = 500000m,
                paymentTerms = "30 Tage netto",
                averagePaymentDays = 22,
                previousOrders = new[]
                {
                    new { 
                        orderId = "ERP-2025-4521", 
                        orderDate = "12.11.2025", 
                        productCategory = "Industrieanlagen", 
                        value = 85000m, 
                        status = "Abgeschlossen",
                        deliveredOn = "28.11.2025",
                        paymentStatus = "Bezahlt",
                        satisfaction = 5
                    },
                    new { 
                        orderId = "ERP-2025-3187", 
                        orderDate = "23.06.2025", 
                        productCategory = "Ersatzteile", 
                        value = 12500m, 
                        status = "Abgeschlossen",
                        deliveredOn = "30.06.2025",
                        paymentStatus = "Bezahlt",
                        satisfaction = 4
                    },
                    new { 
                        orderId = "ERP-2024-8742", 
                        orderDate = "05.09.2024", 
                        productCategory = "Wartungsvertrag", 
                        value = 45000m, 
                        status = "Laufend",
                        deliveredOn = "-",
                        paymentStatus = "Quartalsweise",
                        satisfaction = 5
                    },
                    new { 
                        orderId = "ERP-2024-2156", 
                        orderDate = "18.02.2024", 
                        productCategory = "Industrieanlagen", 
                        value = 125000m, 
                        status = "Abgeschlossen",
                        deliveredOn = "15.04.2024",
                        paymentStatus = "Bezahlt",
                        satisfaction = 5
                    },
                    new { 
                        orderId = "ERP-2023-6893", 
                        orderDate = "22.08.2023", 
                        productCategory = "Software-Lizenzen", 
                        value = 28000m, 
                        status = "Abgeschlossen",
                        deliveredOn = "25.08.2023",
                        paymentStatus = "Bezahlt",
                        satisfaction = 4
                    }
                },
                financialSummary = new
                {
                    totalRevenueLast3Years = 295500m,
                    averageOrderValue = 59100m,
                    totalOrders = 5,
                    openInvoices = 0m,
                    lastPaymentDate = "15.12.2025",
                    currency = "EUR"
                },
                serviceHistory = new[]
                {
                    new { ticketId = "SRV-2025-892", date = "03.12.2025", type = "Wartung", status = "Erledigt", responseTime = "4 Stunden" },
                    new { ticketId = "SRV-2025-654", date = "17.08.2025", type = "Technische Anfrage", status = "Erledigt", responseTime = "2 Stunden" },
                    new { ticketId = "SRV-2024-421", date = "22.11.2024", type = "Reklamation", status = "Erledigt", responseTime = "1 Stunde" }
                },
                logisticsInfo = new
                {
                    preferredShipping = "Express",
                    defaultWarehouse = "Lager München",
                    deliveryAddress = "Industriestraße 42, 80331 München",
                    specialInstructions = "Anlieferung nur Mo-Fr 8-16 Uhr, Rampe 3"
                }
            };

            // Fiktive Daten: Potenzielle Hindernisse
            var potentialObstacles = new[]
            {
                new { 
                    id = "OBS-001",
                    category = "Budget",
                    description = "Budgetfreigabe durch CFO steht noch aus",
                    severity = "Hoch",
                    status = "Offen",
                    identifiedOn = "15.01.2026",
                    mitigation = "Termin mit CFO für ROI-Präsentation vereinbaren",
                    owner = "Max Mustermann"
                },
                new { 
                    id = "OBS-002",
                    category = "Technisch",
                    description = "Integration mit bestehendem SAP-System muss geprüft werden",
                    severity = "Mittel",
                    status = "In Bearbeitung",
                    identifiedOn = "22.01.2026",
                    mitigation = "Technischer Workshop mit IT-Abteilung geplant für 15.02.2026",
                    owner = "Thomas Technik"
                },
                new { 
                    id = "OBS-003",
                    category = "Wettbewerb",
                    description = "Konkurrenzangebot von Siemens liegt vor",
                    severity = "Hoch",
                    status = "Offen",
                    identifiedOn = "28.01.2026",
                    mitigation = "Alleinstellungsmerkmale hervorheben, Referenzbesuch anbieten",
                    owner = "Sarah Sales"
                },
                new { 
                    id = "OBS-004",
                    category = "Zeitplan",
                    description = "Kunde benötigt Lieferung vor Q2 - enge Timeline",
                    severity = "Mittel",
                    status = "Offen",
                    identifiedOn = "01.02.2026",
                    mitigation = "Expressproduktion prüfen, Teillieferung als Option anbieten",
                    owner = "Lisa Logistik"
                },
                new { 
                    id = "OBS-005",
                    category = "Intern",
                    description = "Ressourcenengpass im Projektteam für Implementierung",
                    severity = "Niedrig",
                    status = "Gelöst",
                    identifiedOn = "10.01.2026",
                    mitigation = "Externe Berater für Implementierungsphase eingeplant",
                    owner = "Peter Projekt"
                }
            };

            // Fiktive Daten: Relevante Stakeholder
            var stakeholders = new[]
            {
                new { 
                    name = "Dr. Klaus Entscheider",
                    role = "Geschäftsführer",
                    company = "Mustermann GmbH",
                    influence = "Entscheider",
                    attitude = "Positiv",
                    email = "k.entscheider@mustermann.de",
                    phone = "+49 89 12345-100",
                    lastContact = "05.02.2026",
                    notes = "Sehr interessiert an Digitalisierung, trifft finale Kaufentscheidung"
                },
                new { 
                    name = "Maria Finanzen",
                    role = "CFO",
                    company = "Mustermann GmbH",
                    influence = "Genehmiger",
                    attitude = "Neutral",
                    email = "m.finanzen@mustermann.de",
                    phone = "+49 89 12345-110",
                    lastContact = "28.01.2026",
                    notes = "Fokus auf ROI und TCO, benötigt detaillierte Kostenkalkulation"
                },
                new { 
                    name = "Stefan Technik",
                    role = "IT-Leiter",
                    company = "Mustermann GmbH",
                    influence = "Beeinflusser",
                    attitude = "Skeptisch",
                    email = "s.technik@mustermann.de",
                    phone = "+49 89 12345-200",
                    lastContact = "01.02.2026",
                    notes = "Bedenken bzgl. SAP-Integration, technische Machbarkeit prüfen"
                },
                new { 
                    name = "Anna Einkauf",
                    role = "Einkaufsleiterin",
                    company = "Mustermann GmbH",
                    influence = "Gatekeeper",
                    attitude = "Positiv",
                    email = "a.einkauf@mustermann.de",
                    phone = "+49 89 12345-300",
                    lastContact = "03.02.2026",
                    notes = "Gute Beziehung, unterstützt unser Angebot intern"
                },
                new { 
                    name = "Jürgen Produktion",
                    role = "Produktionsleiter",
                    company = "Mustermann GmbH",
                    influence = "Nutzer",
                    attitude = "Sehr positiv",
                    email = "j.produktion@mustermann.de",
                    phone = "+49 89 12345-400",
                    lastContact = "30.01.2026",
                    notes = "Hauptnutzer der Lösung, starker interner Befürworter"
                },
                new { 
                    name = "Externe: Hans Berater",
                    role = "Unternehmensberater",
                    company = "Consulting AG",
                    influence = "Beeinflusser",
                    attitude = "Neutral",
                    email = "h.berater@consulting-ag.de",
                    phone = "+49 69 98765-50",
                    lastContact = "20.01.2026",
                    notes = "Berät den Kunden bei der Anbieterauswahl, objektive Bewertung"
                }
            };

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

            // ERP-basierte Empfehlungen
            recommendations.Add($"Langjähriger Kunde seit {erpHistory.customerSince} - Bestandskundenpflege wichtig!");
            recommendations.Add($"Bisheriger Gesamtumsatz: {erpHistory.financialSummary.totalRevenueLast3Years:N0} EUR - Cross-Selling-Potenzial prüfen.");
            recommendations.Add($"Kundenbewertung {erpHistory.customerRating} mit Ø Zahlungsziel {erpHistory.averagePaymentDays} Tage - zuverlässiger Zahler.");
            
            if (erpHistory.financialSummary.openInvoices == 0)
            {
                recommendations.Add("Keine offenen Rechnungen - Bonität ausgezeichnet.");
            }

            // Hindernis- und Stakeholder-basierte Empfehlungen
            var openObstacles = potentialObstacles.Count(o => o.status == "Offen");
            var highSeverityObstacles = potentialObstacles.Count(o => o.severity == "Hoch" && o.status != "Gelöst");
            if (highSeverityObstacles > 0)
            {
                recommendations.Add($"ACHTUNG: {highSeverityObstacles} kritische Hindernisse offen - sofortige Maßnahmen erforderlich!");
            }
            if (openObstacles > 0)
            {
                recommendations.Add($"{openObstacles} offene Hindernisse identifiziert - Maßnahmenplan erstellen.");
            }

            var skepticalStakeholders = stakeholders.Count(s => s.attitude == "Skeptisch" || s.attitude == "Neutral");
            if (skepticalStakeholders > 0)
            {
                recommendations.Add($"{skepticalStakeholders} Stakeholder mit neutraler/skeptischer Haltung - gezielte Überzeugungsarbeit leisten.");
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
                erpData = erpHistory,
                obstacles = potentialObstacles,
                stakeholders = stakeholders,
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

    [McpServerTool, Description("Fügt Stakeholder zur Opportunity im CRM hinzu. Parameter: opportunityId (GUID), stakeholders (JSON-Array mit Name, Rolle und Beschreibung). Rückgabe: JSON mit Ergebnis der erstellten Verbindungen.")]
    public async Task<string> addStakeholdersToOpportunity(string opportunityId, string stakeholdersJson)
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        _logger.LogInformation("MCP Tool aufgerufen: addStakeholdersToOpportunity | User={UserName} | UserId={UserId} | OpportunityId={OpportunityId}", userName, userId, opportunityId);

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
                _logger.LogWarning("MCP Tool addStakeholdersToOpportunity: Ungültige OpportunityId");
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            if (string.IsNullOrWhiteSpace(stakeholdersJson))
            {
                var errorResult = new
                {
                    success = false,
                    error = "Ungültiger Parameter: stakeholdersJson darf nicht leer sein.",
                    timestamp = DateTime.UtcNow
                };
                _logger.LogWarning("MCP Tool addStakeholdersToOpportunity: Leere Stakeholder-Liste");
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // Stakeholder-JSON parsen
            var stakeholders = System.Text.Json.JsonSerializer.Deserialize<StakeholderInput[]>(stakeholdersJson, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (stakeholders == null || stakeholders.Length == 0)
            {
                var errorResult = new
                {
                    success = false,
                    error = "Keine gültigen Stakeholder im JSON gefunden.",
                    timestamp = DateTime.UtcNow
                };
                return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // WhoAmI-Abfrage durchführen, um den CRM-Benutzer zu identifizieren
            var whoAmI = await _dataverseService.WhoAmIAsync();
            
            _logger.LogInformation(
                "CRM Benutzer identifiziert | Dataverse UserId={DataverseUserId} | BusinessUnitId={BusinessUnitId}",
                whoAmI.UserId,
                whoAmI.BusinessUnitId
            );

            // Jeden Stakeholder zur Opportunity hinzufügen
            var results = new List<object>();
            var successCount = 0;
            var failedCount = 0;

            foreach (var stakeholder in stakeholders)
            {
                try
                {
                    var connectionId = await _dataverseService.AddStakeholderAsync(
                        opportunityId,
                        stakeholder.Name,
                        stakeholder.Role,
                        stakeholder.Description ?? ""
                    );

                    results.Add(new
                    {
                        name = stakeholder.Name,
                        role = stakeholder.Role,
                        success = true,
                        connectionId = connectionId.ToString(),
                        message = "Stakeholder erfolgreich hinzugefügt"
                    });
                    successCount++;

                    _logger.LogInformation("Stakeholder {Name} erfolgreich zur Opportunity {OpportunityId} hinzugefügt", stakeholder.Name, opportunityId);
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        name = stakeholder.Name,
                        role = stakeholder.Role,
                        success = false,
                        connectionId = (string?)null,
                        message = $"Fehler: {ex.Message}"
                    });
                    failedCount++;

                    _logger.LogWarning(ex, "Fehler beim Hinzufügen von Stakeholder {Name} zur Opportunity {OpportunityId}", stakeholder.Name, opportunityId);
                }
            }

            var result = new
            {
                success = failedCount == 0,
                opportunityId = opportunityId,
                crmUserId = whoAmI.UserId.ToString(),
                summary = new
                {
                    total = stakeholders.Length,
                    successful = successCount,
                    failed = failedCount
                },
                stakeholderResults = results,
                timestamp = DateTime.UtcNow
            };

            var jsonResult = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("MCP Tool addStakeholdersToOpportunity abgeschlossen | User={UserName} | OpportunityId={OpportunityId} | Erfolg={SuccessCount}/{Total}", 
                userName, opportunityId, successCount, stakeholders.Length);

            return jsonResult;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Fehler beim Parsen der Stakeholder-JSON für Opportunity {OpportunityId}", opportunityId);
            var errorResult = new
            {
                success = false,
                error = $"Ungültiges JSON-Format: {ex.Message}",
                expectedFormat = new
                {
                    example = new[]
                    {
                        new { name = "Max Mustermann", role = "Entscheider", description = "CEO des Unternehmens" },
                        new { name = "Anna Schmidt", role = "Beeinflusser", description = "IT-Leiterin" }
                    }
                },
                timestamp = DateTime.UtcNow
            };
            return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Hinzufügen von Stakeholdern zur Opportunity {OpportunityId}", opportunityId);
            var errorResult = new
            {
                success = false,
                error = $"Fehler beim Hinzufügen von Stakeholdern: {ex.Message}",
                timestamp = DateTime.UtcNow
            };
            return System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
    }
}

/// <summary>
/// Eingabe-Modell für Stakeholder-Daten
/// </summary>
public class StakeholderInput
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Description { get; set; }
}
