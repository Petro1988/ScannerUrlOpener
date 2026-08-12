using System.Text.Json;

namespace ScannerUrlOpener.Configuration;

internal sealed class ScannerDeviceConfigurationStore
{
    private readonly string _configurationFilePath;

    public ScannerDeviceConfigurationStore()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ScannerUrlOpener");

        Directory.CreateDirectory(directory);

        _configurationFilePath = Path.Combine(
            directory,
            "scanner.json");
    }

    public ScannerDeviceConfiguration Load()
    {
        try
        {
            if (!File.Exists(_configurationFilePath))
            {
                return new ScannerDeviceConfiguration();
            }

            string json = File.ReadAllText(
                _configurationFilePath);

            return JsonSerializer.Deserialize
                       <ScannerDeviceConfiguration>(json)
                   ?? new ScannerDeviceConfiguration();
        }
        catch
        {
            return new ScannerDeviceConfiguration();
        }
    }

    public void Save(
        ScannerDeviceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string json = JsonSerializer.Serialize(
            configuration,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(
            _configurationFilePath,
            json);
    }

    public void Delete()
    {
        if (File.Exists(_configurationFilePath))
        {
            File.Delete(_configurationFilePath);
        }
    }
}