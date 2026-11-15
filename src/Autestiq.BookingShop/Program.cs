using Autestiq.BookingShop;

// Runs the clinic by hand: `dotnet run --project src/Autestiq.BookingShop`. The benchmark starts its
// own copy on a port of its own, through BookingShopRoutes.Map.
Defects.ReadEnvironment();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

BookingShopRoutes.Map(app);

app.Run();
