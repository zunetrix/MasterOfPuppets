using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

class HeartEffect : ParticleEffectBase {

    readonly List<HeartParticle> hearts = new();
    public float SpawnRate = 10f;
    public float RiseSpeed = 60f;
    float spawnAcc;
    struct HeartParticle {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float Size;
        public float Rotation;
    }

    public override void Update(float dt) {
        if (!Enabled)
            return;

        spawnAcc += dt * SpawnRate;

        while (spawnAcc >= 1f) {
            if (CanSpawn())
                Spawn();

            spawnAcc--;
        }

        for (int i = hearts.Count - 1; i >= 0; i--) {
            var p = hearts[i];

            p.Pos += p.Vel * dt;
            p.Rotation += 0.5f * dt;
            p.Life -= dt;

            if (p.Life <= 0 || p.Pos.Y < -p.Size * 2) {
                hearts.RemoveAt(i);
                OnParticleRemoved();
            } else {
                hearts[i] = p;
            }
        }
    }
    public override void Draw(ImDrawListPtr dl) {
        if (!Enabled)
            return;

        Vector2 winPos = ImGui.GetWindowPos();
        Vector2 winSize = ImGui.GetWindowSize();

        Vector2 min = winPos;
        Vector2 max = winPos + winSize;

        dl.PushClipRect(min, max, true);

        uint color = ImGui.ColorConvertFloat4ToU32(
            new Vector4(1f, 0.2f, 0.4f, 0.9f)
        );

        foreach (var p in hearts) {
            Vector2 screenPos = winPos + p.Pos;
            DrawHeart(dl, screenPos, p.Size, color);
        }

        dl.PopClipRect();
    }

    private void Spawn() {
        Vector2 winSize = ImGui.GetWindowSize();

        hearts.Add(new HeartParticle {
            Pos = new Vector2(
                rng.Next(20, (int)winSize.X - 20),
                winSize.Y + 20
            ),
            Vel = new Vector2(
                (float)(rng.NextDouble() - 0.5f) * 30f,
                -RiseSpeed - (float)rng.NextDouble() * 40f
            ),
            Life = 10f,
            Size = rng.Next(8, 20),
            Rotation = 0f
        });
    }

    private static void DrawHeart(ImDrawListPtr dl, Vector2 pos, float size, uint color) {
        dl.AddCircleFilled(pos + new Vector2(-size / 2, 0), size / 2, color);
        dl.AddCircleFilled(pos + new Vector2(size / 2, 0), size / 2, color);
        dl.AddTriangleFilled(
            pos + new Vector2(-size, 0),
            pos + new Vector2(size, 0),
            pos + new Vector2(0, size),
            color
        );
    }
}
