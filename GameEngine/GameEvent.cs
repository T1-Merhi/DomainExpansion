public enum GameEvent
{
    PlayRequested,
    MainMenuRequested,
    PlayerDied,
    RestartRequested,
    QuitRequested,

    // Admin mode only. SceneManager refuses to route these in player mode, so
    // an accidental raise cannot expose the tuning tools in a shipped run.
    AdminRequested,
    TestArenaRequested,
}
