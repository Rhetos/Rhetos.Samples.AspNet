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
   5. [Use NLog to write application's system log into a file](#use-nlog-to-write-applications-system-log-into-a-file)
   6. [Adding localization](#adding-localization)

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

*This demo app has namespace `Rhetos.Sample.AspNet` that starts with `Rhetos.`, so we need to correct `Host` conflict in `Program.cs` by changing `Host.CreateDefaultBuilder(...` reads `Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(...`.*

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
        .UseBuilderLogProviderFromHost(serviceProvider)
        .ConfigureConfiguration(cfg => cfg.MapNetCoreConfiguration(Configuration));
}
```

And register Rhetos in `ConfigureServices` method:

```cs
services.AddRhetosHost(ConfigureRhetosHostBuilder)
    .AddAspNetCoreIdentityUser()
    .AddHostLogging();
```

Rhetos needs database to work with, create it and configure connection string in `appsettings.json` file:

```cs
  "ConnectionStrings": {
    "RhetosConnectionString": "<YOURDBCONNECTIONSTRING>"
  }
```

## Applying Rhetos model to database

To apply model to database we need to use `rhetos.exe` CLI tool. CLI tools need to be able to discover host application configuration and setup. We provide that via static method in `Program.cs`.
`rhetos.exe` will look for the class where the entry point method is located and will look for the method  `public static IHostBuilder CreateHostBuilder(string[] args)` inside that class and use this method to construct a Rhetos host.

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

In larger applications, for improved Swagger load time, it is recommended to **split each DSL Module into a separate Swagger document**. See additional instructions in RestGenerator documentation in section [Adding Swagger/OpenAPI](https://github.com/Rhetos/RestGenerator/blob/master/Readme.md#adding-swaggeropenapi).

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
    "AllClaimsForUsers": "SampleUser"
  }
}
```

`http://localhost:5000/Rhetos/ReadBooks` should now correctly return `0` as we haven't added any `Book` entities.

You can write additional controllers/actions and invoke Rhetos commands now.

### Use NLog to write application's system log into a file

1. In Program.cs add `using NLog.Web;`
2. In `Program.CreateHostBuilder` method add `hostBuilder.UseNLog();`
3. In `Startup.ConfigureServices`, at `AddRhetosHost`, add `.AddHostLogging()`
   (if it's not there already).
4. To configure NLog add the `nlog.config` file to the project.
   Make sure that the file properties are set to Copy to Output Directory: Copy if newer.
   To make logging compatible with Rhetos v3 and v4, enter the following text into the file.

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- THis configuration file is used by NLog to setup the logging if the hostBuilder.UseNLog() method is called inside the Program.CreateHostBuilder method-->
<nlog throwConfigExceptions="true" xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <target name="MainLog" xsi:type="File" fileName="${basedir}\Logs\RhetosServer.log" encoding="utf-8" archiveFileName="${basedir}\Logs\Archives\RhetosServer {#####}.zip" enableArchiveFileCompression="true" archiveAboveSize="2000000" archiveNumbering="DateAndSequence" />
    <target name="ConsoleLog" xsi:type="Console" />
    <target name="TraceLog" xsi:type="AsyncWrapper" overflowAction="Block">
      <target name="TraceLogBase" xsi:type="File" fileName="${basedir}\Logs\RhetosServerTrace.log" encoding="utf-8" archiveFileName="${basedir}\Logs\Archives\RhetosServerTrace {#####}.zip" enableArchiveFileCompression="true" archiveAboveSize="10000000" archiveNumbering="DateAndSequence" />
    </target>
    <target name="TraceCommandsXml" xsi:type="AsyncWrapper" overflowAction="Block">
      <target name="TraceCommandsXmlBase" xsi:type="File" fileName="${basedir}\Logs\RhetosServerCommandsTrace.xml" encoding="utf-16" layout="&lt;!--${longdate} ${logger}--&gt;${newline}${message}" archiveFileName="${basedir}\Logs\Archives\RhetosServerCommandsTrace {#####}.zip" enableArchiveFileCompression="true" archiveAboveSize="10000000" archiveNumbering="DateAndSequence" />
    </target>
    <target name="PerformanceLog" xsi:type="AsyncWrapper" overflowAction="Block">
      <target name="PerformanceLogBase" xsi:type="File" fileName="${basedir}\Logs\RhetosServerPerformance.log" encoding="utf-8" archiveFileName="${basedir}\Logs\Archives\RhetosServerPerformance {#####}.zip" enableArchiveFileCompression="true" archiveAboveSize="10000000" archiveNumbering="DateAndSequence" />
    </target>
  </targets>
  <rules>
    <logger name="*" minLevel="Info" writeTo="MainLog" />
    <!-- <logger name="*" minLevel="Info" writeTo="ConsoleLog" /> -->
    <!-- <logger name="*" minLevel="Trace" writeTo="TraceLog" /> -->
    <!-- <logger name="ProcessingEngine Request" minLevel="Trace" writeTo="ConsoleLog" /> -->
    <!-- <logger name="ProcessingEngine Request" minLevel="Trace" writeTo="TraceLog" /> -->
    <!-- <logger name="ProcessingEngine Commands" minLevel="Trace" writeTo="TraceCommandsXml" /> -->
    <!-- <logger name="ProcessingEngine CommandsResult" minLevel="Trace" writeTo="TraceCommandsXml" /> -->
    <!-- <logger name="ProcessingEngine CommandsWithClientError" minLevel="Trace" writeTo="TraceCommandsXml" /> -->
    <logger name="ProcessingEngine CommandsWithServerError" minLevel="Trace" writeTo="TraceCommandsXml" />
    <!-- <logger name="ProcessingEngine CommandsWithServerError" minLevel="Trace" writeTo="MainLog" /> -->
    <!-- <logger name="Performance*" minLevel="Trace" writeTo="PerformanceLog" /> -->
  </rules>
</nlog>
```

### Adding localization

Localization provides support for multiple languages, but it can also be very useful even if
an application uses **only one** language (English, e.g.) to modify the messages
to match the client requirements.

Localization in Rhetos app is automatically applied on translating the Rhetos response messages
for end users. For example, a data validation error message (InvalidData), UserException, and other.

The following example adds [GetText / PO](http://en.wikipedia.org/wiki/Gettext) localization
support to the Rhetos app:

1. Rhetos components are configured to use the host application's localization
   (standard ASP.NET Core localization) by simply adding `AddHostLocalization()` in Rhetos setup.
2. Any ASP.NET Core localization plugin can be used.
   This example uses OrchardCore, a 3rd party library recommended by Microsoft,
   see [Configure portable object localization in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/portable-object-localization?view=aspnetcore-5.0)

Add localization to your Rhetos app:

1. In the `.csproj` file, add the following lines:

    ```xml
      <ItemGroup>
        <PackageReference Include="OrchardCore.Localization.Core" Version="1.1.0" />
        <None Update="Localization\hr.po">
          <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
      </ItemGroup>
    ```

2. Create file `Localization\hr.po` with translations for language "hr"
   (see [CultureInfo](https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo) for language codes),
   with the following content:

    ```pot
    msgctxt "Rhetos"
    msgid "It is not allowed to enter {0} because the required property {1} is not set."
    msgstr "Nije dozvoljen unos zapisa {0} jer polje {1} nije zadano."
    ```

3. In `Startup.cs` file, add the following lines (note that DefaultRequestCulture is set to "hr"):

    ```cs
    using Microsoft.AspNetCore.Localization;
    using System.Collections.Generic;
    using System.Globalization;

    // ... in ConfigureServices method, after services.AddRhetosHost:
                    .AddHostLocalization()

    // ... in ConfigureServices method:
                services.AddLocalization()
                    .AddPortableObjectLocalization(options => options.ResourcesPath = "Localization")
                    .AddMemoryCache();

    // ... in Configure method:
                app.UseRequestLocalization(options =>
                {
                    var supportedCultures = new List<CultureInfo>
                    {
                        new CultureInfo("en"),
                        new CultureInfo("hr")
                    };

                    options.DefaultRequestCulture = new RequestCulture("hr");
                    options.SupportedCultures = supportedCultures;
                    options.SupportedUICultures = supportedCultures;
                    options.RequestCultureProviders = new List<IRequestCultureProvider>
                    {
                        //The culture will be resolved based on the query parameter.
                        //For example if we want the validation message to be translated to Croatian
                        //we can call the POST method rest/Bookstore/Book?culture=hr and insert a json object without the 'Title' property.
                        //It can be configured so that the culture gets resolved based on cookies or headers.
                        new QueryStringRequestCultureProvider()
                    };
                });
    ```

For example, see [Bookstore.Service](https://github.com/Rhetos/Bookstore/tree/master/src/Bookstore.Service) demo app.
