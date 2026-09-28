using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Maps common missing Java types and package names to their canonical Maven Central coordinates.
/// </summary>
public static class MavenPackageMap
{
    private static readonly FrozenDictionary<string, string> TypeToPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // JSON
        ["Gson"] = "com.google.code.gson:gson:2.11.0",
        ["JsonObject"] = "com.google.code.gson:gson:2.11.0",
        ["JsonArray"] = "com.google.code.gson:gson:2.11.0",
        ["JsonElement"] = "com.google.code.gson:gson:2.11.0",
        ["ObjectMapper"] = "com.fasterxml.jackson.core:jackson-databind:2.17.0",
        ["JsonNode"] = "com.fasterxml.jackson.core:jackson-databind:2.17.0",
        ["ObjectNode"] = "com.fasterxml.jackson.core:jackson-databind:2.17.0",
        ["ArrayNode"] = "com.fasterxml.jackson.core:jackson-databind:2.17.0",
        ["JSONObject"] = "org.json:json:20240303",
        ["JSONArray"] = "org.json:json:20240303",

        // Apache Commons
        ["StringUtils"] = "org.apache.commons:commons-lang3:3.14.0",
        ["ArrayUtils"] = "org.apache.commons:commons-lang3:3.14.0",
        ["RandomStringUtils"] = "org.apache.commons:commons-lang3:3.14.0",
        ["IOUtils"] = "commons-io:commons-io:2.16.1",
        ["FileUtils"] = "commons-io:commons-io:2.16.1",
        ["FilenameUtils"] = "commons-io:commons-io:2.16.1",
        ["CSVParser"] = "org.apache.commons:commons-csv:1.10.0",
        ["CSVPrinter"] = "org.apache.commons:commons-csv:1.10.0",
        ["CSVFormat"] = "org.apache.commons:commons-csv:1.10.0",
        ["DigestUtils"] = "commons-codec:commons-codec:1.16.1",

        // Google Guava
        ["ImmutableList"] = "com.google.guava:guava:33.1.0-jre",
        ["ImmutableMap"] = "com.google.guava:guava:33.1.0-jre",
        ["ImmutableSet"] = "com.google.guava:guava:33.1.0-jre",
        ["Lists"] = "com.google.guava:guava:33.1.0-jre",
        ["Maps"] = "com.google.guava:guava:33.1.0-jre",
        ["Sets"] = "com.google.guava:guava:33.1.0-jre",
        ["Multimap"] = "com.google.guava:guava:33.1.0-jre",
        ["Preconditions"] = "com.google.guava:guava:33.1.0-jre",
        ["RateLimiter"] = "com.google.guava:guava:33.1.0-jre",

        // Logging
        ["Logger"] = "org.slf4j:slf4j-api:2.0.12",
        ["LoggerFactory"] = "org.slf4j:slf4j-api:2.0.12",

        // Database
        ["SQLiteDataSource"] = "org.xerial:sqlite-jdbc:3.45.1.0",
        ["SQLiteConnection"] = "org.xerial:sqlite-jdbc:3.45.1.0",
        ["JDBC"] = "org.xerial:sqlite-jdbc:3.45.1.0",
        ["sqlite"] = "org.xerial:sqlite-jdbc:3.45.1.0",
        ["sqlite-jdbc"] = "org.xerial:sqlite-jdbc:3.45.1.0",
        ["PGSimpleDataSource"] = "org.postgresql:postgresql:42.7.3",

        // Testing
        ["Test"] = "org.junit.jupiter:junit-jupiter:5.10.2",
        ["Assertions"] = "org.junit.jupiter:junit-jupiter:5.10.2",
        ["assertThat"] = "org.assertj:assertj-core:3.25.3",

        // Networking / HTTP
        ["OkHttpClient"] = "com.squareup.okhttp3:okhttp:4.12.0",
        ["Request"] = "com.squareup.okhttp3:okhttp:4.12.0",
        ["Response"] = "com.squareup.okhttp3:okhttp:4.12.0",

        // Charting / Visualization
        ["XYChart"] = "org.knowm.xchart:xchart:3.8.7",
        ["CategoryChart"] = "org.knowm.xchart:xchart:3.8.7",
        ["SwingWrapper"] = "org.knowm.xchart:xchart:3.8.7"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string PackageFor(string missingName)
    {
        if (string.IsNullOrWhiteSpace(missingName)) return missingName;

        var clean = missingName.Trim();
        var dot = clean.LastIndexOf('.');
        if (dot >= 0 && dot < clean.Length - 1)
        {
            clean = clean[(dot + 1)..];
        }

        if (TypeToPackage.TryGetValue(clean, out var pkg))
        {
            return pkg;
        }

        if (TypeToPackage.TryGetValue(missingName.Trim(), out var fullPkg))
        {
            return fullPkg;
        }

        return missingName.Trim();
    }
}
