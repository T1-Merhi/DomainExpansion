public interface IScene
{
    // Raised when the scene wants the SceneManager to transition (e.g. level completed, player died).
    // The scene itself never decides what happens next - it just reports what occurred.
    event Action<GameEvent> EventRaised;

    void Init(AssetManager assets, GameSettings settings);
    void Update(float deltaTime);
    void Draw();
    void Unload(); // Called automatically when switching away from this scene
}

