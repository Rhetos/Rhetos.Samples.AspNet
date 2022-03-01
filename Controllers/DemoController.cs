using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Rhetos.Processing;
using Rhetos.Processing.DefaultCommands;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Rhetos.Samples.AspNet.Controllers
{
    [Route("Demo/[action]")]
    public class DemoController : ControllerBase
    {
        private readonly IProcessingEngine processingEngine;
        private readonly IUnitOfWork unitOfWork;

        public DemoController(IRhetosComponent<IProcessingEngine> processingEngine, IRhetosComponent<IUnitOfWork> unitOfWork)
        {
            this.processingEngine = processingEngine.Value;
            this.unitOfWork = unitOfWork.Value;
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
        public string WriteBook()
        {
            var newBook = new Bookstore.Book { Title = "NewBook" };
            var saveCommandInfo = new SaveEntityCommandInfo { Entity = "Bookstore.Book", DataToInsert = new[] { newBook } };
            processingEngine.Execute(saveCommandInfo);
            unitOfWork.CommitAndClose(); // Commits and closes the database transaction for the current unit of work scope (web request).
            return "1 book inserted.";
        }

        [HttpGet]
        public async Task Login()
        {
            // Singing in as a fixed predefined user, for demo purpose.
            const string username = "SampleUser";
            var claimsIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties() { IsPersistent = true });
        }
    }
}
