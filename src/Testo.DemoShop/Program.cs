using Testo.DemoShop;

// A deliberately small web shop for test runs to act on.
//
// It is not a demo of Testo. It is the thing Testo points at, and it exists so that execution can
// be built and measured without aiming a browser at anybody else's website
// (docs/execution-threat-model.md §4.2).
//
// Several of its features are wrong on purpose, one defect at a time, switched through /_defects.
// That switch is the instrument the AI benchmark rests on: a tester that always says "passed"
// scores perfectly against a working application, so the only measurement worth having is whether
// it still says "passed" when something is actually broken (Defects.cs).

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();

Defects.ReadEnvironment();
DemoShopRoutes.Map(app);

app.Run();
