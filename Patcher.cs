using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Logger = BepInEx.Logging.Logger;

namespace SWEAR;

public class Patcher
{
    internal static string DataPath;
    
    private const string MappingsURL =
        "https://script.google.com/macros/s/AKfycbwdvLOUsw2MfSr0itlvvqT9tk4Pn_okIMeXA-tRKUsWCYszuf5YgPhhiXHa7_hD7zBjIA/exec";

    public static IEnumerable<string> TargetDLLs => new[] { "Assembly-CSharp.dll" };
    
    public static readonly ManualLogSource Log = Logger.CreateLogSource("APIPatcher");
    
    public static void Patch(AssemblyDefinition assembly)
    {
        DataPath = Path.Combine(Paths.PatcherPluginPath, "SWEAR");
        
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
        
        if (!Directory.Exists(DataPath))
            Directory.CreateDirectory(DataPath);
        
        var mappings = new Dictionary<string, string>();
        if (TryLoadCachedMappings(out mappings, out var ver) && ver == new WebClient().DownloadString(MappingsURL + "?mode=version"))
        {
            Log.LogInfo("Loaded cached mappings");
        }
        else
        {
            Log.LogInfo("Downloading mappings");
            var listE = new WebClient().DownloadString(MappingsURL + "?mode=column&column=A").Split('\n');
            var listF = new WebClient().DownloadString(MappingsURL + "?mode=column&column=C").Split('\n');
            for (int i = 1; i < listE.Length; i++)
            {
                if (listF[i] == "TBA" || listF[i].StartsWith("TBA in ") || string.IsNullOrWhiteSpace(listF[i]) ||
                    mappings.ContainsKey(listE[i]))
                    continue;
                if (listF[i].StartsWith("*")) listF[i] = listF[i].Substring(1);
                mappings.Add(listE[i], listF[i]);
            }
            var version = listE[0];
            CacheMappings(mappings, version);
            Log.LogInfo("Downloaded mappings");
        }

        var allTypes = assembly.MainModule.GetAllTypes();
        foreach (var type in allTypes)
        {
            if (type.Name.Length == 11 && type.Name.All(char.IsUpper))
            {
                if (mappings.TryGetValue(type.Name, out var newName) && newName != type.Name)
                {
                    Log.LogDebug($"Renaming {type.Name} to {newName}");
                    type.Name = newName;
                }
            }
        }
        var allMethods = allTypes.SelectMany(t => t.Methods);
        foreach (var method in allMethods)
        {
            if (method.Name.Length == 11 && method.Name.All(char.IsUpper))
            {
                if (mappings.TryGetValue(method.Name, out var newName))
                {
                    Log.LogDebug($"Renaming {method.Name} to {newName}");
                    method.Name = newName;
                }
            }
            foreach (var parameter in method.Parameters)
            {
                if (parameter.Name.Length == 11 && parameter.Name.All(char.IsUpper))
                {
                    if (mappings.TryGetValue(parameter.Name, out var newName))
                    {
                        Log.LogDebug($"Renaming {parameter.Name} to {newName}");
                        parameter.Name = newName;
                    }
                }
            }
        }
        var allFields = allTypes.SelectMany(t => t.Fields);
        foreach (var field in allFields)
        {
            if (field.Name.Length == 11 && field.Name.All(char.IsUpper))
            {
                if (mappings.TryGetValue(field.Name, out var newName))
                {
                    Log.LogDebug($"Renaming {field.Name} to {newName}");
                    field.Name = newName;
                }
            }
        }
        var allProperties = allTypes.SelectMany(t => t.Properties);
        foreach (var property in allProperties)
        {
            if (property.Name.Length == 11 && property.Name.All(char.IsUpper))
            {
                if (mappings.TryGetValue(property.Name, out var newName))
                {
                    Log.LogDebug($"Renaming {property.Name} to {newName}");
                    property.Name = newName;
                }
            }
        }
        var allEvents = allTypes.SelectMany(t => t.Events);
        foreach (var @event in allEvents)
        {
            if (@event.Name.Length == 11 && @event.Name.All(char.IsUpper))
            {
                if (mappings.TryGetValue(@event.Name, out var newName))
                {
                    Log.LogDebug($"Renaming {@event.Name} to {newName}");
                    @event.Name = newName;
                }
            }
        }
    }
    
    private static void CacheMappings(Dictionary<string, string> mappings, string version)
    {
        var mappingsPath = Path.Combine(DataPath, "mappings.txt");
        if (!File.Exists(mappingsPath))
        {
            File.WriteAllText(mappingsPath, string.Join("\n", mappings.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
        else
        {
            var oldMappings = File.ReadAllLines(mappingsPath);
            var newMappings = new List<string>();
            foreach (var oldMapping in oldMappings)
            {
                var split = oldMapping.Split('=');
                if (mappings.TryGetValue(split[0], out var newName))
                {
                    newMappings.Add($"{split[0]}={newName}");
                }
                else
                {
                    newMappings.Add(oldMapping);
                }
            }
            File.WriteAllLines(mappingsPath, newMappings);
        }
        var versionPath = Path.Combine(DataPath, "version.txt");
        File.WriteAllText(versionPath, version);
    }
    
    private static bool TryLoadCachedMappings(out Dictionary<string, string> mappings, out string version)
    {
        mappings = new Dictionary<string, string>();
        version = null;
        var mappingsPath = Path.Combine(DataPath, "mappings.txt");
        if (!File.Exists(mappingsPath))
            return false;
        var lines = File.ReadAllLines(mappingsPath);
        foreach (var line in lines)
        {
            var split = line.Split('=');
            mappings.Add(split[0], split[1]);
        }
        var versionPath = Path.Combine(DataPath, "version.txt");
        if (!File.Exists(versionPath))
            return false;
        version = File.ReadAllText(versionPath);
        return true;
    }
}