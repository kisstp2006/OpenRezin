using ECS.World;
using Foundation.Types;
using TinyTui;

var world = new World();
var results = new List<(string Name, bool Passed)>();

void Check(string name, Func<bool> test)
{
    bool passed;
    try { passed = test(); }
    catch { passed = false; }
    results.Add((name, passed));
}

Id entity = default;

Check("CreateEntity", () =>
{
    entity = world.CreateEntity();
    return entity.IsValid;
});

Check("AddComponent", () =>
{
    world.AddComponent(entity, 42);
    return true;
});

Check("Has", () => world.Has<int>(entity));

Check("GetComponent returns 42", () =>
{
    var value = world.GetComponent<int>(entity);
    return value != null && value.Value == 42;
});

Check("RemoveComponent", () =>
{
    world.RemoveComponent<int>(entity);
    return !world.Has<int>(entity);
});

using var app = new TuiApplication { ExitOnEscape = true };
var window = new TuiWindow("Rezin Test Runner")
{
    Frame = new TuiRect(0, 0, 50, results.Count + 4),
    Style = new TuiStyle(TuiColor.White, TuiColor.Black),
    BorderStyle = new TuiStyle(TuiColor.White, TuiColor.Black)
};
var list = window.Add(new TuiListView());
list.Style = new TuiStyle(TuiColor.White, TuiColor.Black);
list.SetItems(results.Select(r => $"{(r.Passed ? "PASS" : "FAIL")}  {r.Name}"));
app.Run(window);