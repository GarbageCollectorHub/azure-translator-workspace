using System.Diagnostics;
using TranslatorAzure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace TranslatorAzure.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction(nameof(TranslatorController.Index), "Translator");
        }

        [HttpGet]
        public IActionResult Translator()
        {
            return RedirectToAction(nameof(TranslatorController.Index), "Translator", CurrentQueryValues());
        }

        [HttpGet]
        public IActionResult TranslateAPI()
        {
            return RedirectToAction(nameof(TranslatorController.Translate), "Translator", CurrentQueryValues());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private RouteValueDictionary CurrentQueryValues()
        {
            return new RouteValueDictionary(
                Request.Query.ToDictionary(
                    item => item.Key,
                    item => (object?)item.Value.ToString()));
        }
    }
}
