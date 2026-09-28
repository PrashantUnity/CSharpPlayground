using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Maps common missing C++ include header files to their corresponding vcpkg package names.
/// </summary>
public static class CppPackageMap
{
    private static readonly FrozenDictionary<string, string> HeaderToPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // JSON & Serialization
        ["nlohmann/json.hpp"] = "nlohmann-json",
        ["json.hpp"] = "nlohmann-json",
        ["rapidjson/document.h"] = "rapidjson",
        ["yaml-cpp/yaml.h"] = "yaml-cpp",
        ["pugixml.hpp"] = "pugixml",

        // Formatting & Printing
        ["fmt/core.h"] = "fmt",
        ["fmt/format.h"] = "fmt",
        ["fmt/color.h"] = "fmt",
        ["fmt/ranges.h"] = "fmt",

        // Logging
        ["spdlog/spdlog.h"] = "spdlog",
        ["spdlog/sinks/stdout_color_sinks.h"] = "spdlog",
        ["glog/logging.h"] = "glog",

        // CLI & Arguments
        ["cxxopts.hpp"] = "cxxopts",
        ["CLI/CLI.hpp"] = "cli11",

        // Math & Linear Algebra
        ["Eigen/Dense"] = "eigen3",
        ["Eigen/Core"] = "eigen3",
        ["glm/glm.hpp"] = "glm",

        // Testing
        ["catch2/catch.hpp"] = "catch2",
        ["catch2/catch_test_macros.hpp"] = "catch2",
        ["gtest/gtest.h"] = "gtest",
        ["doctest/doctest.h"] = "doctest",

        // Networking & Web
        ["httplib.h"] = "cpp-httplib",
        ["cpr/cpr.h"] = "cpr",
        ["curl/curl.h"] = "curl",

        // Graphics & Media
        ["opencv2/opencv.hpp"] = "opencv4",
        ["stb_image.h"] = "stb",
        ["stb_image_write.h"] = "stb",

        // Boost
        ["boost/algorithm/string.hpp"] = "boost",
        ["boost/asio.hpp"] = "boost-asio",
        ["boost/beast.hpp"] = "boost-beast",
        ["boost/filesystem.hpp"] = "boost-filesystem",
        ["boost/program_options.hpp"] = "boost-program-options",
        ["boost/system/error_code.hpp"] = "boost-system"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string PackageFor(string missingHeader)
    {
        if (string.IsNullOrWhiteSpace(missingHeader)) return missingHeader;

        var clean = missingHeader.Trim().Trim('<', '>', '"');

        if (HeaderToPackage.TryGetValue(clean, out var pkg))
        {
            return pkg;
        }

        if (clean.StartsWith("boost/", StringComparison.OrdinalIgnoreCase))
        {
            return "boost";
        }

        if (clean.StartsWith("opencv2/", StringComparison.OrdinalIgnoreCase))
        {
            return "opencv4";
        }

        return clean;
    }
}
