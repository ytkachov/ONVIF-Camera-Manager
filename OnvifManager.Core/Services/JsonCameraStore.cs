using System.Text.Json;
using System.Text.Json.Serialization;
using OnvifManager.Models;

namespace OnvifManager.Services;

public sealed class JsonCameraStore : ICameraStore
{
    private readonly IPasswordProtector _protector;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string StorePath { get; }

    public JsonCameraStore(string storePath, IPasswordProtector protector)
    {
        StorePath = storePath;
        _protector = protector;
    }

    public IReadOnlyList<CameraDevice> Load()
    {
        if (!File.Exists(StorePath)) return Array.Empty<CameraDevice>();

        try
        {
            var json = File.ReadAllText(StorePath);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<CameraDevice>();

            var file = JsonSerializer.Deserialize<CameraStoreFile>(json, Options);
            if (file?.Cameras is null) return Array.Empty<CameraDevice>();

            return file.Cameras.Select(ToDevice).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<CameraDevice>();
        }
    }

    public async Task SaveAsync(IEnumerable<CameraDevice> cameras, CancellationToken ct = default)
    {
        var snapshot = cameras.Select(ToEntry).ToList();
        var file = new CameraStoreFile { Cameras = snapshot };

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(StorePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = StorePath + ".tmp";
            await using (var fs = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(fs, file, Options, ct).ConfigureAwait(false);
            }

            if (File.Exists(StorePath)) File.Replace(tmp, StorePath, null);
            else File.Move(tmp, StorePath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private CameraStoreEntry ToEntry(CameraDevice c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        IpAddress = c.IpAddress,
        Port = c.Port,
        Username = c.Username,
        PasswordCipher = string.IsNullOrEmpty(c.Password) ? null : _protector.Protect(c.Password),
        AdminUsername = string.IsNullOrEmpty(c.AdminUsername) ? null : c.AdminUsername,
        AdminPasswordCipher = string.IsNullOrEmpty(c.AdminPassword) ? null : _protector.Protect(c.AdminPassword),
        Endpoint = c.Endpoint,
        Manufacturer = c.Manufacturer,
        Model = c.Model,
        FirmwareVersion = c.FirmwareVersion,
        SerialNumber = c.SerialNumber,
        HardwareId = c.HardwareId,
        IsManual = c.IsManual,
        FullMode = c.FullMode
    };

    private CameraDevice ToDevice(CameraStoreEntry e) => new()
    {
        Id = string.IsNullOrEmpty(e.Id) ? Guid.NewGuid().ToString("N") : e.Id,
        Name = e.Name ?? string.Empty,
        IpAddress = e.IpAddress ?? string.Empty,
        Port = e.Port <= 0 ? 80 : e.Port,
        Username = e.Username ?? "admin",
        Password = string.IsNullOrEmpty(e.PasswordCipher) ? string.Empty : SafeUnprotect(e.PasswordCipher),
        AdminUsername = e.AdminUsername ?? string.Empty,
        AdminPassword = string.IsNullOrEmpty(e.AdminPasswordCipher) ? string.Empty : SafeUnprotect(e.AdminPasswordCipher),
        Endpoint = e.Endpoint ?? string.Empty,
        Manufacturer = e.Manufacturer ?? string.Empty,
        Model = e.Model ?? string.Empty,
        FirmwareVersion = e.FirmwareVersion ?? string.Empty,
        SerialNumber = e.SerialNumber ?? string.Empty,
        HardwareId = e.HardwareId ?? string.Empty,
        IsManual = e.IsManual,
        FullMode = e.FullMode,
        IsDiscovered = !e.IsManual,
        IsConnected = false,
        StatusMessage = string.Empty
    };

    private string SafeUnprotect(string cipher)
    {
        try { return _protector.Unprotect(cipher); }
        catch { return string.Empty; }
    }

    private sealed class CameraStoreFile
    {
        public int Version { get; set; } = 1;
        public List<CameraStoreEntry> Cameras { get; set; } = new();
    }

    private sealed class CameraStoreEntry
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? IpAddress { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? PasswordCipher { get; set; }
        public string? AdminUsername { get; set; }
        public string? AdminPasswordCipher { get; set; }
        public string? Endpoint { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? SerialNumber { get; set; }
        public string? HardwareId { get; set; }
        public bool IsManual { get; set; }
        public bool FullMode { get; set; }
    }
}
