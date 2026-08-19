public class SceneManager
{
    private IScene? _currentScene;
    private GameEvent? _pendingEvent;
    private readonly AssetManager _assets;
    private readonly GameSettings _settings;

    // Not a scene transition, so it's surfaced separately for the engine to act on.
    public event Action? QuitRequested;

    public SceneManager(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;
    }

    public void ChangeScene(IScene newScene)
    {
        // Drop anything still queued from the outgoing scene so it can't drive the next transition.
        _pendingEvent = null;

        // 1. Clean up the current scene if one exists
        if (_currentScene is not null)
        {
            _currentScene.EventRaised -= OnSceneEvent;
            _currentScene.Unload();
        }

        // 2. Switch to the new scene
        _currentScene = newScene;
        _currentScene.EventRaised += OnSceneEvent;

        // 3. Initialize the new scene with only what it needs
        _currentScene.Init(_assets, _settings);
    }

    // The scene reports what happened; the SceneManager decides what happens next.
    // Queued rather than handled inline: the raising scene is still executing inside
    // its own Update(), so unloading it here would pull the rug out from under it.
    private void OnSceneEvent(GameEvent gameEvent)
    {
        // First event of the frame wins; later ones are ignored.
        _pendingEvent ??= gameEvent;
    }

    private void ApplyEvent(GameEvent gameEvent)
    {
        if (gameEvent == GameEvent.QuitRequested)
        {
            QuitRequested?.Invoke();
            return;
        }

        IScene nextScene = gameEvent switch
        {
            GameEvent.PlayRequested => new Level1Scene(),
            GameEvent.MainMenuRequested => new MainMenuScene(),
            GameEvent.Level1Completed => new Level2Scene(),
            GameEvent.Level2Completed => new MainMenuScene(),
            GameEvent.PlayerDied => new Level1Scene(),
            _ => throw new ArgumentOutOfRangeException(nameof(gameEvent), gameEvent, null),
        };

        ChangeScene(nextScene);
    }

    public void Update(float deltaTime)
    {
        _currentScene?.Update(deltaTime);

        // Safe to swap scenes now that the current scene's Update has returned.
        if (_pendingEvent is GameEvent pending)
        {
            _pendingEvent = null;
            ApplyEvent(pending);
        }
    }

    public void Draw()
    {
        _currentScene?.Draw();
    }

    public void UnloadCurrent()
    {
        if (_currentScene is not null)
        {
            _currentScene.EventRaised -= OnSceneEvent;
            _currentScene.Unload();
            _currentScene = null;
        }
    }
}

