using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

class FireworkEffect : ParticleEffectBase {

    struct FireworkParticle {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Size;
        public Vector4 Color;
    }

    readonly List<FireworkParticle> particles = new();
    // timings
    public float ExplosionInterval = 1.0f;
    public float ExplosionIntervalDelayed = 1.25f;

    float timerA;
    float timerB;
    float positionTime;

    const float Gravity = 220f;

    public override void Update(float dt) {
        if (!Enabled)
            return;

        timerA += dt;
        timerB += dt;
        positionTime += dt * 0.2f;

        Vector2 winSize = ImGui.GetWindowSize();

        if (timerA >= ExplosionInterval) {
            Explode(GetLauncherPos(positionTime, winSize));
            timerA = 0f;
        }

        if (timerB >= ExplosionIntervalDelayed) {
            Explode(GetLauncherPos(positionTime + 0.15f, winSize));
            timerB = 0f;
        }

        UpdateParticles(dt);
    }

    public override void Draw(ImDrawListPtr dl) {
        if (!Enabled)
            return;

        Vector2 winPos = ImGui.GetWindowPos();
        Vector2 winSize = ImGui.GetWindowSize();

        Vector2 min = winPos;
        Vector2 max = winPos + winSize;

        dl.PushClipRect(min, max, true);

        foreach (var p in particles) {
            float alpha = p.Life / p.MaxLife;

            uint col = ImGui.ColorConvertFloat4ToU32(
                new Vector4(p.Color.X, p.Color.Y, p.Color.Z, alpha)
            );

            Vector2 screenPos = winPos + p.Pos;
            dl.AddCircleFilled(screenPos, p.Size, col);
        }

        dl.PopClipRect();
    }

    private void UpdateParticles(float dt) {
        for (int i = particles.Count - 1; i >= 0; i--) {
            var p = particles[i];

            p.Vel.Y += Gravity * dt;
            p.Pos += p.Vel * dt;
            p.Life -= dt;

            if (p.Life <= 0) {
                particles.RemoveAt(i);
                OnParticleRemoved();
                continue;
            }

            particles[i] = p;
        }
    }

    private void Explode(Vector2 pos) {
        const int desired = 50;

        int spawn = CanSpawnPartial(desired);
        if (spawn == 0)
            return;

        for (int i = 0; i < spawn; i++) {
            float angle = rng.NextSingle() * MathF.Tau;
            float speed = 80f + rng.NextSingle() * 160f;

            particles.Add(new FireworkParticle {
                Pos = pos,
                Vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Life = 1.2f,
                MaxLife = 1.2f,
                Size = 3f,
                Color = RandomHsv()
            });
        }
    }

    private static Vector2 GetLauncherPos(float t, Vector2 size) {
        t %= 1f;

        return t switch {
            < 0.2f => new Vector2(size.X * 0.40f, size.Y * 0.10f),
            < 0.4f => new Vector2(size.X * 0.30f, size.Y * 0.40f),
            < 0.6f => new Vector2(size.X * 0.70f, size.Y * 0.20f),
            < 0.8f => new Vector2(size.X * 0.20f, size.Y * 0.30f),
            _ => new Vector2(size.X * 0.80f, size.Y * 0.30f),
        };
    }

    private Vector4 RandomHsv() {
        float h = rng.NextSingle();
        float s = 1f;
        float v = 1f;

        return HsvToRgb(h, s, v);
    }

    static Vector4 HsvToRgb(float h, float s, float v) {
        float r = 0, g = 0, b = 0;

        int i = (int)(h * 6);
        float f = h * 6 - i;
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        switch (i % 6) {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }

        return new Vector4(r, g, b, 1f);
    }
}
