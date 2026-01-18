using System;

using Dalamud.Bindings.ImGui;

abstract class ParticleEffectBase : IParticleEffect {
    protected readonly Random rng = new();

    private bool _enabled = true;
    public bool Enabled {
        get => _enabled;
        set {
            if (_enabled == value)
                return;

            _enabled = value;

            if (!_enabled)
                OnDisabled();
        }
    }

    public int MaxParticles = 300;

    protected int ParticleCount { get; private set; }

    protected virtual void OnDisabled() {
        Clear();
    }

    protected bool CanSpawn(int count = 1) {
        if (!Enabled)
            return false;

        if (ParticleCount + count > MaxParticles)
            return false;

        ParticleCount += count;
        return true;
    }

    protected int CanSpawnPartial(int desired) {
        if (!Enabled)
            return 0;

        int available = MaxParticles - ParticleCount;
        int spawn = Math.Min(desired, available);

        if (spawn > 0)
            ParticleCount += spawn;

        return spawn;
    }

    protected void OnParticleRemoved(int count = 1) {
        ParticleCount -= count;
        if (ParticleCount < 0)
            ParticleCount = 0;
    }

    protected void Clear() {
        ParticleCount = 0;
    }

    public abstract void Update(float dt);
    public abstract void Draw(ImDrawListPtr drawList);
}
