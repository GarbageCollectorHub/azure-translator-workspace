# TranslatorAzure

ASP.NET Core MVC translation workspace built with Azure Translator and deployed to Azure App Service.

Live demo: https://garbagecollector.azurewebsites.net/Translator

## Features

- Text translation through Azure Translator.
- Automatic source language detection.
- Transliteration display when Azure supports it for the selected language/script.
- Language switch flow for translated text.
- Document mode with selected-text translation and optional replacement.
- TXT, DOCX, and PDF text import with section navigation and PDF preview.
- Local browser history with copy and clear actions.
- Responsive UI for desktop and mobile layouts.
- Graceful error state when Azure Translator is unavailable.

## Tech Stack

- ASP.NET Core MVC
- Azure Translator
- Azure App Service
- Bootstrap
- JavaScript

## Configuration

Local secrets are not committed. Create `azuresettings.json` from `azuresettings_sample.json`:

```json
{
  "translator": {
    "key": "<your-translator-key>",
    "endpoint": "https://api.cognitive.microsofttranslator.com",
    "location": "<YOUR-RESOURCE-LOCATION>",
    "route": "/translate?api-version=3.0",
    "routeTransliterate": "/transliterate?api-version=3.0",
    "languagesUrl": "https://api.cognitive.microsofttranslator.com/languages?api-version=3.0"
  }
}
```

For Azure App Service, store secrets in Configuration > Application settings:

- `translator__key`
- `translator__endpoint`
- `translator__location`
- `translator__route`
- `translator__routeTransliterate`
- `translator__languagesUrl`

## Run Locally

```powershell
dotnet restore
dotnet run
```

Open the `/Translator` route.
