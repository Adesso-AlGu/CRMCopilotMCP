using Azure.Core;
using Azure.Identity;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;

namespace McpServer.Opportunity.Services;

public interface IDataverseService
{
    Task<WhoAmIResponse> WhoAmIAsync();
    Task<EntityCollection> QueryProductsAsync(string opportunityId);
    Task<Entity> QueryOpportunityAsync(string opportunityId);
}

public class DataverseService : IDataverseService
{
    private readonly ILogger<DataverseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DataverseService(
        ILogger<DataverseService> logger,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Erstellt einen ServiceClient mit On-Behalf-Of (OBO) Flow
    /// Verwendet das Access Token des authentifizierten Benutzers
    /// </summary>
    private async Task<ServiceClient> GetServiceClientAsync()
    {
        try
        {
            // Holen des Access Tokens aus dem HTTP Context
            var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext ist nicht verfügbar");

            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Kein gültiges Bearer Token im Authorization Header gefunden");
            }

            var userAccessToken = authHeader.Substring("Bearer ".Length).Trim();
            _logger.LogInformation("userAccessToken={userAccessToken}", userAccessToken);

            // Azure AD und Dataverse Konfiguration
            var tenantId = _configuration["AzureAd:TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId fehlt in der Konfiguration");
            var clientId = _configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId fehlt in der Konfiguration");
            var clientSecret = Environment.GetEnvironmentVariable("MICROSOFT_PROVIDER_AUTHENTICATION_SECRET") ?? throw new InvalidOperationException("MICROSOFT_PROVIDER_AUTHENTICATION_SECRET fehlt in den Umgebungsvariablen");
            var dataverseUrl = _configuration["Dataverse:Url"] ?? throw new InvalidOperationException("Dataverse:Url fehlt in der Konfiguration");
            var dataverseScope = _configuration["Dataverse:Scope"] ?? throw new InvalidOperationException("Dataverse:Scope fehlt in der Konfiguration");
            var clientSecretSub = clientSecret.Substring(0, Math.Min(5, clientSecret.Length));

            _logger.LogInformation("Starte OBO Flow für Dataverse-Zugriff mit: tenantId={TenantId}, clientId={ClientId}, secret={clientSecretSub}, dataverseUrl={DataverseUrl}, dataverseScope={DataverseScope}", tenantId, clientId, clientSecretSub, dataverseUrl, dataverseScope);

            // On-Behalf-Of Credential erstellen
            var credential = new OnBehalfOfCredential(
                tenantId: tenantId,
                clientId: clientId,
                clientSecret: clientSecret,
                userAssertion: userAccessToken
            );

            // Token für Dataverse anfordern
            var tokenRequestContext = new TokenRequestContext(new[] { dataverseScope });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

            _logger.LogInformation("OBO Token erfolgreich erhalten für Dataverse");

            // ServiceClient mit dem erhaltenen Token erstellen
            var serviceClient = new ServiceClient(
                instanceUrl: new Uri(dataverseUrl),
                tokenProviderFunction: async (string instanceUrl) =>
                {
                    var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                    return token.Token;
                },
                useUniqueInstance: true,
                logger: _logger
            );

            if (!serviceClient.IsReady)
            {
                var lastError = serviceClient.LastError;
                _logger.LogError("ServiceClient ist nicht bereit. Fehler: {Error}", lastError);
                throw new InvalidOperationException($"Fehler beim Verbinden mit Dataverse: {lastError}");
            }

            _logger.LogInformation("ServiceClient erfolgreich erstellt und bereit");
            return serviceClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erstellen des ServiceClient");
            throw;
        }
    }

    /// <summary>
    /// Führt eine WhoAmI-Abfrage durch, um den aktuellen Dataverse-Benutzer zu identifizieren
    /// </summary>
    public async Task<WhoAmIResponse> WhoAmIAsync()
    {
        try
        {
            _logger.LogInformation("Führe WhoAmI-Abfrage durch");

            using var serviceClient = await GetServiceClientAsync();

            var whoAmIRequest = new WhoAmIRequest();
            var response = (WhoAmIResponse)await serviceClient.ExecuteAsync(whoAmIRequest);

            _logger.LogInformation(
                "WhoAmI erfolgreich: UserId={UserId}, BusinessUnitId={BusinessUnitId}, OrganizationId={OrganizationId}",
                response.UserId,
                response.BusinessUnitId,
                response.OrganizationId
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei WhoAmI-Abfrage");
            throw;
        }
    }

    /// <summary>
    /// Beispiel: Abfrage von Produkten basierend auf einer Opportunity
    /// </summary>
    public async Task<EntityCollection> QueryProductsAsync(string opportunityId)
    {
        try
        {
            _logger.LogInformation("Abfrage von Produkten für Opportunity {OpportunityId}", opportunityId);

            using var serviceClient = await GetServiceClientAsync();

            // Beispiel-Query für Opportunity Products (opportunityproduct)
            var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("opportunityproduct")
            {
                ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(
                    "opportunityproductid",
                    "productid",
                    "productdescription",
                    "priceperunit",
                    "quantity",
                    "baseamount"
                ),
                Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression
                {
                    Conditions =
                    {
                        new Microsoft.Xrm.Sdk.Query.ConditionExpression(
                            "opportunityid",
                            Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal,
                            Guid.Parse(opportunityId)
                        )
                    }
                }
            };

            var results = await serviceClient.RetrieveMultipleAsync(query);

            _logger.LogInformation(
                "Produkte erfolgreich abgerufen für Opportunity {OpportunityId}: {Count} Produkte gefunden",
                opportunityId,
                results.Entities.Count
            );

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen von Produkten für Opportunity {OpportunityId}", opportunityId);
            throw;
        }
    }

    /// <summary>
    /// Abfrage einer Opportunity mit allen relevanten Details
    /// </summary>
    public async Task<Entity> QueryOpportunityAsync(string opportunityId)
    {
        try
        {
            _logger.LogInformation("Abfrage von Opportunity {OpportunityId}", opportunityId);

            using var serviceClient = await GetServiceClientAsync();

            var opportunity = await serviceClient.RetrieveAsync(
                "opportunity",
                Guid.Parse(opportunityId),
                new Microsoft.Xrm.Sdk.Query.ColumnSet(
                    "opportunityid",
                    "name",
                    "estimatedvalue",
                    "closeprobability",
                    "stepname",
                    "actualclosedate",
                    "estimatedclosedate",
                    "statuscode",
                    "statecode",
                    "description",
                    "createdon",
                    "modifiedon"
                )
            );

            _logger.LogInformation(
                "Opportunity erfolgreich abgerufen: {OpportunityName}",
                opportunity.Contains("name") ? opportunity["name"] : "Unbekannt"
            );

            return opportunity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen von Opportunity {OpportunityId}", opportunityId);
            throw;
        }
    }
}
