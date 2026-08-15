using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class InstallationService {
    public static string InstallationsFilePath =>
        Path.Combine(SettingsService.AppDataDir, "installations.json");
    
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    
    public async Task<List<EngineInstallation>> LoadInstallations() {
        try {
            if (!File.Exists(InstallationsFilePath)) return new List<EngineInstallation>();
            
            string json = await File.ReadAllTextAsync(InstallationsFilePath);
            return JsonSerializer.Deserialize<List<EngineInstallation>>(json) ?? new List<EngineInstallation>();
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to load installations: {e.Message}");
            return new List<EngineInstallation>();
        }
    }

    public async Task SaveInstallations(IEnumerable<EngineInstallation> installations) {
        try {
            string json = JsonSerializer.Serialize(installations, _jsonSerializerOptions);
            await File.WriteAllTextAsync(InstallationsFilePath, json); 
        }
        catch  (Exception e) {
            Console.WriteLine($"Failed to save installations: {e.Message}");
        }
    }
}