using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
var app = builder.Build();

app.MapGet("/", async (HttpRequest request) => 
{
    var ipInfo = new IpInfo();
    string hostName = Dns.GetHostName(); // Retrive the Name of HOST
    Console.WriteLine(hostName);
    // Get the private IP
    var allIps = Dns.GetHostEntry(hostName).AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork);
    var myIP = allIps.FirstOrDefault();

    // check some header
    string? ip = request.HttpContext.GetServerVariable("HTTP_X_FORWARDED_FOR");  
    if (string.IsNullOrEmpty(ip))  
    {     
        ip = request.HttpContext.GetServerVariable("REMOTE_ADDR");  
    }

    //make another request
    using var client = new HttpClient();
    var result = await client.GetStringAsync("https://icanhazip.com/");

    ipInfo.PrivateIp = myIP?.ToString();
    ipInfo.HostName = hostName;
    ipInfo.ICanHazIp = result.Replace("\n","");
    ipInfo.RemoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();
    ipInfo.LocalIp = ip;
    ipInfo.HeadersCommaDelimited = string.Join("||",request.Headers.Select(x => $"{x.Key}:{x.Value}"));

    return ipInfo;
});

app.MapGet("/todoitems", async (TodoDb db) =>
    await db.Todos.ToListAsync());

app.MapGet("/todoitems/complete", async (TodoDb db) =>
    await db.Todos.Where(t => t.IsComplete).ToListAsync());

app.MapGet("/todoitems/{id}", async (int id, TodoDb db) =>
    await db.Todos.FindAsync(id)
        is Todo todo
            ? Results.Ok(todo)
            : Results.NotFound());

app.MapPost("/todoitems", async (Todo todo, TodoDb db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todoitems/{todo.Id}", todo);
});

app.MapPut("/todoitems/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.Name = inputTodo.Name;
    todo.IsComplete = inputTodo.IsComplete;

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapDelete("/todoitems/{id}", async (int id, TodoDb db) =>
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Results.Ok(todo);
    }

    return Results.NotFound();
});

app.Run();

class Todo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsComplete { get; set; }
}

class IpInfo
{
    public string? PrivateIp { get; set; }
    public string? HostName { get; set; }
    public string? ICanHazIp { get; set; }
    public string? RemoteIp { get; set; }
    public string? LocalIp { get; set; }
    public string? HeadersCommaDelimited { get; set; }

}

class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}