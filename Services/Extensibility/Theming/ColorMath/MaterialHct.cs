// Ported from Google's material-color-utilities (https://github.com/material-foundation/material-color-utilities),
// Java sources utils/ColorUtils, utils/MathUtils, hct/ViewingConditions, hct/Cam16, hct/HctSolver, hct/Hct and
// palettes/TonalPalette.
//
// Copyright 2021 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.
//
// Changes from the original: C# port; the solver's matrices and critical planes are computed from the default viewing
// conditions instead of being listed as constants (same values, to floating-point precision).

using System;
using System.Collections.Concurrent;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>Colour-space helpers of material-color-utilities (sRGB, XYZ, L*). Channels are 0..100 when linear.</summary>
internal static class MaterialColorUtils
{
    internal static readonly double[][] SrgbToXyz =
    [
        [0.41233895, 0.35762064, 0.18051042],
        [0.2126, 0.7152, 0.0722],
        [0.01932141, 0.11916382, 0.95034478],
    ];

    internal static readonly double[] WhitePointD65 = [95.047, 100.0, 108.883];

    public static int ArgbFromRgb(int red, int green, int blue) =>
        unchecked((int)0xFF000000) | ((red & 255) << 16) | ((green & 255) << 8) | (blue & 255);

    public static int ArgbFromLinrgb(double[] linrgb) =>
        ArgbFromRgb(Delinearized(linrgb[0]), Delinearized(linrgb[1]), Delinearized(linrgb[2]));

    public static int RedFromArgb(int argb) => (argb >> 16) & 255;
    public static int GreenFromArgb(int argb) => (argb >> 8) & 255;
    public static int BlueFromArgb(int argb) => argb & 255;

    public static double[] XyzFromArgb(int argb) =>
        MatrixMultiply([Linearized(RedFromArgb(argb)), Linearized(GreenFromArgb(argb)), Linearized(BlueFromArgb(argb))], SrgbToXyz);

    public static int ArgbFromLstar(double lstar)
    {
        var component = Delinearized(YFromLstar(lstar));
        return ArgbFromRgb(component, component, component);
    }

    public static double LstarFromArgb(int argb) => 116.0 * LabF(XyzFromArgb(argb)[1] / 100.0) - 16.0;

    public static double YFromLstar(double lstar) => 100.0 * LabInvf((lstar + 16.0) / 116.0);

    public static double LstarFromY(double y) => LabF(y / 100.0) * 116.0 - 16.0;

    /// <summary>An 8-bit channel to linear light, 0..100.</summary>
    public static double Linearized(int rgbComponent)
    {
        var normalized = rgbComponent / 255.0;
        return normalized <= 0.040449936 ? normalized / 12.92 * 100.0 : Math.Pow((normalized + 0.055) / 1.055, 2.4) * 100.0;
    }

    /// <summary>Linear light 0..100 to an 8-bit channel.</summary>
    public static int Delinearized(double rgbComponent)
    {
        var normalized = rgbComponent / 100.0;
        var delinearized = normalized <= 0.0031308 ? normalized * 12.92 : 1.055 * Math.Pow(normalized, 1.0 / 2.4) - 0.055;
        return Math.Clamp((int)Math.Round(delinearized * 255.0), 0, 255);
    }

    public static double LabF(double t)
    {
        const double e = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        return t > e ? Math.Cbrt(t) : (kappa * t + 16) / 116;
    }

    public static double LabInvf(double ft)
    {
        const double e = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        var ft3 = ft * ft * ft;
        return ft3 > e ? ft3 : (116 * ft - 16) / kappa;
    }

    public static double SanitizeDegrees(double degrees)
    {
        degrees %= 360.0;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }

    public static double Lerp(double start, double stop, double amount) => (1.0 - amount) * start + amount * stop;

    public static double[] MatrixMultiply(double[] row, double[][] matrix) =>
    [
        row[0] * matrix[0][0] + row[1] * matrix[0][1] + row[2] * matrix[0][2],
        row[0] * matrix[1][0] + row[1] * matrix[1][1] + row[2] * matrix[1][2],
        row[0] * matrix[2][0] + row[1] * matrix[2][1] + row[2] * matrix[2][2],
    ];

    public static int ToArgb(ColorRgb color) => ArgbFromRgb(color.R, color.G, color.B);

