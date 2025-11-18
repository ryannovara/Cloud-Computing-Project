# Cloud Computing Midterm Project

Azure Functions API for game inventory management with secure authentication, automated validation, and comprehensive monitoring.

## 🎯 Project Overview

This project implements a RESTful API using Azure Functions that manages a game inventory database. It demonstrates cloud computing best practices including secure secret management, managed identity authentication, automated workflows, and observability.

## 🏗️ Architecture

```
┌─────────────────┐
│  Azure Logic App│  (Automated Validation)
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────┐     ┌─────────────┐
│  Azure Function │────▶│  Key Vault   │     │ Application │
│      App        │     │  (Secrets)   │     │  Insights   │
└────────┬────────┘     └──────────────┘     └─────────────┘
         │
         ▼
┌─────────────────┐
│  Azure SQL      │
│   Database      │
└─────────────────┘
```

## 🚀 Features

- **Secure API Authentication**: API key stored in Azure Key Vault, accessed via Managed Identity
- **Database Integration**: Azure SQL Database with managed identity authentication
- **Automated Validation**: Logic App triggers batch validation of game records
- **Comprehensive Monitoring**: Application Insights for logging and telemetry
- **CRUD Operations**: Full Create, Read, Update, Delete functionality
- **Custom Validation Logic**: 
  - Archives games older than 10 years
  - Flags games missing publisher information
  - Validates required fields (title, UPC)

## 🛠️ Technologies Used

- **.NET 8.0** - Runtime framework
- **Azure Functions v4** - Serverless compute
- **Azure SQL Database** - Relational database
- **Azure Key Vault** - Secret management
- **Azure Logic Apps** - Workflow automation
- **Application Insights** - Monitoring and logging
- **Managed Identity** - Secure authentication (no passwords!)
- **ADO.NET** - Database access

## 📋 API Endpoints

### Public Endpoints (No Authentication Required)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/games` | Get all games |
| GET | `/api/games/{upc}` | Get game by UPC |
| GET | `/api/games/count` | Get total game count |

### Protected Endpoints (Requires API Key)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/games` | Add a new game |
| PUT | `/api/games` | Update an existing game |
| DELETE | `/api/games/{upc}` | Delete a game by UPC |
| PATCH | `/api/games/validate` | Batch validate all games |

### Request Headers
```
x-api-key: <your-api-key-from-key-vault>
```

### Example Request (POST /api/games)
```json
{
  "title": "The Legend of Zelda: Breath of the Wild",
  "upc": "045496590741",
  "year": 2017,
  "publisher": "Nintendo"
}
```

## 🔐 Security Features

- **Managed Identity**: No connection strings or passwords in code
- **Key Vault Integration**: API keys stored securely in Azure Key Vault
- **HTTPS Only**: All endpoints use encrypted connections
- **API Key Authentication**: Protected endpoints require valid API key

## 📊 Azure Resources

| Resource Type | Name | Purpose |
|---------------|------|---------|
| Function App | `FARnovaraMidterm` | Hosts the API |
| SQL Database | `novararFinalSQLDatabase` | Stores game data |
| SQL Server | `rnovarafinaldatabaseserver` | Database server |
| Key Vault | `finalkeyvault` | Stores API keys |
| Logic App | (Your Logic App Name) | Automated validation |
| Application Insights | (Linked to Function App) | Monitoring |

## 🗄️ Database Schema

```sql
CREATE TABLE Games (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Title NVARCHAR(255) NOT NULL,
    Upc NVARCHAR(50) NOT NULL UNIQUE,
    Data NVARCHAR(MAX),
    Year INT NULL,
    Publisher NVARCHAR(255) NULL
);
```

## 🚀 Setup Instructions

### Prerequisites
- Azure subscription
- .NET 8.0 SDK
- Azure Functions Core Tools
- Azure CLI (optional)

### Local Development

1. **Clone the repository**
   ```bash
   git clone https://github.com/ryannovara/Cloud-Computing-Project.git
   cd Cloud-Computing-Project
   ```

2. **Configure local settings**
   - Create `local.settings.json` (not included in repo for security)
   - Add your local configuration:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
     }
   }
   ```

3. **Set up local database** (optional)
   - Use Azure SQL Database or local SQL Server
   - Run `create_table.sql` to create the schema
   - Run `update_table_schema.sql` to add additional columns

4. **Run locally**
   ```bash
   func start
   ```

### Azure Deployment

1. **Publish to Azure**
   ```bash
   dotnet publish -c Release -o ./publish
   cd publish
   zip -r ../function.zip .
   az functionapp deployment source config-zip \
     --resource-group <your-resource-group> \
     --name FARnovaraMidterm \
     --src ../function.zip
   ```

2. **Configure Managed Identity**
   - Enable System-assigned Managed Identity on Function App
   - Grant Key Vault access: Key Vault → Access policies → Add access policy
   - Grant SQL access: SQL Server → Azure Active Directory → Add Function App identity

3. **Set up Logic App**
   - Create Logic App with Recurrence trigger
   - Add "Get secret" action (Key Vault)
   - Add HTTP action to call `/api/games/validate`
   - Configure managed identity for Logic App

## 📈 Monitoring

### Application Insights Queries

**View validation logs:**
```kusto
traces
| where timestamp > ago(24h)
| where message contains "Validation"
| project timestamp, message, severityLevel
| order by timestamp desc
```

**View API requests:**
```kusto
requests
| where timestamp > ago(24h)
| summarize count() by bin(timestamp, 1h), resultCode
| render timechart
```

## 🧪 Testing

### Using cURL

**Get all games:**
```bash
curl https://farnovaramidterm-dgchhthxdtdwe2ae.westus2-01.azurewebsites.net/api/games
```

**Add a game:**
```bash
curl -X POST https://farnovaramidterm-dgchhthxdtdwe2ae.westus2-01.azurewebsites.net/api/games \
  -H "Content-Type: application/json" \
  -H "x-api-key: YOUR_API_KEY" \
  -d '{"title":"Test Game","upc":"123456789","year":2024,"publisher":"Test Publisher"}'
```

**Validate games:**
```bash
curl -X PATCH https://farnovaramidterm-dgchhthxdtdwe2ae.westus2-01.azurewebsites.net/api/games/validate \
  -H "x-api-key: YOUR_API_KEY"
```

## 📝 Project Structure

```
Cloud-Computing-Project/
├── HttpTrigger1.cs          # Main API endpoints
├── Program.cs               # Function app configuration
├── Models/
│   └── Game.cs             # Data model
├── create_table.sql        # Database schema
├── update_table_schema.sql # Schema updates
├── host.json               # Function app settings
└── README.md               # This file
```

## 🎓 Learning Objectives Demonstrated

- ✅ Serverless computing with Azure Functions
- ✅ Secure secret management with Key Vault
- ✅ Managed Identity authentication
- ✅ Database integration with Azure SQL
- ✅ Workflow automation with Logic Apps
- ✅ Application monitoring with Application Insights
- ✅ RESTful API design
- ✅ Cloud-native architecture patterns

## 👤 Author

**Ryan Novara**
- GitHub: [@ryannovara](https://github.com/ryannovara)

