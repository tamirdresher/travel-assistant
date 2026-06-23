var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos").RunAsEmulator();
var conversations = cosmos.AddCosmosDatabase("conversations");

var postgres = builder.AddPostgres("postgres");
var bookings = postgres.AddDatabase("bookings");

builder.AddProject<Projects.TravelAssistant_Api>("api")
    .WithReference(conversations).WaitFor(conversations)
    .WithReference(bookings).WaitFor(bookings);

builder.Build().Run();