    public static ColorRgb ToColorRgb(int argb) => new((byte)RedFromArgb(argb), (byte)GreenFromArgb(argb), (byte)BlueFromArgb(argb));
}

/// <summary>The environment a colour is seen in, for CAM16 (material-color-utilities' ViewingConditions).</summary>
internal sealed class MaterialViewingConditions
{
    /// <summary>sRGB-like viewing: D65 white, 200 lux, a mid-grey (L* 50) background, average surround.</summary>
    public static readonly MaterialViewingConditions Default = Make(
        MaterialColorUtils.WhitePointD65, 200.0 / Math.PI * MaterialColorUtils.YFromLstar(50.0) / 100.0, 50.0, 2.0, false);

    private MaterialViewingConditions(double n, double aw, double nbb, double ncb, double c, double nc, double[] rgbD, double fl, double flRoot, double z)
    {
        N = n;
        Aw = aw;
        Nbb = nbb;
        Ncb = ncb;
        C = c;
        Nc = nc;
        RgbD = rgbD;
        Fl = fl;
        FlRoot = flRoot;
        Z = z;
    }

    public double N { get; }
    public double Aw { get; }
    public double Nbb { get; }
    public double Ncb { get; }
    public double C { get; }
    public double Nc { get; }
    public double[] RgbD { get; }
    public double Fl { get; }
    public double FlRoot { get; }
    public double Z { get; }

    public static MaterialViewingConditions Make(double[] whitePoint, double adaptingLuminance, double backgroundLstar, double surround, bool discountingIlluminant)
    {
        backgroundLstar = Math.Max(0.1, backgroundLstar);
        var matrix = MaterialCam16.XyzToCam16Rgb;
        var xyz = whitePoint;
        var rW = xyz[0] * matrix[0][0] + xyz[1] * matrix[0][1] + xyz[2] * matrix[0][2];
        var gW = xyz[0] * matrix[1][0] + xyz[1] * matrix[1][1] + xyz[2] * matrix[1][2];
        var bW = xyz[0] * matrix[2][0] + xyz[1] * matrix[2][1] + xyz[2] * matrix[2][2];
        var f = 0.8 + surround / 10.0;
        var c = f >= 0.9 ? MaterialColorUtils.Lerp(0.59, 0.69, (f - 0.9) * 10.0) : MaterialColorUtils.Lerp(0.525, 0.59, (f - 0.8) * 10.0);
        var d = discountingIlluminant ? 1.0 : f * (1.0 - 1.0 / 3.6 * Math.Exp((-adaptingLuminance - 42.0) / 92.0));
        d = Math.Clamp(d, 0.0, 1.0);
        var nc = f;
        double[] rgbD = [d * (100.0 / rW) + 1.0 - d, d * (100.0 / gW) + 1.0 - d, d * (100.0 / bW) + 1.0 - d];
        var k = 1.0 / (5.0 * adaptingLuminance + 1.0);
        var k4 = k * k * k * k;
        var k4F = 1.0 - k4;
        var fl = k4 * adaptingLuminance + 0.1 * k4F * k4F * Math.Cbrt(5.0 * adaptingLuminance);
        var n = MaterialColorUtils.YFromLstar(backgroundLstar) / whitePoint[1];
        var z = 1.48 + Math.Sqrt(n);
        var nbb = 0.725 / Math.Pow(n, 0.2);
        var ncb = nbb;
        double[] rgbAFactors =
        [
            Math.Pow(fl * rgbD[0] * rW / 100.0, 0.42),
            Math.Pow(fl * rgbD[1] * gW / 100.0, 0.42),
            Math.Pow(fl * rgbD[2] * bW / 100.0, 0.42),
        ];
        double[] rgbA =
        [
            400.0 * rgbAFactors[0] / (rgbAFactors[0] + 27.13),
            400.0 * rgbAFactors[1] / (rgbAFactors[1] + 27.13),
            400.0 * rgbAFactors[2] / (rgbAFactors[2] + 27.13),
        ];
        var aw = (2.0 * rgbA[0] + rgbA[1] + 0.05 * rgbA[2]) * nbb;
        return new MaterialViewingConditions(n, aw, nbb, ncb, c, nc, rgbD, fl, Math.Pow(fl, 0.25), z);
    }
}

