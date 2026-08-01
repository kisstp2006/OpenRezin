using ECS.World;
using Foundation.Types;
using Foundation.Containers;
using Foundation.Math;
using System.Numerics;
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

Check("QuadTree Insert/Query", () =>
{
    var root = new QuadTreeNode<int>(new AABB(new Vector3(-100, -100, -100), new Vector3(100, 100, 100)));

    root.Insert(1, new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 1)));
    root.Insert(2, new AABB(new Vector3(50, 0, 50), new Vector3(51, 1, 51)));
    root.Insert(3, new AABB(new Vector3(-50, 0, -50), new Vector3(-49, 1, -49)));

    var queryResults = new List<int>();
    root.Query(new AABB(new Vector3(-2, -2, -2), new Vector3(2, 2, 2)), queryResults);

    return queryResults.Count == 1 && queryResults[0] == 1;
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