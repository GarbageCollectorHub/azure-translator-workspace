using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TranslatorAzure.Controllers;
using TranslatorAzure.Models.Translator;
using TranslatorAzure.Services.Translator;
using TranslatorAzure.Tests.TestSupport;

namespace TranslatorAzure.Tests.Translator;

// Exercises controller decisions separately from routing, antiforgery, and real HTTP serialization.
public sealed class TranslatorControllerTests
{
    [Fact]
    public async Task Translate_ReturnsTranslationPayloadForValidRequest()
    {
        var translator = new TestTranslatorService();
        var controller = CreateController(translator);

        var action = await controller.Translate(new TranslationRequest
        {
            Text = "Hello",
            LanguageFrom = "en",
            LanguageTo = "uk"
        }, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        Assert.Equal(200, result.StatusCode);
        Assert.Single(translator.TranslateCalls);
        using var payload = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", payload.RootElement.GetProperty("Data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Translate_ReturnsBadRequestWhenModelValidationFails()
    {
        var translator = new TestTranslatorService();
        var controller = CreateController(translator);
        controller.ModelState.AddModelError("Text", "too short");

        var action = await controller.Translate(new TranslationRequest { Text = "x" }, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(translator.TranslateCalls);
    }

    [Fact]
    public async Task Translate_ReturnsServiceUnavailableWhenTranslationIsEmpty()
    {
        var translator = new TestTranslatorService
        {
            Result = new TranslatorResult { TranslatedText = string.Empty }
        };
        var controller = CreateController(translator);

        var action = await controller.Translate(new TranslationRequest { Text = "Hello" }, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task Translate_ReturnsServiceUnavailableWhenTranslatorThrows()
    {
        var translator = new TestTranslatorService
        {
            TranslateException = new HttpRequestException("Azure unavailable")
        };
        var controller = CreateController(translator);

        var action = await controller.Translate(new TranslationRequest { Text = "Hello" }, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task Index_TranslatesSubmittedTextAndExposesDetectionMetadata()
    {
        var translator = new TestTranslatorService
        {
            Result = new TranslatorResult
            {
                TranslatedText = "\u041f\u0440\u0438\u0432\u0456\u0442",
                DetectedLanguage = "en",
                DetectionScore = 0.98
            }
        };
        var controller = CreateController(translator);

        var action = await controller.Index(new TranslatorFormModel
        {
            Button = "translate",
            Text = "Hello",
            LanguageFrom = "auto",
            LanguageTo = "uk"
        }, CancellationToken.None);

        var model = Assert.IsType<TranslatorViewModel>(Assert.IsType<ViewResult>(action).Model);
        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", model.Translation);
        Assert.Equal("en", model.DetectedLanguage);
        Assert.Equal(0.98, model.DetectionScore!.Value);
        Assert.Single(translator.TranslateCalls);
    }

    [Fact]
    public async Task Index_SwitchesCachedTranslationWithoutCallingAzureAgain()
    {
        var translator = new TestTranslatorService();
        var controller = CreateController(translator);
        var form = new TranslatorFormModel
        {
            Button = "switch",
            LanguageFrom = "en",
            LanguageTo = "uk",
            Text = "Hello",
            Translation = "\u041f\u0440\u0438\u0432\u0456\u0442"
        };

        var action = await controller.Index(form, CancellationToken.None);

        var model = Assert.IsType<TranslatorViewModel>(Assert.IsType<ViewResult>(action).Model);
        Assert.Equal("uk", form.LanguageFrom);
        Assert.Equal("en", form.LanguageTo);
        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", form.Text);
        Assert.Equal("Hello", model.Translation);
        Assert.Empty(translator.TranslateCalls);
    }

    [Fact]
    public async Task Index_SwitchWithAutoDetectTranslatesBeforeChangingDirections()
    {
        var translator = new TestTranslatorService
        {
            Result = new TranslatorResult
            {
                TranslatedText = "\u041f\u0440\u0438\u0432\u0456\u0442",
                DetectedLanguage = "en"
            }
        };
        var controller = CreateController(translator);
        var form = new TranslatorFormModel
        {
            Button = "switch",
            LanguageFrom = "auto",
            LanguageTo = "uk",
            Text = "Hello"
        };

        var action = await controller.Index(form, CancellationToken.None);

        var model = Assert.IsType<TranslatorViewModel>(Assert.IsType<ViewResult>(action).Model);
        Assert.Equal("uk", form.LanguageFrom);
        Assert.Equal("en", form.LanguageTo);
        Assert.Equal("\u041f\u0440\u0438\u0432\u0456\u0442", form.Text);
        Assert.Equal("Hello", model.Translation);
        Assert.Single(translator.TranslateCalls);
    }

    [Fact]
    public async Task Index_ShowsControlledErrorWhenTranslationFails()
    {
        var translator = new TestTranslatorService
        {
            TranslateException = new HttpRequestException("Azure unavailable")
        };
        var controller = CreateController(translator);

        var action = await controller.Index(new TranslatorFormModel
        {
            Button = "translate",
            Text = "Hello"
        }, CancellationToken.None);

        var model = Assert.IsType<TranslatorViewModel>(Assert.IsType<ViewResult>(action).Model);
        Assert.Equal("Translation is temporarily unavailable. Please try again.", model.ErrorMessage);
    }

    private static TranslatorController CreateController(ITranslatorService translator) =>
        new(translator, NullLogger<TranslatorController>.Instance);
}