/// <summary>CAM16 appearance of a colour (material-color-utilities' Cam16): only what HCT needs.</summary>
internal readonly record struct MaterialCam16(double Hue, double Chroma, double J, double Q, double M, double S)
{
    internal static readonly double[][] XyzToCam16Rgb =
    [
        [0.401288, 0.650173, -0.051461],
        [-0.250268, 1.204414, 0.045854],
        [-0.002079, 0.048952, 0.953127],
    ];

    public static MaterialCam16 FromArgb(int argb) => FromXyz(MaterialColorUtils.XyzFromArgb(argb), MaterialViewingConditions.Default);

    public static MaterialCam16 FromXyz(double[] xyz, MaterialViewingConditions vc)
    {
        double x = xyz[0], y = xyz[1], z = xyz[2];
        var rC = 0.401288 * x + 0.650173 * y - 0.051461 * z;
        var gC = -0.250268 * x + 1.204414 * y + 0.045854 * z;
        var bC = -0.002079 * x + 0.048952 * y + 0.953127 * z;

        var rD = vc.RgbD[0] * rC;
        var gD = vc.RgbD[1] * gC;
        var bD = vc.RgbD[2] * bC;

        var rAF = Math.Pow(vc.Fl * Math.Abs(rD) / 100.0, 0.42);
        var gAF = Math.Pow(vc.Fl * Math.Abs(gD) / 100.0, 0.42);
        var bAF = Math.Pow(vc.Fl * Math.Abs(bD) / 100.0, 0.42);
        var rA = Math.Sign(rD) * 400.0 * rAF / (rAF + 27.13);
        var gA = Math.Sign(gD) * 400.0 * gAF / (gAF + 27.13);
        var bA = Math.Sign(bD) * 400.0 * bAF / (bAF + 27.13);

        var a = (11.0 * rA + -12.0 * gA + bA) / 11.0;
        var b = (rA + gA - 2.0 * bA) / 9.0;
        var u = (20.0 * rA + 20.0 * gA + 21.0 * bA) / 20.0;
        var p2 = (40.0 * rA + 20.0 * gA + bA) / 20.0;

        var atanDegrees = Math.Atan2(b, a) * 180.0 / Math.PI;
        var hue = atanDegrees < 0 ? atanDegrees + 360.0 : atanDegrees >= 360 ? atanDegrees - 360.0 : atanDegrees;

        var ac = p2 * vc.Nbb;
        var j = 100.0 * Math.Pow(ac / vc.Aw, vc.C * vc.Z);
        var q = 4.0 / vc.C * Math.Sqrt(j / 100.0) * (vc.Aw + 4.0) * vc.FlRoot;

        var huePrime = hue < 20.14 ? hue + 360 : hue;
        var eHue = 0.25 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);
        var p1 = 50000.0 / 13.0 * eHue * vc.Nc * vc.Ncb;
        var t = p1 * Math.Sqrt(a * a + b * b) / (u + 0.305);
        var alpha = Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73) * Math.Pow(t, 0.9);
        var c = alpha * Math.Sqrt(j / 100.0);
        var m = c * vc.FlRoot;
        var s = 50.0 * Math.Sqrt(alpha * vc.C / (vc.Aw + 4.0));
        return new MaterialCam16(hue, c, j, q, m, s);
    }
}

/// <summary>
/// Finds the sRGB colour of a given HCT hue, chroma and tone, lowering chroma to what sRGB can show at that tone and
/// hue (material-color-utilities' HctSolver).
/// </summary>
internal static class MaterialHctSolver
{
    // Linear RGB (0..100) → CAM16 RGB, discounted for the default viewing conditions and scaled by FL/100; and back.
    private static readonly double[][] ScaledDiscountFromLinrgb = BuildScaledDiscount();
    private static readonly double[][] LinrgbFromScaledDiscount = Invert(ScaledDiscountFromLinrgb);
    private static readonly double[] YFromLinrgb = [0.2126, 0.7152, 0.0722];

    // The linear values at which an 8-bit channel changes: (i + 0.5) / 255, linearised, for i = 0..254.
    private static readonly double[] CriticalPlanes = BuildCriticalPlanes();

