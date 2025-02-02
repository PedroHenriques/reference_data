using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost(
  "/",
  async ([FromBody] dynamic body) =>
  {
    Console.WriteLine(body);
    return Results.Ok();
  }
);

app.Run();
