public interface IScene
{
    void Init(GameEngine engine);
    void Update(float deltaTime);
    void Draw();
    void Unload(); // Called automatically when switching away from this scene
}

