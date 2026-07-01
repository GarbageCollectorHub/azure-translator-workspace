# Azure Translator Workspace

ASP.NET Core MVC translation workspace built around Azure AI Translator, Azure AI Speech, document text import, PDF selection translation, and production-style request protection.

Live demo: https://garbagecollector.azurewebsites.net/Translator

## Highlights

- Text translation with Azure AI Translator, automatic source language detection, language switching, and transliteration when Azure supports the selected language/script.
- Azure Text to Speech playback with automatic neural voice selection, supported-language detection, request timing logs, and monthly character quotas stored in Cosmos DB.
- Document workspace for TXT, DOCX, and PDF imports, including safe extraction limits, translation-ready segmentation, section navigation, and PDF.js preview.
- Selected-text translation for editor text and PDF text selections, with floating draggable/resizable translation panels and optional replacement in editable text.
- Local browser history for recent translations, copy actions, saved language preferences, and session-scoped layout preferences for resizable UI.
- Rate limiting for translation, file import, and speech synthesis with per-client and global fixed-window limits.
- Background usage metrics pipeline with a queue, hashed IPs, user-agent parsing, optional GeoIP lookup, source/referrer metadata, and an ignore token for developer visits.
- Graceful failure handling for Azure service errors, unsupported speech languages, missing configuration, upload validation, and rate-limit responses.

## Tech Stack

- .NET 9, ASP.NET Core MVC, C#
- Azure AI Translator, Azure AI Speech, Azure App Service
- Azure Cosmos DB
- PDF.js, PdfPig, Open XML SDK
- Bootstrap, Bootstrap Icons, modular vanilla JavaScript
- xUnit, WebApplicationFactory integration tests

## Architecture

The application keeps the main translation flow server-rendered with ASP.NET Core MVC, while richer interactions are handled by small JavaScript modules under `wwwroot/js/translator`.

- `Controllers/TranslatorController.cs` serves the translator page, form translation, selected-text translation, and speech synthesis.
- `Services/Translator` wraps Azure Translator requests, language caching, fallback languages, and transliteration.
- `Services/Speech` handles Azure Speech voice lookup, SSML generation, quota reservation/release, and audio responses.
- `Services/FileText` imports TXT, DOCX, and PDF content into translation-safe segments.
- `Services/Analytics` processes visit and usage events through a background queue before writing to Cosmos DB.
- `Infrastructure/RateLimiting` defines per-client and global request protection policies.

## Configuration

Local secrets are not committed. Copy `azuresettings_sample.json` to `azuresettings.json` and fill in your own Azure values.

Main sections:

- `translator`: Azure Translator key, endpoint, region/location, translate route, transliterate route, and languages endpoint.
- `cosmos`: Cosmos DB connection string, database name, and container name.
- `analytics`: optional background usage metrics, IP hash salt, visit cooldown, visitor cookie lifetime, GeoIP toggle, and ignore token.
- `SpeechSynthesis`: Azure Speech key, region/endpoint, output format, text length limit, and monthly character limits.

For Azure App Service, prefer Configuration > Application settings instead of deploying local secrets. Use double underscores for nested settings, for example:

```text
translator__key
translator__endpoint
translator__location
cosmos__connectionString
analytics__enabled
analytics__ipHashSalt
SpeechSynthesis__key
SpeechSynthesis__region
SpeechSynthesis__endpoint
```

## Run Locally

```powershell
dotnet restore
dotnet run
```

Open:

```text
https://localhost:<port>/Translator
```

## Tests

The automated tests avoid real Azure calls and use fakes/in-memory HTTP handlers where needed.

```powershell
dotnet test tests\TranslatorAzure.Tests\TranslatorAzure.Tests.csproj
```

Coverage includes:

- Azure Translator request contracts, language caching, transliteration, and fallback behavior.
- Controller behavior for form translation, selected-text translation, validation, and service failures.
- File import validation, extraction, segmentation, and PDF metadata.
- Integration-level routing, antiforgery, JSON contracts, uploads, and PDF preview assets.