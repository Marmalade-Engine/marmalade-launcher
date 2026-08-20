using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;

namespace MarmaladeLauncher.Common.Utils;

public static class AdmxParser
{
    private const string AdmxNamespace = "http://schemas.microsoft.com/GroupPolicy/2006/07/PolicyDefinitions";
    private const string FilePath = "Misc/ADMX/MarmaladeLauncher.admx";

    private static readonly Dictionary<string, PolicyInfo> Policies = new();

    public struct PolicyElement
    {
        public string ValueName { get; set; }
    }

    public struct PolicyInfo
    {
        public RegistryHive? RegistryHive { get; set; }
        public string Key { get; set; }
        public string? ValueName { get; set; }
        public Dictionary<string, PolicyElement>? Elements { get; set; }
    }

    private static RegistryHive? GetRegistryHive(string classType)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return classType switch
            {
                "User" => RegistryHive.CurrentUser,
                "Machine" => RegistryHive.LocalMachine,
                _ => throw new Exception("Invalid ADMX template policy class")
            };
        }

        return null;
    }

    private static void LoadAdmx()
    {
        var doc = XDocument.Load(FilePath);
        var nsManager = new XmlNamespaceManager(new NameTable());
        nsManager.AddNamespace("gpo", AdmxNamespace);

        var categoryNodes = doc.XPathSelectElements("//gpo:policies/gpo:policy", nsManager);

        foreach (var node in categoryNodes)
        {
            var name = node.Attribute("name")?.Value;
            var classType = node.Attribute("class")?.Value;
            var key = node.Attribute("key")?.Value;
            if (name == null || classType == null || key == null)
            {
                continue;
            }

            var valueName = node.Attribute("valueName")?.Value;
            if (valueName == null)
            {
                // Contains elements
                var elementsNode = node.Element(XNamespace.Get(AdmxNamespace) + "elements");
                if (elementsNode == null) continue;

                Dictionary<string, PolicyElement> elements = new();
                foreach (var element in elementsNode.Elements())
                {
                    var elementId = element.Attribute("id")?.Value;
                    var elementValName = element.Attribute("valueName")?.Value;
                    if (elementId == null || elementValName == null) continue;

                    elements[elementId] = new PolicyElement()
                    {
                        ValueName = elementValName
                    };
                }

                Policies[name] = new PolicyInfo
                {
                    RegistryHive = GetRegistryHive(classType),
                    Key = key,
                    Elements = elements
                };
            }
            else
            {
                Policies[name] = new PolicyInfo
                {
                    RegistryHive = GetRegistryHive(classType),
                    Key = key,
                    ValueName = valueName!
                };
            }
        }
    }

    private static string? GetRegistryValue(PolicyInfo policy, PolicyElement? element)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;

        using var baseKey = RegistryKey.OpenBaseKey(policy.RegistryHive!.Value, RegistryView.Registry64);
        using var subKey = baseKey.OpenSubKey(policy.Key, false);
        if (subKey == null)
        {
            return null;
        }

        return element != null
            ? subKey.GetValue(element.Value.ValueName)?.ToString()
            : subKey.GetValue(policy.ValueName)?.ToString();
    }

    public static string? GetString(string name, string? elementName = null, string? defaultVal = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return defaultVal;

        if (Policies.Count == 0)
        {
            LoadAdmx();
        }

        if (!Policies.TryGetValue(name, out var policy))
        {
            return defaultVal;
        }

        if (policy.ValueName != null)
        {
            return GetRegistryValue(policy, null) ?? defaultVal;
        }

        if (elementName == null) throw new Exception("Policy contains elements; elementName expected");

        if (policy.Elements == null || !policy.Elements.TryGetValue(elementName, out var element)) return defaultVal;

        return GetRegistryValue(policy, element) ?? defaultVal;
    }

    public static bool? GetBoolean(string name, string? elementName = null, bool? defaultVal = null)
    {
        var str = GetString(name, elementName, null);

        if (string.IsNullOrEmpty(str)) return defaultVal;

        str = str.Trim();
        if (int.TryParse(str, out var intVal))
        {
            return intVal != 0;
        }

        if (bool.TryParse(str, out var boolVal))
        {
            return boolVal;
        }

        return defaultVal;
    }
}