using Microsoft.AspNetCore.DataProtection;
using OnvifManager.Services;
using OnvifManager.Vendors;
using OnvifManager.Vendors.Config;
using OnvifManager.Web.Configuration;
using OnvifManager.Web.Hubs;
using OnvifManager.Web.Services;
using Serilog;

const string DevCorsPolicy = "DevSpa";

var builder = WebApplication.CreateBuilder(args);

// Storage options + dev-time override. Production default is /data (Docker
// volume); on a developer box that path doesn't exist and isn't writable, so
// remap to the user profile unless the operator pinned an explicit value.
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<MediaMtxOptions>(builder.Configuration.GetSection(MediaMtxOptions.SectionName));

var storage = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
if (builder.Environment.IsDevelopment())
{
    var devRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".onvifmanager-web");
    if (string.IsNullOrEmpty(storage.DataDirectory) || storage.DataDirectory == "/data")
        storage.DataDirectory = Path.Combine(devRoot, "data");
    if (string.IsNullOrEmpty(storage.KeysDirectory) || storage.KeysDirectory == "/data/keys")
        storage.KeysDirectory = Path.Combine(devRoot, "keys");
}

Directory.CreateDirectory(storage.DataDirectory);
Directory.CreateDirectory(storage.KeysDirectory);
Directory.CreateDirectory(Path.Combine(storage.DataDirectory, "logs"));

builder.Services.PostConfigure<StorageOptions>(o =>
{
    o.DataDirectory = storage.DataDirectory;
    o.KeysDirectory = storage.KeysDirectory;
});

builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(storage.DataDirectory, "logs", "onvif-web-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true));

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(storage.KeysDirectory))
    .SetApplicationName("OnvifManager");

builder.Services.AddSingleton(new OnvifClientOptions
{
    AllowSelfSignedCertificates = true,
    Timeout = TimeSpan.FromSeconds(30)
});
builder.Services.AddSingleton<OnvifClientProvider>();

builder.Services.AddSingleton<IVendorAdapter, HikvisionVendorAdapter>();
builder.Services.AddSingleton<VendorRegistry>();

builder.Services.AddSingleton<DiscoveryService>();
builder.Services.AddSingleton<DiscoverySessionManager>();
builder.Services.AddSingleton<SnapshotService>();

// Skipped vs App.xaml.cs: VideoPlayerService (LibVLC/WPF) and AppSettingsService
// (WPF settings). Web equivalents land in M4 (MediaMtx control) and elsewhere.
// ViewModels (Discovery/DeviceInfo/...) are WPF-only and have no analogue here.

builder.Services.AddSingleton<VendorProfileStore>(_ =>
{
    var bundled = Path.Combine(AppContext.BaseDirectory, "Vendors", "Profiles");
    var userDir = Path.Combine(storage.DataDirectory, "vendors");
    Directory.CreateDirectory(userDir);
    return new VendorProfileStore(new[] { bundled, userDir });
});
builder.Services.AddSingleton<IVendorProtocol, IsapiProtocol>();
builder.Services.AddSingleton<VendorParameterService>();

builder.Services.AddSingleton<IPasswordProtector, DataProtectionPasswordProtector>();
builder.Services.AddSingleton<ICameraStore>(sp =>
{
    var path = Path.Combine(storage.DataDirectory, "cameras.json");
    return new JsonCameraStore(path, sp.GetRequiredService<IPasswordProtector>());
});
builder.Services.AddSingleton<CameraStoreFacade>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevCorsPolicy, policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });
}

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCorsPolicy);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<DiscoveryHub>("/hubs/discovery");

// SPA fallback only for non-API/hub/swagger paths; otherwise return 404 so
// clients of unknown endpoints don't receive index.html with HTTP 200.
app.MapFallback(async ctx =>
{
    var path = ctx.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (!File.Exists(indexPath))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    ctx.Response.ContentType = "text/html";
    await ctx.Response.SendFileAsync(indexPath);
});

app.Run();
