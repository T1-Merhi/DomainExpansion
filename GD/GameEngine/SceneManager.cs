public class SceneManager
{
    private IScene _currentScene;
    private readonly GameEngine _engine;

    public SceneManager(GameEngine engine)
    {
        _engine = engine;
    }

    public void ChangeScene(IScene newScene)
    {
        // 1. Clean up the current scene if one exists
        _currentScene?.Unload();

        // 2. Switch to the new scene
        _currentScene = newScene;

        // 3. Initialize the new scene, injecting the engine (Assets/Settings)
        _currentScene?.Init(_engine);
    }

    public void Update(float deltaTime)
    {
        _currentScene?.Update(deltaTime);
    }

    public void Draw()
    {
        _currentScene?.Draw();
    }

    public void UnloadCurrent()
    {
        _currentScene?.Unload();
        _currentScene = null;
    }
}

