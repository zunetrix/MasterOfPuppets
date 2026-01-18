using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

class SnowEffect : ParticleEffectBase {
    readonly List<SnowParticle> snows = new();
    public float SpawnRate = 30f; // particles per second
    public float FallSpeed = 40f;
    float spawnAccumulator;
    struct SnowParticle {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float Size;
        public float Rotation;
        public float RotationSpeed;
    }


    public override void Update(float dt) {
        if (!Enabled)
            return;

        spawnAccumulator += dt * SpawnRate;

        while (spawnAccumulator >= 1f) {
            if (CanSpawn()) {
                Spawn();
            }
            spawnAccumulator--;
        }

        var screen = ImGui.GetIO().DisplaySize;

        for (int i = snows.Count - 1; i >= 0; i--) {
            var p = snows[i];

            p.Pos += p.Vel * dt;
            p.Rotation += p.RotationSpeed * dt;
            p.Life -= dt;

            if (p.Pos.Y > screen.Y + 20 || p.Life <= 0) {
                snows.RemoveAt(i);
                OnParticleRemoved();
            } else {
                snows[i] = p; // ← ESSENCIAL
            }
        }
    }

    public override void Draw(ImDrawListPtr dl) {
        if (!Enabled)
            return;

        Vector2 winPos = ImGui.GetWindowPos();
        Vector2 winSize = ImGui.GetWindowSize();

        dl.PushClipRect(winPos, winPos + winSize, true);

        uint color = ImGui.ColorConvertFloat4ToU32(
            new Vector4(1f, 1f, 1f, 0.8f)
        );

        foreach (var p in snows) {
            dl.AddCircleFilled(winPos + p.Pos, p.Size, color);
        }

        dl.PopClipRect();
    }
    // private void SpawnScreen() {
    //     var screen = ImGui.GetIO().DisplaySize;

    //     snows.Add(new SnowParticle {
    //         Pos = new Vector2(rng.Next(0, (int)screen.X), -10),
    //         Vel = new Vector2(
    //             (float)(rng.NextDouble() - 0.5f) * 15f,
    //             FallSpeed + (float)rng.NextDouble() * 20f
    //         ),
    //         Life = 20f,
    //         Size = 2 + (float)rng.NextDouble() * 3,
    //         Rotation = 0,
    //         RotationSpeed = (float)(rng.NextDouble() - 0.5f)
    //     });
    // }

    private void Spawn() {
        var winSize = ImGui.GetWindowSize();

        snows.Add(new SnowParticle {
            Pos = new Vector2(
                rng.Next(0, (int)winSize.X),
                -10
            ),
            Vel = new Vector2(
                (rng.NextSingle() - 0.5f) * 15f,
                FallSpeed + rng.NextSingle() * 20f
            ),
            Life = 20f,
            Size = 2 + rng.NextSingle() * 3,
            Rotation = 0,
            RotationSpeed = (rng.NextSingle() - 0.5f)
        });
    }
}
