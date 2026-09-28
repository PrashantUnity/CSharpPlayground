namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// The pip package that provides an importable module, for the modules whose package is named differently
/// (<c>import cv2</c> comes from <c>opencv-python</c>). Any other module's package has its own name.
/// </summary>
public static class PythonPackageMap
{
    private static readonly Dictionary<string, string> Packages = new(StringComparer.Ordinal)
    {
        ["cv2"] = "opencv-python",
        ["sklearn"] = "scikit-learn",
        ["skimage"] = "scikit-image",
        ["PIL"] = "Pillow",
        ["yaml"] = "PyYAML",
        ["bs4"] = "beautifulsoup4",
        ["dateutil"] = "python-dateutil",
        ["dotenv"] = "python-dotenv",
        ["docx"] = "python-docx",
        ["pptx"] = "python-pptx",
        ["fitz"] = "PyMuPDF",
        ["jwt"] = "PyJWT",
        ["serial"] = "pyserial",
        ["usb"] = "pyusb",
        ["Crypto"] = "pycryptodome",
        ["OpenSSL"] = "pyOpenSSL",
        ["attr"] = "attrs",
        ["google.protobuf"] = "protobuf",
        ["gi"] = "PyGObject",
        ["wx"] = "wxPython",
        ["OpenGL"] = "PyOpenGL",
        ["magic"] = "python-magic",
        ["telegram"] = "python-telegram-bot",
        ["win32api"] = "pywin32",
        ["win32con"] = "pywin32",
        ["pythoncom"] = "pywin32",
        ["mpl_toolkits"] = "matplotlib",
        ["tensorflow_datasets"] = "tensorflow-datasets",
        ["Levenshtein"] = "python-Levenshtein",
        ["sentence_transformers"] = "sentence-transformers",
        ["faiss"] = "faiss-cpu"
    };

    /// <summary>The package to install for a missing module (<c>numpy.linalg</c> → <c>numpy</c>, <c>cv2</c> → <c>opencv-python</c>).</summary>
    public static string PackageFor(string moduleName)
    {
        var name = moduleName.Trim().Trim('\'', '"');
        if (Packages.TryGetValue(name, out var package)) return package;
        var topLevel = TopLevel(name);
        return Packages.TryGetValue(topLevel, out package) ? package : topLevel;
    }

    /// <summary><c>numpy.core._multiarray_umath</c> → <c>numpy</c>.</summary>
    public static string TopLevel(string moduleName)
    {
        var name = moduleName.Trim().Trim('\'', '"');
        var dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }
}
