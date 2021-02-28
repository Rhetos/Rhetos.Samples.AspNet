using Microsoft.AspNetCore.Mvc;
using Rhetos.Host.AspNet;
using Rhetos.Processing;
using Rhetos.Processing.DefaultCommands;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.Security.Claims;

[Route("Rhetos/[action]")]
public class MyRhetosController : ControllerBase
{
    private readonly IRhetosComponent<IProcessingEngine> rhetosProcessingEngine;

    public MyRhetosController(IRhetosComponent<IProcessingEngine> rhetosProcessingEngine)
    {
        this.rhetosProcessingEngine = rhetosProcessingEngine;
    }

    [HttpGet]
    public string HelloRhetos()
    {
        return rhetosProcessingEngine.Value.ToString();
    }

    [HttpGet]
    public string ReadBooks()
    {
        var readCommandInfo = new ReadCommandInfo() { DataSource = "Bookstore.Book", ReadTotalCount = true };

        var processingResult = rhetosProcessingEngine.Value.Execute(new List<ICommandInfo>() {readCommandInfo});
        var result = (ReadCommandResult) processingResult.CommandResults.Single().Data.Value;
        return result.TotalCount.ToString();
    }

    [HttpGet]
    public async Task Login()
    {
        var claimsIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "SampleUser") }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties() { IsPersistent = true });
    }
}


