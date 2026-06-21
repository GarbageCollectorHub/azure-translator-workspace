# TranslatorAzure test suite

The suite is intentionally split by responsibility so a regression can be located quickly.

- `TranslatorAzure.Tests/FileText` tests file validation, extraction, PDF limits, and segmentation.
- `TranslatorAzure.Tests/Translator` tests Azure request behavior and controller decisions without network access.
- `TranslatorAzure.Tests/Integration` tests the real ASP.NET Core routing, antiforgery, JSON, and upload pipeline.
- `TranslatorAzure.Tests/TestSupport` contains in-memory documents, HTTP handlers, and translator fakes shared by tests.

No automated test calls Azure. The integration tests host the application in memory and replace only `ITranslatorService`.

Run the suite from the repository root:

```powershell
dotnet test tests\TranslatorAzure.Tests\TranslatorAzure.Tests.csproj
```

Generate a local coverage report:

```powershell
dotnet test tests\TranslatorAzure.Tests\TranslatorAzure.Tests.csproj --collect:"XPlat Code Coverage"
```