    private static double[][] BuildScaledDiscount()
    {
        var vc = MaterialViewingConditions.Default;
        var m16 = MaterialCam16.XyzToCam16Rgb;
        var toXyz = MaterialColorUtils.SrgbToXyz;
        var result = new double[3][];
        for (var row = 0; row < 3; row++)
        {
            result[row] = new double[3];
            for (var col = 0; col < 3; col++)
            {
                var sum = 0.0;
                for (var k = 0; k < 3; k++) sum += m16[row][k] * toXyz[k][col];
                result[row][col] = sum * vc.RgbD[row] * vc.Fl / 100.0;
            }
        }

        return result;
    }

    private static double[][] Invert(double[][] m)
    {
        var det = m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
                - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
                + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]);
        var inv = 1.0 / det;
        return
        [
            [
                (m[1][1] * m[2][2] - m[1][2] * m[2][1]) * inv,
                (m[0][2] * m[2][1] - m[0][1] * m[2][2]) * inv,
                (m[0][1] * m[1][2] - m[0][2] * m[1][1]) * inv,
            ],
            [
                (m[1][2] * m[2][0] - m[1][0] * m[2][2]) * inv,
                (m[0][0] * m[2][2] - m[0][2] * m[2][0]) * inv,
                (m[0][2] * m[1][0] - m[0][0] * m[1][2]) * inv,
            ],
            [
                (m[1][0] * m[2][1] - m[1][1] * m[2][0]) * inv,
                (m[0][1] * m[2][0] - m[0][0] * m[2][1]) * inv,
                (m[0][0] * m[1][1] - m[0][1] * m[1][0]) * inv,
            ],
        ];
    }

    private static double[] BuildCriticalPlanes()
    {
        var planes = new double[255];
        for (var i = 0; i < planes.Length; i++)
        {
            var normalized = (i + 0.5) / 255.0;
            planes[i] = (normalized <= 0.040449936 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4)) * 100.0;
        }

        return planes;
    }

    private static double SanitizeRadians(double angle) => (angle + Math.PI * 8) % (Math.PI * 2);

    private static double TrueDelinearized(double rgbComponent)
    {
        var normalized = rgbComponent / 100.0;
        var delinearized = normalized <= 0.0031308 ? normalized * 12.92 : 1.055 * Math.Pow(normalized, 1.0 / 2.4) - 0.055;
        return delinearized * 255.0;
    }

    private static double ChromaticAdaptation(double component)
    {
        var af = Math.Pow(Math.Abs(component), 0.42);
        return Math.Sign(component) * 400.0 * af / (af + 27.13);
    }

    private static double HueOf(double[] linrgb)
    {
        var scaledDiscount = MaterialColorUtils.MatrixMultiply(linrgb, ScaledDiscountFromLinrgb);
        var rA = ChromaticAdaptation(scaledDiscount[0]);
        var gA = ChromaticAdaptation(scaledDiscount[1]);
        var bA = ChromaticAdaptation(scaledDiscount[2]);
        var a = (11.0 * rA + -12.0 * gA + bA) / 11.0;
        var b = (rA + gA - 2.0 * bA) / 9.0;
        return Math.Atan2(b, a);
    }

    private static bool AreInCyclicOrder(double a, double b, double c) => SanitizeRadians(b - a) < SanitizeRadians(c - a);

    private static double Intercept(double source, double mid, double target) => (mid - source) / (target - source);

    private static double[] LerpPoint(double[] source, double t, double[] target) =>
    [
        source[0] + (target[0] - source[0]) * t,
        source[1] + (target[1] - source[1]) * t,
        source[2] + (target[2] - source[2]) * t,
    ];

    private static double[] SetCoordinate(double[] source, double coordinate, double[] target, int axis) =>
        LerpPoint(source, Intercept(source[axis], coordinate, target[axis]), target);

    private static bool IsBounded(double x) => x is >= 0.0 and <= 100.0;

    // The nth vertex of the polygon where the plane of luminance y cuts the RGB cube, or (-1,-1,-1).
    private static double[] NthVertex(double y, int n)
    {
        double kR = YFromLinrgb[0], kG = YFromLinrgb[1], kB = YFromLinrgb[2];
        var coordA = n % 4 <= 1 ? 0.0 : 100.0;
        var coordB = n % 2 == 0 ? 0.0 : 100.0;
        if (n < 4)
        {
            double g = coordA, b = coordB;
            var r = (y - g * kG - b * kB) / kR;
            return IsBounded(r) ? [r, g, b] : [-1.0, -1.0, -1.0];
        }

        if (n < 8)
        {
            double b = coordA, r = coordB;
            var g = (y - r * kR - b * kB) / kG;
            return IsBounded(g) ? [r, g, b] : [-1.0, -1.0, -1.0];
        }

        {
            double r = coordA, g = coordB;
            var b = (y - r * kR - g * kG) / kB;
            return IsBounded(b) ? [r, g, b] : [-1.0, -1.0, -1.0];
        }
    }

    private static (double[] Left, double[] Right) BisectToSegment(double y, double targetHue)
    {
        double[] left = [-1.0, -1.0, -1.0];
        var right = left;
        double leftHue = 0.0, rightHue = 0.0;
        var initialized = false;
        var uncut = true;
        for (var n = 0; n < 12; n++)
        {
            var mid = NthVertex(y, n);
            if (mid[0] < 0) continue;
            var midHue = HueOf(mid);
            if (!initialized)
            {
                left = mid;
                right = mid;
                leftHue = midHue;
                rightHue = midHue;
                initialized = true;
                continue;
            }

            if (uncut || AreInCyclicOrder(leftHue, midHue, rightHue))
            {
                uncut = false;
                if (AreInCyclicOrder(leftHue, targetHue, midHue))
                {
                    right = mid;
                    rightHue = midHue;
                }
                else
                {
                    left = mid;
                    leftHue = midHue;
                }
            }
        }

        return (left, right);
    }

    private static double[] Midpoint(double[] a, double[] b) => [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2];

    private static int CriticalPlaneBelow(double x) => (int)Math.Floor(x - 0.5);

    private static int CriticalPlaneAbove(double x) => (int)Math.Ceiling(x - 0.5);

    // The most chromatic colour of the hue at luminance y (on the surface of the RGB cube).
    private static double[] BisectToLimit(double y, double targetHue)
    {
        var (left, right) = BisectToSegment(y, targetHue);
        var leftHue = HueOf(left);
        for (var axis = 0; axis < 3; axis++)
        {
            if (left[axis] == right[axis]) continue;
            int lPlane, rPlane;
            if (left[axis] < right[axis])
            {
                lPlane = CriticalPlaneBelow(TrueDelinearized(left[axis]));
                rPlane = CriticalPlaneAbove(TrueDelinearized(right[axis]));
            }
            else
            {
                lPlane = CriticalPlaneAbove(TrueDelinearized(left[axis]));
                rPlane = CriticalPlaneBelow(TrueDelinearized(right[axis]));
            }

            for (var i = 0; i < 8; i++)
            {
                if (Math.Abs(rPlane - lPlane) <= 1) break;
                var mPlane = (int)Math.Floor((lPlane + rPlane) / 2.0);
                var mid = SetCoordinate(left, CriticalPlanes[mPlane], right, axis);
                var midHue = HueOf(mid);
                if (AreInCyclicOrder(leftHue, targetHue, midHue))
                {
                    right = mid;
                    rPlane = mPlane;
                }
                else
                {
                    left = mid;
                    leftHue = midHue;
                    lPlane = mPlane;
                }
            }
        }

        return Midpoint(left, right);
    }

    private static double InverseChromaticAdaptation(double adapted)
    {
        var adaptedAbs = Math.Abs(adapted);
        var baseValue = Math.Max(0, 27.13 * adaptedAbs / (400.0 - adaptedAbs));
        return Math.Sign(adapted) * Math.Pow(baseValue, 1.0 / 0.42);
    }

    // Newton's method on J for the exact hue and chroma; 0 when that colour is outside sRGB.
    private static int FindResultByJ(double hueRadians, double chroma, double y)
    {
        var j = Math.Sqrt(y) * 11.0;
        var vc = MaterialViewingConditions.Default;
        var tInnerCoeff = 1 / Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73);
        var eHue = 0.25 * (Math.Cos(hueRadians + 2.0) + 3.8);
        var p1 = eHue * (50000.0 / 13.0) * vc.Nc * vc.Ncb;
        var hSin = Math.Sin(hueRadians);
        var hCos = Math.Cos(hueRadians);
        for (var iterationRound = 0; iterationRound < 5; iterationRound++)
        {
            var jNormalized = j / 100.0;
            var alpha = chroma == 0.0 || j == 0.0 ? 0.0 : chroma / Math.Sqrt(jNormalized);
            var t = Math.Pow(alpha * tInnerCoeff, 1.0 / 0.9);
            var ac = vc.Aw * Math.Pow(jNormalized, 1.0 / vc.C / vc.Z);
            var p2 = ac / vc.Nbb;
            var gamma = 23.0 * (p2 + 0.305) * t / (23.0 * p1 + 11 * t * hCos + 108.0 * t * hSin);
            var a = gamma * hCos;
            var b = gamma * hSin;
            var rA = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
            var gA = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
            var bA = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;
            double[] scaled = [InverseChromaticAdaptation(rA), InverseChromaticAdaptation(gA), InverseChromaticAdaptation(bA)];
            var linrgb = MaterialColorUtils.MatrixMultiply(scaled, LinrgbFromScaledDiscount);
            if (linrgb[0] < 0 || linrgb[1] < 0 || linrgb[2] < 0) return 0;
            var fnj = YFromLinrgb[0] * linrgb[0] + YFromLinrgb[1] * linrgb[1] + YFromLinrgb[2] * linrgb[2];
            if (fnj <= 0) return 0;
            if (iterationRound == 4 || Math.Abs(fnj - y) < 0.002)
            {
                if (linrgb[0] > 100.01 || linrgb[1] > 100.01 || linrgb[2] > 100.01) return 0;
                return MaterialColorUtils.ArgbFromLinrgb(linrgb);
            }

            j -= (fnj - y) * j / (2 * fnj);
        }

        return 0;
    }

    /// <summary>The sRGB colour (ARGB) of the HCT hue (degrees), chroma and tone (L*), with chroma lowered if needed.</summary>
    public static int SolveToInt(double hueDegrees, double chroma, double lstar)
    {
        if (chroma < 0.0001 || lstar < 0.0001 || lstar > 99.9999) return MaterialColorUtils.ArgbFromLstar(lstar);
        hueDegrees = MaterialColorUtils.SanitizeDegrees(hueDegrees);
        var hueRadians = hueDegrees / 180 * Math.PI;
        var y = MaterialColorUtils.YFromLstar(lstar);
        var exactAnswer = FindResultByJ(hueRadians, chroma, y);
        if (exactAnswer != 0) return exactAnswer;
        return MaterialColorUtils.ArgbFromLinrgb(BisectToLimit(y, hueRadians));
    }
}

