namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Provides display and inspection runtime support for Go scripts and notebooks in C# Code Studio,
/// emitting rich display MIME bundles for interactive HTML, image, JSON, and table rendering in Results (.DUMP).
/// </summary>
public static class GoDisplayRuntime
{
    public static async Task<string> EnsureDisplayPackageAsync(string buildDir, CancellationToken ct = default)
    {
        var displayDir = Path.Combine(buildDir, "fry", "display");
        try
        {
            Directory.CreateDirectory(displayDir);
            var file = Path.Combine(displayDir, "display.go");
            await File.WriteAllTextAsync(file, DisplayGoContent, ct).ConfigureAwait(false);
        }
        catch
        {
            // Gracefully ignore directory creation issues
        }

        return displayDir;
    }

    public const string DisplayGoContent = """
        package display

        import (
        	"encoding/base64"
        	"encoding/json"
        	"fmt"
        	"os"
        	"strings"
        )

        func emitProtocol(jsonBundle string) {
        	fmt.Println("__FRY_DISPLAY__ " + jsonBundle)
        }

        // Html renders raw HTML in the C# Code Studio Results (.DUMP) deck.
        func Html(htmlContent string) {
        	data := map[string]interface{}{
        		"type": "display",
        		"data": map[string]string{
        			"text/html": htmlContent,
        		},
        		"metadata": map[string]interface{}{},
        	}
        	bytes, _ := json.Marshal(data)
        	emitProtocol(string(bytes))
        }

        // Image displays a local image file path or Base64-encoded image data in the Results deck.
        func Image(pathOrBase64 string) {
        	b64 := pathOrBase64
        	mime := "image/png"

        	if strings.HasPrefix(b64, "data:image/") {
        		idx := strings.Index(b64, ",")
        		if idx != -1 {
        			b64 = b64[idx+1:]
        		}
        	} else if fileBytes, err := os.ReadFile(pathOrBase64); err == nil {
        		b64 = base64.StdEncoding.EncodeToString(fileBytes)
        		if strings.HasSuffix(strings.ToLower(pathOrBase64), ".jpg") || strings.HasSuffix(strings.ToLower(pathOrBase64), ".jpeg") {
        			mime = "image/jpeg"
        		} else if strings.HasSuffix(strings.ToLower(pathOrBase64), ".svg") {
        			mime = "image/svg+xml"
        		}
        	}

        	data := map[string]interface{}{
        		"type": "display",
        		"data": map[string]string{
        			mime: b64,
        		},
        		"metadata": map[string]interface{}{},
        	}
        	bytes, _ := json.Marshal(data)
        	emitProtocol(string(bytes))
        }

        // Json displays structured JSON in the Results deck.
        func Json(jsonString string) {
        	data := map[string]interface{}{
        		"type": "display",
        		"data": map[string]string{
        			"text/plain": jsonString,
        		},
        		"metadata": map[string]interface{}{},
        	}
        	bytes, _ := json.Marshal(data)
        	emitProtocol(string(bytes))
        }

        // Dump serializes any Go variable to JSON and renders it in the Results deck.
        func Dump(v interface{}) {
        	bytes, err := json.MarshalIndent(v, "", "  ")
        	if err != nil {
        		fmt.Printf("%+v\n", v)
        		return
        	}
        	Json(string(bytes))
        }
        """;
}
