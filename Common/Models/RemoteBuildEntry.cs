using System;
using System.Collections.Generic;
using System.Globalization;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Models;

public class RemoteBuildEntry {
    public int id { get; set; }
    public string? name { get; set; }
    public string url { get; set; } = string.Empty;
    public string date { get; set; } = string.Empty;
    public long size { get; set; } = 0;
    public string? version { get; set; }
    public string branch { get; set; } = "main";

    public List<string>? features { get; set; }
    public List<string>? changelog { get; set; }

    public string ResolvedVersion => !string.IsNullOrWhiteSpace(version) 
        ? version 
        : $"Build {id}";
    
    public DateTime DateAdded => DateTime.TryParseExact(
        date, 
        "r", 
        CultureInfo.InvariantCulture, 
        DateTimeStyles.None, 
        out var parsedDate) 
        ? parsedDate 
        : (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallbackDate) 
            ? fallbackDate 
            : DateTime.MinValue);
}