/// <summary>
/// HCT (Material Design 3): CAM16 <see cref="Hue"/> and <see cref="Chroma"/> with <see cref="Tone"/> = CIE L* (0 black,
/// 100 white). Tone alone predicts contrast: a tone difference of 40 gives at least 3:1, 50 at least 4.5:1.
/// </summary>
public readonly record struct Hct(double Hue, double Chroma, double Tone)
{
    public static Hct FromRgb(ColorRgb color)
    {
        var argb = MaterialColorUtils.ToArgb(color);
        var cam = MaterialCam16.FromArgb(argb);
        return new Hct(cam.Hue, cam.Chroma, MaterialColorUtils.LstarFromArgb(argb));
    }

    /// <summary>The displayable colour: chroma is lowered to what sRGB can show at this hue and tone.</summary>
    public ColorRgb ToRgb() => MaterialColorUtils.ToColorRgb(MaterialHctSolver.SolveToInt(Hue, Chroma, Tone));

    /// <summary>The colour actually shown, read back (its chroma may be lower than asked for).</summary>
    public Hct Resolved() => FromRgb(ToRgb());
}

/// <summary>One hue and chroma at every tone (material-color-utilities' TonalPalette). Tones are cached.</summary>
public sealed class TonalPalette
{
    private readonly ConcurrentDictionary<int, ColorRgb> _cache = new();

    private TonalPalette(double hue, double chroma)
    {
        Hue = hue;
        Chroma = chroma;
    }

    public double Hue { get; }
    public double Chroma { get; }

    public static TonalPalette FromHueAndChroma(double hue, double chroma) => new(hue, chroma);

    public static TonalPalette FromRgb(ColorRgb color)
    {
        var hct = Hct.FromRgb(color);
        return new TonalPalette(hct.Hue, hct.Chroma);
    }

    /// <summary>The colour at <paramref name="tone"/> (0..100).</summary>
    public ColorRgb Tone(int tone) => _cache.GetOrAdd(tone, t => new Hct(Hue, Chroma, t).ToRgb());
}
