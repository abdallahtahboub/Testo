using Autestiq.InvoiceDesk;

// Runs the desk by hand: `dotnet run --project src/Autestiq.InvoiceDesk`. The benchmark starts its own
// copy on a port of its own, through InvoiceDeskRoutes.Map.
Defects.ReadEnvironment();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

InvoiceDeskRoutes.Map(app);

app.Run();
