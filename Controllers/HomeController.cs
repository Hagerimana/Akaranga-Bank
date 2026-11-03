using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AkarangaBankMvc.Web.Models;

using Microsoft.AspNetCore.Authorization;

namespace AkarangaBankMvc.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
