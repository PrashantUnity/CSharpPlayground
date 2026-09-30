namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Maps common Go package nicknames or module identifiers to canonical Go module paths for auto-installation.
/// </summary>
public static class GoPackageMap
{
    private static readonly Dictionary<string, string> WellKnownPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        // Web & HTTP
        ["gin"] = "github.com/gin-gonic/gin",
        ["echo"] = "github.com/labstack/echo/v4",
        ["fiber"] = "github.com/gofiber/fiber/v2",
        ["chi"] = "github.com/go-chi/chi/v5",
        ["mux"] = "github.com/gorilla/mux",
        ["websocket"] = "github.com/gorilla/websocket",

        // Utilities & Data
        ["uuid"] = "github.com/google/uuid",
        ["testify"] = "github.com/stretchr/testify",
        ["cobra"] = "github.com/spf13/cobra",
        ["viper"] = "github.com/spf13/viper",
        ["logrus"] = "github.com/sirupsen/logrus",
        ["zap"] = "go.uber.org/zap",
        ["zerolog"] = "github.com/rs/zerolog",
        ["gorm"] = "gorm.io/gorm",
        ["sqlx"] = "github.com/jmoiron/sqlx",
        ["yaml"] = "gopkg.in/yaml.v3",
        ["jwt"] = "github.com/golang-jwt/jwt/v5",
        ["color"] = "github.com/fatih/color",
        ["validator"] = "github.com/go-playground/validator/v10",

        // Databases & Caches
        ["redis"] = "github.com/redis/go-redis/v9",
        ["mongo"] = "go.mongodb.org/mongo-driver/mongo",
        ["pq"] = "github.com/lib/pq",
        ["pgx"] = "github.com/jackc/pgx/v5",
        ["sqlite3"] = "github.com/mattn/go-sqlite3",

        // Concurrency & Networking
        ["sync"] = "golang.org/x/sync",
        ["crypto"] = "golang.org/x/crypto",
        ["net"] = "golang.org/x/net",
        ["protobuf"] = "google.golang.org/protobuf",
        ["grpc"] = "google.golang.org/grpc"
    };

    /// <summary>
    /// Returns the canonical module path for a missing Go package or alias.
    /// </summary>
    public static string PackageFor(string missingName)
    {
        if (string.IsNullOrWhiteSpace(missingName)) return missingName;

        var clean = missingName.Trim().Trim('"', '\'');

        // Check if it's already a full module path (e.g. github.com/foo/bar)
        if (clean.Contains('/', StringComparison.Ordinal) || clean.Contains('.', StringComparison.Ordinal))
        {
            return clean;
        }

        if (WellKnownPackages.TryGetValue(clean, out var canonical))
        {
            return canonical;
        }

        return clean;
    }
}
