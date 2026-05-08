using System.Numerics;

namespace MasterOfPuppets.Movement;

public readonly record struct SimpleMovementContext(
    Vector3 Destination,
    float Precision,
    float? FaceDirection);

internal enum SimpleMovementUpdateResult {
    Running,
    Complete,
}

internal interface ISimpleMovementStrategy {
    string Name { get; }
    bool UsesNativeStopOnCompletion { get; }
    void Start(SimpleMovementContext context);
    SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition);
    void Stop();
}
