namespace Dealership.Domain.Inspections;

public enum InspectionCategory
{
    FrameChassis,
    EngineTransmission,
    ExteriorPaint,
    InteriorElectrical,
    UnderbodyBrakesSuspension
}

public sealed record InspectionPoint(string Code, InspectionCategory Category, string Description);
