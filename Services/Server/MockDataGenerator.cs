using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

/// <summary>
/// Built-in realistic mock data generators for prototyping API endpoints without manual hardcoding.
/// </summary>
public static class Mock
{
    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Evan", "Fiona", "George", "Hannah", "Ian", "Julia" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Vance", "Taylor", "Anderson" };
    private static readonly string[] Companies = { "Acme Corp", "Globex", "Initech", "Soylent", "Umbrella", "Cyberdyne", "Stark Industries", "Wayne Enterprises" };
    private static readonly string[] Domains = { "example.com", "frypdf.com", "codefry.dev", "testmail.org" };
    private static readonly string[] Cities = { "New York", "San Francisco", "London", "Tokyo", "Berlin", "Sydney", "Toronto", "Paris" };

    private static readonly Random Rng = new();

    public static string Name() =>
        $"{FirstNames[Rng.Next(FirstNames.Length)]} {LastNames[Rng.Next(LastNames.Length)]}";

    public static string Email(string? name = null)
    {
        var baseName = (name ?? Name()).ToLowerInvariant().Replace(' ', '.');
        return $"{baseName}@{Domains[Rng.Next(Domains.Length)]}";
    }

    public static string Company() => Companies[Rng.Next(Companies.Length)];

    public static string City() => Cities[Rng.Next(Cities.Length)];

    public static string Guid() => System.Guid.NewGuid().ToString("D");

    public static int Int(int min = 1, int max = 1000) => Rng.Next(min, max);

    public static double Double(double min = 10.0, double max = 1000.0) =>
        Math.Round(min + (Rng.NextDouble() * (max - min)), 2);

    public static bool Bool(double trueProbability = 0.5) => Rng.NextDouble() < trueProbability;

    public static DateTime Date(int pastDays = 30) =>
        DateTime.UtcNow.AddDays(-Rng.Next(0, pastDays)).AddMinutes(-Rng.Next(0, 1440));

    public static List<T> List<T>(int count, Func<int, T> factory) =>
        Enumerable.Range(1, count).Select(factory).ToList();
}
