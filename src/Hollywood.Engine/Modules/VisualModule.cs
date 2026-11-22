using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public interface IVisualModule : IDisposable
{
    string Title { get; }
    void Update(TimeSpan elapsed, DeterministicRandom random);
    void Render(Canvas canvas, Rect bounds, TimeSpan elapsed);
}
