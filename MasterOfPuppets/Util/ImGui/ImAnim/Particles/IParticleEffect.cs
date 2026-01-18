using Dalamud.Bindings.ImGui;

interface IParticleEffect {
    void Update(float dt);
    void Draw(ImDrawListPtr drawList);
}
