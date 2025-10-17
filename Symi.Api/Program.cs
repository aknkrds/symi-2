using System.Text;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Symi.Api.Data;
using Symi.Api.Middleware;
using Symi.Api.Services;
using Symi.Api.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Config
var config = builder.Configuration;

// Serilog (env-controlled via config)
var serilogFormat = config["Logging:Serilog:Format"] ?? (builder.Environment.IsDevelopment() ? "json" : "plain");
var minLevel = (config["Logging:Serilog:MinimumLevel"] ?? (builder.Environment.IsDevelopment() ? "Debug" : "Information")).ToLowerInvariant();
var level = minLevel switch
{
    "debug" => LogEventLevel.Debug,
    "information" => LogEventLevel.Information,
    "warning" => LogEventLevel.Warning,
    "error" => LogEventLevel.Error,
    _ => LogEventLevel.Information
};
var loggerConfig = new LoggerConfiguration().MinimumLevel.Is(level).Enrich.FromLogContext();
if (serilogFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
{
    loggerConfig = loggerConfig.WriteTo.Console(new JsonFormatter());
}
else
{
    loggerConfig = loggerConfig.WriteTo.Console();
}
Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog(Log.Logger);

// DbContext (SQLite/PostgreSQL via reflection)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var defaultConn = config.GetConnectionString("Default") ?? "Data Source=symi.db";
    if (defaultConn.Contains("Host=", StringComparison.OrdinalIgnoreCase) || defaultConn.Contains("Username=", StringComparison.OrdinalIgnoreCase))
    {
        var npgsqlExtType = Type.GetType("Npgsql.EntityFrameworkCore.PostgreSQLDbContextOptionsExtensions, Npgsql.EntityFrameworkCore.PostgreSQL");
        var useNpgsql = npgsqlExtType?.GetMethod("UseNpgsql", new[] { typeof(DbContextOptionsBuilder), typeof(string) });
        if (useNpgsql != null)
        {
            useNpgsql.Invoke(null, new object[] { options, defaultConn });
        }
        else
        {
            Log.Warning("PostgreSQL connection string detected but Npgsql provider not referenced; falling back to SQLite.");
            options.UseSqlite("Data Source=symi.db");
        }
    }
    else
    {
        options.UseSqlite(defaultConn);
    }
});

// Authentication & JWT
var jwtIssuer = config["Jwt:Issuer"] ?? "symi";
var jwtAudience = config["Jwt:Audience"] ?? "symi-client";
var accessSecret = config["Jwt:AccessTokenSecret"] ?? "dev-access-secret-change-me";
var refreshSecret = config["Jwt:RefreshTokenSecret"] ?? "dev-refresh-secret-change-me";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessSecret)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.ValidateAudience = false;
    }
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtAuth");
            var authHeader = ctx.Request.Headers.Authorization.ToString();
            log.LogInformation("Auth header: {Authorization}", string.IsNullOrEmpty(authHeader) ? "<empty>" : authHeader);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtAuth");
            log.LogError(ctx.Exception, "JWT auth failed: {Message}", ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtAuth");
            log.LogInformation("JWT challenge for path {Path}", ctx.Request.Path);
            return Task.CompletedTask;
        }
    };
});

// Authorization (roles)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("User", p => p.RequireRole("user"));
    options.AddPolicy("Organizer", p => p.RequireRole("organizer"));
    options.AddPolicy("Moderator", p => p.RequireRole("moderator"));
    options.AddPolicy("Admin", p => p.RequireRole("admin"));
});
// standardized 403 body will be added via a post-authorization middleware below

// AWS S3 client (skip in Testing to avoid external dependency)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client());
}

// Services
builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<JwtService>();
// Storage service abstraction
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IStorageService, S3StorageService>();
}
else
{
    builder.Services.AddSingleton<IStorageService, FakeStorageService>();
}
// Background workers
builder.Services.AddHostedService<ThumbnailWorker>();
builder.Services.AddHostedService<PayoutWorker>();
// RateLimit store selection (avoid Redis in Testing environment)
// Prefer Redis when configured and reachable; otherwise fall back to in-memory to avoid startup failures.
var redisConn = config.GetConnectionString("Redis") ?? config["Redis:ConnectionString"];
var isTestingEnv = builder.Environment.IsEnvironment("Testing");
if (!isTestingEnv && !string.IsNullOrWhiteSpace(redisConn))
{
    try
    {
        var opts = StackExchange.Redis.ConfigurationOptions.Parse(redisConn);
        opts.AbortOnConnectFail = false;
        var mux = StackExchange.Redis.ConnectionMultiplexer.Connect(opts);
        builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(mux);
        builder.Services.AddScoped<IRateLimitStore, RedisRateLimitStore>();
    }
    catch (Exception ex)
    {
        Serilog.Log.Warning(ex, "Redis connection failed; falling back to InMemoryRateLimitStore.");
        builder.Services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();
    }
}
else
{
    builder.Services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();
}

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Symi API", Version = "v1" });
    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Bearer {token}"
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            securityScheme,
            new List<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks();
// Add memory cache for feed endpoint
builder.Services.AddMemoryCache();

var app = builder.Build();

// DB bootstrap
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Use EnsureCreated in Testing, otherwise prefer Migrate when migrations exist; fallback to EnsureCreated when none.
    if (app.Environment.IsEnvironment("Testing"))
    {
        db.Database.EnsureCreated();
    }
    else
    {
        var hasMigrations = db.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            db.Database.Migrate();
        }
        else
        {
            Serilog.Log.Warning("No EF migrations found; using EnsureCreated to bootstrap schema.");
            db.Database.EnsureCreated();
        }
    }

    // Seed admin user only (avoid Role table dependency)
    var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
    var adminEmail = "admin@local";
    var admin = db.Users.FirstOrDefault(u => u.Email == adminEmail);
    if (admin == null)
    {
        var pwd = "Admin123!";
        var (hash, salt) = hasher.Hash(pwd);
        admin = new User
        {
            Email = adminEmail,
            Username = "admin",
            FullName = "Admin",
            Role = "admin",
            Status = "active",
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordAlgorithm = "PBKDF2",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
        db.SaveChanges();
    }
}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global exception to JSON
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalException");
        logger.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            code = "error",
            message = ex.Message,
            route = context.Request.Path,
            stack = ex.StackTrace
        }));
    }
});

// CorrelationId & route enrichment
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    using (Serilog.Context.LogContext.PushProperty("Route", context.Request.Path))
    {
        await next();
    }
});

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

// Debug: log Authorization header for /me
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/me"))
    {
        var auth = context.Request.Headers["Authorization"].ToString();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AuthHeader");
        logger.LogInformation("/me Authorization header: {Auth}", string.IsNullOrEmpty(auth) ? "<empty>" : auth);
    }
    await next();
});

// Rate limit (skip in Testing to reduce flakiness and external dependencies)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<RateLimitMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();

// Status guard for critical endpoints
app.UseMiddleware<StatusMiddleware>();

// Standardize 403 responses body
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == StatusCodes.Status403Forbidden && !context.Response.HasStarted)
    {
        await context.Response.WriteAsJsonAsync(new { code = "forbidden", message = "Insufficient role or policy" });
    }
});

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = "ok",
            ts = DateTime.UtcNow.ToString("O")
        }));
    }
});

app.Run();
