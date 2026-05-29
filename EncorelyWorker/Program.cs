using EncorelyWorker;
using EncorelyApplication.Interfaces;
using EncorelyApplication.Services;
using EncorelyQuery;
using EncorelyQuery.Interfaces;
using EncorelyQuery.Implementations;
using EncorelyRepository;
using EncorelyRepository.Interfaces;
using EncorelyRepository.Implements;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Carga el .env (si existe) para que DB_HOST, DB_PASSWORD, KAFKA_HOST/PORT, etc.
// estén disponibles sin tener que exportarlos a mano en desarrollo.
DotNetEnv.Env.TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);

// Configuration source: las env vars ya están en el proceso (DotNetEnv las puso ahí).
builder.Configuration.AddEnvironmentVariables();

// Dapper / Postgres connection factories (mismo wiring que el API).
builder.Services.AddSingleton<EncorelyQuery.IDbConnectionFactory, EncorelyQuery.DbConnectionFactory>();
builder.Services.AddSingleton<EncorelyRepository.IDbConnectionFactory, EncorelyRepository.DbConnectionFactory>();

// Queries (lectura)
builder.Services.AddScoped<IUsuarioQueries, UsuarioQueries>();
builder.Services.AddScoped<IMatchQueries, MatchQueries>();
builder.Services.AddScoped<ISwipeQueries, SwipeQueries>();
builder.Services.AddScoped<IMusicalProfileQueries, MusicalProfileQueries>();

// Repositorios (escritura)
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<ISwipeRepository, SwipeRepository>();
builder.Services.AddScoped<IMusicalProfileRepository, MusicalProfileRepository>();

// Servicio de compatibilidad (cálculo de afinidad).
builder.Services.AddScoped<ICompatibilityService, CompatibilityService>();

// Hosted service: el consumidor de Kafka.
builder.Services.AddHostedService<KafkaConsumerWorker>();

var host = builder.Build();
host.Run();
