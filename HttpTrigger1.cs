using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Core;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Company.Function.Models;

namespace Company.Function;

public class Triggers
{
    private readonly ILogger<Triggers> _logger;
    private readonly TelemetryClient _telemetryClient;

    public Triggers(ILogger<Triggers> logger, TelemetryClient telemetryClient)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    private async Task<IActionResult?> ValidateApiKeyAsync(HttpRequest req)
    {
        req.Headers.TryGetValue("x-api-key", out var apiKeyValues);
        var apiKey = apiKeyValues.ToString();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("x-api-key header missing");
            return new UnauthorizedResult();
        }

        try
        {
            var credential = new DefaultAzureCredential();
            var client = new SecretClient(new Uri("https://finalkeyvault.vault.azure.net/"), credential);
            var secret = await client.GetSecretAsync("APIKEY");
            var expectedKey = secret.Value.Value;
            if (string.IsNullOrEmpty(expectedKey) || expectedKey != apiKey)
            {
                _logger.LogWarning("x-api-key mismatch");
                return new UnauthorizedResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve API key from Key Vault. Error: {Error}", ex.Message);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        return null;
    }

    private async Task<SqlConnection> OpenSqlConnectionAsync()
    {
        var server = "rnovarafinaldatabaseserver.database.windows.net";
        var database = "novararFinalSQLDatabase";

        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://database.windows.net/.default" }));

        var connection = new SqlConnection($"Server=tcp:{server},1433;Initial Catalog={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
        connection.AccessToken = token.Token;
        await connection.OpenAsync();
        return connection;
    }

    private static Game MapGame(SqlDataReader reader)
    {
        return new Game
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            Upc = reader.GetString(2),
            Data = reader.IsDBNull(3) ? null : reader.GetString(3),
            Year = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            Publisher = reader.IsDBNull(5) ? null : reader.GetString(5)
        };
    }

    [Function("GetGames")]
    public async Task<IActionResult> RunGetGames([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "games")] HttpRequest req)
    {
        _logger.LogInformation("Processing GET /games");

        var results = new List<Game>();

        using var connection = await OpenSqlConnectionAsync();
        using var command = new SqlCommand("SELECT Id, Title, Upc, Data, Year, Publisher FROM Games", connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(MapGame(reader));
        }

        _logger.LogInformation("Returned {Count} games", results.Count);
        return new OkObjectResult(results);
    }

