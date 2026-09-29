using System;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Camera3D
{
    private double _pitch = 30.0;
    private double _distance = 18.0;

    public double Yaw { get; set; } = 45.0;

    public double Pitch
    {
        get => _pitch;
        set => _pitch = System.Math.Clamp(value, -89.5, 89.5);
    }

    public double Distance
    {
        get => _distance;
        set => _distance = System.Math.Clamp(value, 2.0, 500.0);
    }

    public Vector3D Target { get; set; } = Vector3D.Zero;
    public double FieldOfView { get; set; } = 45.0; // degrees
    public bool IsOrthographic { get; set; }
    public double OrthographicSize { get; set; } = 12.0;

    public Vector3D GetEyePosition()
    {
        double yawRad = Yaw * System.Math.PI / 180.0;
        double pitchRad = Pitch * System.Math.PI / 180.0;

        double cosPitch = System.Math.Cos(pitchRad);
        double sinPitch = System.Math.Sin(pitchRad);
        double cosYaw = System.Math.Cos(yawRad);
        double sinYaw = System.Math.Sin(yawRad);

        double x = Target.X + Distance * cosPitch * sinYaw;
        double y = Target.Y + Distance * sinPitch;
        double z = Target.Z + Distance * cosPitch * cosYaw;

        return new Vector3D(x, y, z);
    }

    public Matrix4x4D GetViewMatrix()
    {
        var eye = GetEyePosition();
        return Matrix4x4D.CreateLookAt(eye, Target, Vector3D.UnitY);
    }

    public Matrix4x4D GetProjectionMatrix(double aspectRatio)
    {
        aspectRatio = System.Math.Max(0.001, aspectRatio);
        if (IsOrthographic)
        {
            double h = OrthographicSize;
            double w = h * aspectRatio;
            return Matrix4x4D.CreateOrthographic(w, h, 0.1, 1000.0);
        }
        else
        {
            double fovRad = FieldOfView * System.Math.PI / 180.0;
            return Matrix4x4D.CreatePerspectiveFieldOfView(fovRad, aspectRatio, 0.1, 1000.0);
        }
    }

    public void SetIsometric()
    {
        Yaw = 45.0;
        Pitch = 35.264;
        Target = Vector3D.Zero;
    }

    public void SetTop()
    {
        Yaw = 0.0;
        Pitch = 89.0;
        Target = Vector3D.Zero;
    }

    public void SetFront()
    {
        Yaw = 0.0;
        Pitch = 0.0;
        Target = Vector3D.Zero;
    }

    public void SetSide()
    {
        Yaw = 90.0;
        Pitch = 0.0;
        Target = Vector3D.Zero;
    }

    public void Reset()
    {
        SetIsometric();
        Distance = 18.0;
        IsOrthographic = false;
    }

    public void Orbit(double deltaYaw, double deltaPitch)
    {
        Yaw = (Yaw + deltaYaw) % 360.0;
        if (Yaw < 0) Yaw += 360.0;
        Pitch += deltaPitch;
    }

    public void Pan(double deltaScreenX, double deltaScreenY, double viewportWidth, double viewportHeight)
    {
        double factor = Distance / System.Math.Max(viewportWidth, viewportHeight) * 1.5;
        double yawRad = Yaw * System.Math.PI / 180.0;

        // Camera right and up in world space
        var right = new Vector3D(System.Math.Cos(yawRad), 0, -System.Math.Sin(yawRad));
        var up = Vector3D.UnitY;

        Target += -right * (deltaScreenX * factor) + up * (deltaScreenY * factor);
    }

    public void Zoom(double zoomDelta)
    {
        Distance *= zoomDelta;
    }
}
