using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace Exatech_Indotel_API.Services.Siuben;

public enum SiubenPhase { Phase1 = 1, Phase2 = 2 }

public class SiubenLocalities
{
    private readonly Dictionary<string, (string Name, SiubenPhase Phase)> _localities = new();

    public SiubenLocalities(IOptions<SiubenOptions> options)
    {
        var configured = options.Value;

        if (!IsValidConfiguration(configured))
            throw new OptionsValidationException(nameof(SiubenOptions), typeof(SiubenOptions),
                ["Each Siuben phase must configure non-empty Localities, with no blank or duplicate names across either phase."]);

        foreach (var name in configured.Phase1.Localities)
            _localities.Add(Normalize(name), (name.Trim(), SiubenPhase.Phase1));

        foreach (var name in configured.Phase2.Localities)
            _localities.Add(Normalize(name), (name.Trim(), SiubenPhase.Phase2));
    }

    public static bool IsValidConfiguration(SiubenOptions options)
    {
        if (options.Phase1.Localities is not { Length: > 0 } || options.Phase2.Localities is not { Length: > 0 })
            return false;

        var names = new HashSet<string>();

        return options.Phase1.Localities
            .Concat(options.Phase2.Localities)
            .All(name => !string.IsNullOrWhiteSpace(name) && names.Add(Normalize(name)));
    }

    private static string Normalize(string? locality)
    {
        var text = new string((locality ?? string.Empty)
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    public bool IsSupported(string? locality) => _localities.ContainsKey(Normalize(locality));

    public IEnumerable<SiubenLocality> GetAll() => _localities.Values
        .OrderBy(entry => entry.Phase)
        .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        .Select(entry => new SiubenLocality(entry.Name, (int)entry.Phase));

    public SiubenPhase GetPhase(string locality) => _localities.TryGetValue(Normalize(locality), out var entry)
        ? entry.Phase : throw new ArgumentException("Locality is required and must be a supported Siuben locality.", nameof(locality));

    public string GetName(string locality) => _localities.TryGetValue(Normalize(locality), out var entry)
        ? entry.Name : throw new ArgumentException("Unsupported Siuben locality.", nameof(locality));
}

public record SiubenLocality(string Name, int Phase);