    [Function("AddGames")]
    public async Task<IActionResult> RunAddGames([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "games")] HttpRequest req)
    {
        _logger.LogInformation("Processing POST /games");

        try
        {
            var authResult = await ValidateApiKeyAsync(req);
            if (authResult != null) return authResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateApiKeyAsync");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return new BadRequestObjectResult("Request body is required.");
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(body);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("Invalid JSON payload.");
        }

        var title = payload.Value<string>("title") ?? payload.Value<string>("Title");
        var upc = payload.Value<string>("upc") ?? payload.Value<string>("Upc");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(upc))
        {
            return new BadRequestObjectResult("Both title and upc are required.");
        }

        try
        {
            _logger.LogInformation("Attempting to open SQL connection...");
            using var connection = await OpenSqlConnectionAsync();
            _logger.LogInformation("SQL connection opened successfully");
            
            var year = payload.Value<int?>("year") ?? payload.Value<int?>("Year");
            var publisher = payload.Value<string>("publisher") ?? payload.Value<string>("Publisher");
            
            using var command = new SqlCommand("INSERT INTO Games (Title, Upc, Data, Year, Publisher) VALUES (@title, @upc, @data, @year, @publisher)", connection);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@upc", upc);
            command.Parameters.AddWithValue("@data", body);
            command.Parameters.AddWithValue("@year", year ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@publisher", publisher ?? (object)DBNull.Value);

            try
            {
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Inserted game with UPC {Upc}", upc);
                return new OkObjectResult("Item added successfully");
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                _logger.LogWarning(ex, "Duplicate UPC {Upc}", upc);
                return new ConflictObjectResult("A game with this UPC already exists.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SQL connection. Error: {Message}", ex.Message);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [Function("UpdateGames")]
    public async Task<IActionResult> RunUpdateGames([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "games")] HttpRequest req)
    {
        _logger.LogInformation("Processing PUT /games");

        var authResult = await ValidateApiKeyAsync(req);
        if (authResult != null) return authResult;

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return new BadRequestObjectResult("Request body is required.");
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(body);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("Invalid JSON payload.");
        }

        var title = payload.Value<string>("title") ?? payload.Value<string>("Title");
        var upc = payload.Value<string>("upc") ?? payload.Value<string>("Upc");

        if (string.IsNullOrWhiteSpace(upc))
        {
            return new BadRequestObjectResult("UPC is required to update a game.");
        }

        var year = payload.Value<int?>("year") ?? payload.Value<int?>("Year");
        var publisher = payload.Value<string>("publisher") ?? payload.Value<string>("Publisher");
        
        using var connection = await OpenSqlConnectionAsync();
        using var command = new SqlCommand("UPDATE Games SET Title = @title, Data = @data, Year = @year, Publisher = @publisher WHERE Upc = @upc", connection);
        command.Parameters.AddWithValue("@title", (object?)title ?? DBNull.Value);
        command.Parameters.AddWithValue("@upc", upc);
        command.Parameters.AddWithValue("@data", body);
        command.Parameters.AddWithValue("@year", year ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@publisher", publisher ?? (object)DBNull.Value);

        var rows = await command.ExecuteNonQueryAsync();
        if (rows == 0)
        {
            return new NotFoundObjectResult($"Game with UPC {upc} not found.");
        }

        _logger.LogInformation("Updated game with UPC {Upc}", upc);
        return new OkObjectResult("Game updated successfully");
    }

    [Function("CountGames")]
    public async Task<IActionResult> RunCountGames([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "games/count")] HttpRequest req)
    {
        _logger.LogInformation("Processing GET /games/count");

        using var connection = await OpenSqlConnectionAsync();
        using var command = new SqlCommand("SELECT COUNT(*) FROM Games", connection);
        var result = await command.ExecuteScalarAsync();
        var count = result is null ? 0 : Convert.ToInt32(result);

        return new OkObjectResult($"Total games: {count}");
    }

    [Function("GetGameByUPC")]
    public async Task<IActionResult> RunGetGameByUPC([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "games/{upc}")] HttpRequest req, string upc)
    {
        _logger.LogInformation("Processing GET /games/{Upc}", upc);

        using var connection = await OpenSqlConnectionAsync();
        using var command = new SqlCommand("SELECT Id, Title, Upc, Data, Year, Publisher FROM Games WHERE Upc = @upc", connection);
        command.Parameters.AddWithValue("@upc", upc);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new OkObjectResult(MapGame(reader));
        }

        return new NotFoundObjectResult($"Game with UPC {upc} not found");
    }

    [Function("DeleteGames")]
    public async Task<IActionResult> RunDeleteGames([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "games/{upc}")] HttpRequest req, string upc)
    {
        _logger.LogInformation("Processing DELETE /games/{Upc}", upc);

        var authResult = await ValidateApiKeyAsync(req);
        if (authResult != null) return authResult;

        using var connection = await OpenSqlConnectionAsync();
        using var command = new SqlCommand("DELETE FROM Games WHERE Upc = @upc", connection);
        command.Parameters.AddWithValue("@upc", upc);

        var rows = await command.ExecuteNonQueryAsync();
        if (rows == 0)
        {
            return new NotFoundObjectResult($"Game with UPC {upc} not found");
        }

        _logger.LogInformation("Deleted game with UPC {Upc}", upc);
        return new OkObjectResult($"Game with UPC {upc} deleted successfully");
    }

    [Function("ValidateGames")]
    public async Task<IActionResult> RunValidateGames([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "games/validate")] HttpRequest req)
    {
        _logger.LogInformation("Validation function triggered - Starting game validation process");
        _telemetryClient.TrackTrace("Validation function triggered - Starting game validation process", SeverityLevel.Information);
        _logger.LogInformation("Processing PATCH /games/validate");
        _telemetryClient.TrackTrace("Processing PATCH /games/validate", SeverityLevel.Information);

        var authResult = await ValidateApiKeyAsync(req);
        if (authResult != null) return authResult;

        int updatedCount = 0;
        int archivedCount = 0;
        int needsReviewCount = 0;
        var validationTimestamp = DateTime.UtcNow;

        try
        {
            using var connection = await OpenSqlConnectionAsync();
            _logger.LogInformation("SQL connection established. Retrieving games for validation...");
            _telemetryClient.TrackTrace("SQL connection established. Retrieving games for validation...", SeverityLevel.Information);
            
            using var selectCommand = new SqlCommand("SELECT Id, Title, Upc, Data, Year, Publisher FROM Games", connection);
            using var reader = await selectCommand.ExecuteReaderAsync();

            var gamesToUpdate = new List<(int Id, string NewData)>();
            int totalGamesProcessed = 0;

            while (await reader.ReadAsync())
            {
                totalGamesProcessed++;
                var game = MapGame(reader);
                var needsUpdate = false;
                var dataJson = string.IsNullOrEmpty(game.Data) ? "{}" : game.Data;
                var currentYear = DateTime.UtcNow.Year;
                
                JObject? dataObj;
                try
                {
                    dataObj = JObject.Parse(dataJson);
                }
                catch
                {
                    dataObj = new JObject();
                    needsUpdate = true;
                }

                if (game.Year.HasValue && game.Year < currentYear - 10)
                {
                    dataObj["archived"] = true;
                    needsUpdate = true;
                    archivedCount++;
                    var archivedMsg = $"Game {game.Id} ({game.Title}) marked as archived - released {game.Year}";
                    _logger.LogInformation(archivedMsg);
                    _telemetryClient.TrackTrace(archivedMsg, SeverityLevel.Information);
                }

                if (string.IsNullOrWhiteSpace(game.Publisher))
                {
                    dataObj["needsReview"] = true;
                    dataObj["validationReason"] = "Missing publisher";
                    needsUpdate = true;
                    needsReviewCount++;
                    var needsReviewMsg = $"Game {game.Id} ({game.Title}) marked as needsReview - missing publisher";
                    _logger.LogInformation(needsReviewMsg);
                    _telemetryClient.TrackTrace(needsReviewMsg, SeverityLevel.Information);
                }

                if (string.IsNullOrWhiteSpace(game.Title) || string.IsNullOrWhiteSpace(game.Upc))
                {
                    dataObj["needsReview"] = true;
                    dataObj["validationReason"] = "Missing title or UPC";
                    needsUpdate = true;
                    needsReviewCount++;
                    var missingDataMsg = $"Game {game.Id} marked as needsReview - missing title or UPC";
                    _logger.LogInformation(missingDataMsg);
                    _telemetryClient.TrackTrace(missingDataMsg, SeverityLevel.Information);
                }

                dataObj["validatedOn"] = validationTimestamp.ToString("O");
                needsUpdate = true;

                if (needsUpdate)
                {
                    gamesToUpdate.Add((game.Id, dataObj.ToString()));
                }
            }

            reader.Close();
            var processedMsg = $"Processed {totalGamesProcessed} games. Found {gamesToUpdate.Count} games requiring updates";
            _logger.LogInformation(processedMsg);
            _telemetryClient.TrackTrace(processedMsg, SeverityLevel.Information);

            foreach (var (id, newData) in gamesToUpdate)
            {
                using var updateCommand = new SqlCommand("UPDATE Games SET Data = @data WHERE Id = @id", connection);
                updateCommand.Parameters.AddWithValue("@data", newData);
                updateCommand.Parameters.AddWithValue("@id", id);
                await updateCommand.ExecuteNonQueryAsync();
                updatedCount++;
            }

            var completedMsg = $"Validation completed successfully. Updated {updatedCount} games. Archived: {archivedCount}, Needs Review: {needsReviewCount}";
            _logger.LogInformation(completedMsg);
            _telemetryClient.TrackTrace(completedMsg, SeverityLevel.Information);
            
            var reviewMsg = $"{updatedCount} items marked as late or needing review";
            _logger.LogInformation(reviewMsg);
            _telemetryClient.TrackTrace(reviewMsg, SeverityLevel.Information);
            
            var response = new
            {
                updatedCount = updatedCount,
                archivedCount = archivedCount,
                needsReviewCount = needsReviewCount,
                timestamp = validationTimestamp.ToString("O")
            };

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}