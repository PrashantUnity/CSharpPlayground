using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

public readonly struct Matrix4x4D
{
    public double M11 { get; } public double M12 { get; } public double M13 { get; } public double M14 { get; }
    public double M21 { get; } public double M22 { get; } public double M23 { get; } public double M24 { get; }
    public double M31 { get; } public double M32 { get; } public double M33 { get; } public double M34 { get; }
    public double M41 { get; } public double M42 { get; } public double M43 { get; } public double M44 { get; }

    public static readonly Matrix4x4D Identity = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    );

    public Matrix4x4D(
        double m11, double m12, double m13, double m14,
        double m21, double m22, double m23, double m24,
        double m31, double m32, double m33, double m34,
        double m41, double m42, double m43, double m44)
    {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14;
        M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34;
        M41 = m41; M42 = m42; M43 = m43; M44 = m44;
    }

    public static Matrix4x4D CreateTranslation(double x, double y, double z) =>
        new(
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            x, y, z, 1
        );

    public static Matrix4x4D CreateTranslation(in Vector3D v) =>
        CreateTranslation(v.X, v.Y, v.Z);

    public static Matrix4x4D CreateScale(double x, double y, double z) =>
        new(
            x, 0, 0, 0,
            0, y, 0, 0,
            0, 0, z, 0,
            0, 0, 0, 1
        );

    public static Matrix4x4D CreateScale(double s) =>
        CreateScale(s, s, s);

    public static Matrix4x4D CreateRotationX(double radians)
    {
        double cos = System.Math.Cos(radians);
        double sin = System.Math.Sin(radians);
        return new(
            1, 0, 0, 0,
            0, cos, sin, 0,
            0, -sin, cos, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4x4D CreateRotationY(double radians)
    {
        double cos = System.Math.Cos(radians);
        double sin = System.Math.Sin(radians);
        return new(
            cos, 0, -sin, 0,
            0, 1, 0, 0,
            sin, 0, cos, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4x4D CreateRotationZ(double radians)
    {
        double cos = System.Math.Cos(radians);
        double sin = System.Math.Sin(radians);
        return new(
            cos, sin, 0, 0,
            -sin, cos, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4x4D CreateLookAt(in Vector3D eye, in Vector3D target, in Vector3D up)
    {
        var zAxis = (eye - target).Normalized;
        var xAxis = Vector3D.Cross(up, zAxis).Normalized;
        var yAxis = Vector3D.Cross(zAxis, xAxis);

        return new(
            xAxis.X, yAxis.X, zAxis.X, 0,
            xAxis.Y, yAxis.Y, zAxis.Y, 0,
            xAxis.Z, yAxis.Z, zAxis.Z, 0,
            -Vector3D.Dot(xAxis, eye), -Vector3D.Dot(yAxis, eye), -Vector3D.Dot(zAxis, eye), 1
        );
    }

    public static Matrix4x4D CreatePerspectiveFieldOfView(double fovYRadians, double aspect, double near, double far)
    {
        double yScale = 1.0 / System.Math.Tan(fovYRadians * 0.5);
        double xScale = yScale / aspect;
        double fRange = far / (near - far);

        return new(
            xScale, 0, 0, 0,
            0, yScale, 0, 0,
            0, 0, fRange, -1,
            0, 0, near * fRange, 0
        );
    }

    public static Matrix4x4D CreateOrthographic(double width, double height, double near, double far)
    {
        double fn = 1.0 / (near - far);
        return new(
            2.0 / width, 0, 0, 0,
            0, 2.0 / height, 0, 0,
            0, 0, 2.0 * fn, 0,
            0, 0, (near + far) * fn, 1
        );
    }

    public Vector3D Transform(in Vector3D v)
    {
        double x = v.X * M11 + v.Y * M21 + v.Z * M31 + M41;
        double y = v.X * M12 + v.Y * M22 + v.Z * M32 + M42;
        double z = v.X * M13 + v.Y * M23 + v.Z * M33 + M43;
        double w = v.X * M14 + v.Y * M24 + v.Z * M34 + M44;

        if (System.Math.Abs(w) > 1e-12 && System.Math.Abs(w - 1.0) > 1e-12)
        {
            return new Vector3D(x / w, y / w, z / w);
        }
        return new Vector3D(x, y, z);
    }

    public Vector3D TransformVector(in Vector3D v)
    {
        return new Vector3D(
            v.X * M11 + v.Y * M21 + v.Z * M31,
            v.X * M12 + v.Y * M22 + v.Z * M32,
            v.X * M13 + v.Y * M23 + v.Z * M33
        );
    }

    public static Matrix4x4D operator *(in Matrix4x4D a, in Matrix4x4D b)
    {
        return new(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

            a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
        );
    }
}
