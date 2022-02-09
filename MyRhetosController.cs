using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Rhetos;
using Rhetos.Processing;
using Rhetos.Processing.DefaultCommands;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Route("Rhetos/[action]")]
public class MyRhetosController : ControllerBase
{
    private readonly IProcessingEngine processingEngine;

    public MyRhetosController(IRhetosComponent<IProcessingEngine> rhetosProcessingEngine)
    {
        processingEngine = rhetosProcessingEngine.Value;
    }

    [HttpGet]
    public string HelloRhetos()
    {
        return processingEngine.ToString();
    }

    [HttpGet]
    public string ReadBooks()
    {
        var readCommandInfo = new ReadCommandInfo { DataSource = "Bookstore.Book", ReadTotalCount = true };

        var result = processingEngine.Execute(readCommandInfo);

        return $"{result.TotalCount} books.";
    }

    [HttpGet]
    public async Task Login()
    {
        // Singing in as a fixed predefined user, for demo.
        const string username = "SampleUser";
        var claimsIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties() { IsPersistent = true });
    }
}


