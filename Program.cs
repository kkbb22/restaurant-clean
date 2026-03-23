using Microsoft.EntityFrameworkCore;
using Restaurant.Data;
using Restaurant.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. إعداد الـ Controllers ومنع حلقات التكرار في JSON
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ReservationService>();

// 2. إعداد الـ CORS (للسماح بالاتصال من أي مكان)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// 3. إعداد قاعدة البيانات (التحويل الذكي لرابط Railway)
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(dbUrl))
    {
        // إعدادات PostgreSQL لـ Railway
        var uri = new Uri(dbUrl);
        var userInfo = uri.UserInfo.Split(':');
        var pgConn = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        
        options.UseNpgsql(pgConn);
    }
    else
    {
        // إعدادات PostgreSQL للمحلي (تأكد من وجود السلسلة في appsettings.json)
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// 4. ضبط المنفذ الخاص بـ Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// 5. تنفيذ الميغريشن تلقائياً عند التشغيل (لبناء الجداول فوراً)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try {
        var context = services.GetRequiredService<AppDbContext>();
        Console.WriteLine("⏳ Running Migrations...");
        context.Database.Migrate(); 
        Console.WriteLine("✅ Database Migration Executed Successfully!");
    } catch (Exception ex) {
        Console.WriteLine($"❌ Migration Error: {ex.Message}");
    }
}

// 6. Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Restaurant API V1");
    c.RoutePrefix = "swagger"; 
});

app.UseCors("AllowAll");
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();