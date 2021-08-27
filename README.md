# Using Rhetos 5 with ASP.NET

Sample app on how to add Rhetos to ASP.NET Web API project.

Complete source code for this example is available at: <https://github.com/Rhetos/Rhetos.Samples.AspNet>

Contents:

1. [Prerequisites](#prerequisites)
2. [Setting up](#setting-up)
3. [Build your first Rhetos App](#build-your-first-rhetos-app)
4. [Connecting to ASP.NET pipeline](#connecting-to-aspnet-pipeline)
5. [Applying Rhetos model to database](#applying-rhetos-model-to-database)
6. [Use Rhetos components in ASP.NET controllers](#use-rhetos-components-in-aspnet-controllers)
   1. [Executing Rhetos commands](#executing-rhetos-commands)
7. [Additional integration/extension options](#additional-integrationextension-options)
   1. [Adding Rhetos dashboard](#adding-rhetos-dashboard)
   2. [Adding Rhetos.RestGenerator](#adding-rhetosrestgenerator)
   3. [View Rhetos.RestGenerator endpoints in Swagger](#view-rhetosrestgenerator-endpoints-in-swagger)
   4. [Adding ASP.NET authentication and connecting it to Rhetos](#adding-aspnet-authentication-and-connecting-it-to-rhetos)

## Prerequisites

1. Run `dotnet --version` to check if you have **.NET 5 SDK** installed. It should output 5.x.x.
   If not, install the latest version from <https://dotnet.microsoft.com/download/dotnet/5.0>.

## Setting up

1. Create a new folder for your project

2. Run `dotnet new webapi`

3. Configure `.csproj`

   * Prevent Rhetos auto deploy:
     Add `<RhetosDeploy>False</RhetosDeploy>` to `<PropertyGroup>` tag.

   * Add packages:

     ```xml
     <ItemGroup>
       <PackageReference Include="Rhetos.Host" Version="5.0.0-dev*" />
       <PackageReference Include="Rhetos.Host.AspNet" Version="5.0.0-dev*" />
       <PackageReference Include="Rhetos.CommonConcepts" Version="5.0.0-dev*" />
       <PackageReference Include="Rhetos.MSBuild" Version="5.0.0-dev*" />
       <PackageReference Include="Microsoft.Extensions.Configuration" Version="5.0.0" />
     </ItemGroup>
     ```

## Build your first Rhetos App

Add Rhetos DSL script named `DslScripts/Books.rhe` and add the following to it:

```c
Module Bookstore
{
    Entity Book
    {
        ShortString Code { AutoCode; }
        ShortString Title;
        Integer NumberOfPages;

        ItemFilter CommonMisspelling 'book => book.Title.Contains("curiousity")';
        InvalidData CommonMisspelling 'It is not allowed to enter misspelled word "curiousity".';

        Logging;
    }
}
```

*This sample is in `Rhetos.*` namespace so we need to correct `Host` conflict in `Program.cs` by changing `Host.CreateDefaultBuilder(...` reads `Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(...`.*

Run `dotnet build` to verify that everything compiles. **Your DSL model from newly added script will be compiled and Rhetos classes are now available in your project.**

## Connecting to ASP.NET pipeline

To wire up Rhetos and ASP.NET dependency injection, modify `Startup.cs`, add a static method (this is a useful convention but it is not required):

```cs
using Rhetos;
```

```cs
private void ConfigureRhetosHostBuilder(IServiceProvider serviceProvider, IRhetosHostBuilder rhetosHostBuilder)
{
    rhetosHostBuilder
        .ConfigureRhetosAppDefaults()
        .ConfigureConfiguration(cfg => cfg.MapNetCoreConfiguration(Configuration));
}
```

And register Rhetos in `ConfigureServices` method:

```cs
services.AddRhetosHost(ConfigureRhetosHostBuilder)
    .AddAspNetCoreIdentityUser();
```

Rhetos needs database to work with, create it and configure connection string in `appsettings.json` file:

```cs
  "ConnectionStrings": {
    "RhetosConnectionString": "<YOURDBCONNECTIONSTRING>"
  }
```

## Applying Rhetos model to database

To apply model to database we need to use `rhetos.exe` CLI tool. CLI tools need to be able to discover host application configuration and setup. We provide that via static method in `Program.cs`.
`rhetos.exe` will look for the class where the enry point method is located and will look for the method  `public static IHostBuilder CreateHostBuilder(string[] args)` inside that class and use this method to construct a Rhetos host.

Run `dotnet build`

Run `./rhetos.exe dbupdate Rhetos.Samples.AspNet.dll` in the binary output folder. This runs database update operation in the context of specified host DLL (in our case, our sample application).

## Use Rhetos components in ASP.NET controllers

This example shows how to use Rhetos components when developing a custom controller.

Add a new controller `MyRhetosController.cs`.

```cs
using Microsoft.AspNetCore.Mvc;
using Rhetos.Host.AspNet;
using Rhetos.Processing;

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
}
```

Run `dotnet run` and browse to `http://localhost:5000/Rhetos/HelloRhetos`. You should see the name of the `ProcessingEngine` type meaning we have successfully resolved it from Rhetos:
`Rhetos.Processing.ProcessingEngine`.

### Executing Rhetos commands

Add a method to `MyRhetosController.cs` to read our Books entity.

```cs
using Rhetos.Processing.DefaultCommands;
using System.Collections.Generic;
using System.Linq;
```

```cs
[HttpGet]
public string ReadBooks()
{
    var readCommandInfo = new ReadCommandInfo() { DataSource = "Bookstore.Book", ReadTotalCount = true };

    var processingResult = rhetosProcessingEngine.Value.Execute(new List<ICommandInfo>() {readCommandInfo});
    var result = (ReadCommandResult) processingResult.CommandResults.Single().Data.Value;
    return result.TotalCount.ToString();
}
```

By default, Rhetos permissions will not allow anonymous users to read any data. Enable anonymous access by modifying `appsettings.json`:

```json
"Rhetos": {
  "AppSecurity": {
    "AllClaimsForAnonymous": true
  }
}
```

Run the example and navigate to `http://localhost:5000/Rhetos/ReadBooks`. You should receive a response value `0` indicating there are 0 entries in our book repository.

## Additional integration/extension options

### Adding Rhetos dashboard

Rhetos dashboard is a standard Rhetos "homepage" that includes basic system information and GUI for some plugins.
It is intended for testing and administration, but it could also be used by end users if needed,
since all official features are implemented with standard Rhetos security permissions.

Adding Rhetos dashboard to a Rhetos application:

1. Extend the Rhetos services configuration (at `services.AddRhetosHost`) with
   the dashboard components: `.AddDashboard()`
2. Extend the application with new endpoint: in the `Startup.Configure` method call
   `app.UseEndpoints(endpoints => { endpoints.MapRhetosDashboard(); });`

To use it simply open `/rhetos` web page in your Rhetos app,
for example <http://localhos:5000/rhetos>. The route is configurable in `MapRhetosDashboard`.

### Adding Rhetos.RestGenerator

Rhetos.RestGenerator package automatically maps all Rhetos data structures to REST endpoints.

Add package to `.csproj` file:

```xml
<PackageReference Include="Rhetos.RestGenerator" Version="5.0.0-dev*" />
```

Modify lines which add Rhetos in `Startup.cs`, method `ConfigureServices` to read:

```cs
services.AddRhetosHost(ConfigureRhetosHostBuilder)
    .AddAspNetCoreIdentityUser()
    .AddRestApi(o => o.BaseRoute = "rest");
```

Add to `Startup.cs`, method `Configure` **before** line `app.UseEndpoints(...`:

```cs
app.UseRhetosRestApi();
```

If you have not configured authentication yet, enable "AllClaimsForAnonymous" configuration option (see the example in section above).

Run `dotnet run`. REST API is now available. Navigate to `http://localhost:5000/rest/Bookstore/Book` to issue a GET and retrieve all Book entity records in the database.

For more info on usage and serialization configuration see [Rhetos.RestGenerator](https://github.com/Rhetos/RestGenerator)

### View Rhetos.RestGenerator endpoints in Swagger

Since Swagger is already added to webapi project template, we can generate Open API specification for mapped Rhetos endpoints.

Modify lines which add Rhetos in `Startup.cs`, method `ConfigureServices` to read:

```cs
services.AddRhetosHost(ConfigureRhetosHostBuilder)
    .AddAspNetCoreIdentityUser()
    .AddRestApi(o => 
    {
        o.BaseRoute = "rest";
        o.GroupNameMapper = (conceptInfo, controller, oldName) => "v1";
    });
```

This addition maps all generated Rhetos API controllers to an existing Swagger document named 'v1'.

Run `dotnet run Environment=Development` and navigate to `http://localhost:5000/swagger/index.html`. You should see entire Rhetos REST API in interactive UI.

### Adding ASP.NET authentication and connecting it to Rhetos

**In this example we will use the simplest possible authentication method, although ANY authentication method supported by ASP.NET may be used. For example [Configure Windows Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth?view=aspnetcore-5.0&tabs=visual-studio)**

Add authentication to ASPNET application. Modify `Services.cs`:

```cs
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
```

Add to `ConfigureServices`:

```cs
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => o.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    });
```

And in `Configure` method after `UseRouting()` add:

```cs
app.UseAuthentication();
```

Modify `MyRhetosController.cs`

```cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.Security.Claims;
```

and a new method to allow us to sign-in:

```cs
[HttpGet]
public async Task Login()
{
    var claimsIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "SampleUser") }, CookieAuthenticationDefaults.AuthenticationScheme);

    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(claimsIdentity),
        new AuthenticationProperties() { IsPersistent = true });
}
```

This is simple stub code to sign-in `SampleUser` so we have a valid user to work with.

In `appsettings.json` set `AllClaimsForAnonymous` to `false`. This disables anonymous workaround we have been using so far.

If you run the app now and navigate to `http://localhost:5000/Rhetos/Login` and then to `http://localhost:5000/Rhetos/ReadBooks`, you will receive an error:
`UserException: Your account 'SampleUser' is not registered in the system. Please contact the system administrator.`

Since 'SampleUser' doesn't exist in Rhetos we will use a simple configuration feature to treat him as admin.

Add to `appsettings.json`:

```json
"Rhetos": {
  "AppSecurity": {
    "AllClaimsForUsers": "SampleUser@<YOURMACHINENAME>"
  }
}
```

`http://localhost:5000/Rhetos/ReadBooks` should now correctly return `0` as we haven't added any `Book` entities.

You can write additional controllers/actions and invoke Rhetos commands now.
