# BlackBytesBox.Routed.RequestFilters

A collection of configurable middleware filters for ASP.NET Core applications. This library provides a set of reusable request filters that can be easily integrated into your web application's middleware pipeline.

## Available Filters

- **RemoteIPFilteringMiddleware**: Filters requests based on remote IP address. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **AcceptLanguageFilteringMiddleware**: Filters requests based on Accept-Language header. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **RequestUrlFilteringMiddleware**: Filters requests based on URL patterns. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **HttpProtocolFilteringMiddleware**: Filters requests based on HTTP protocol version. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **SegmentFilteringMiddleware**: Filters requests based on URL segments. Blacklist takes precedence - if any segment is blacklisted, request is blocked regardless of whitelist.
- **UserAgentFilteringMiddleware**: Filters requests based on User-Agent header. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **HostNameFilteringMiddleware**: Filters requests based on host name. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **DnsHostNameFilteringMiddleware**: Filters requests based on DNS hostname. Whitelist takes precedence - if matched, request is allowed; otherwise, checks blacklist.
- **HeaderValuesRequiredFilteringMiddleware**: Validates required HTTP header values. Uses only allowed values list for validation.
- **HeaderPresentsFilteringMiddleware**: Validates presence of required headers. Blacklist takes precedence - if any header is blacklisted, request is blocked.
- **PathDeepFilteringMiddleware**: Limits URL path depth. Uses only depth limit configuration.
- **FailurePointsFilteringMiddleware**: Manages request failure tracking. Uses accumulated points against configured limit.

## Features

- Configurable middleware filters
- Easy integration with ASP.NET Core applications
- Flexible request handling and routing options
- Extensible architecture for custom filter implementation

## Installation

You can install the package via NuGet:

```shell
dotnet add package BlackBytesBox.Routed.RequestFilters
```

## Usage

### Basic Setup

Add the desired filters to your application's middleware pipeline in the `Program.cs` or `Startup.cs` file:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddAcceptLanguageFilteringMiddleware();
builder.Services.AddRequestUrlFilteringMiddleware();
// Add other middleware services as needed

var app = builder.Build();

// Configure middleware
app.UseAcceptLanguageFilteringMiddleware();
app.UseRequestUrlFilteringMiddleware();
// Use other middleware as needed
```

### Configuration Examples

#### HostName Filter

```json
  "HostNameFilteringMiddlewareOptions": {
    "FilterPriority": "Whitelist",
    "Whitelist": [ "localhost", "*.localhost", "h123456.server.net", "*.h123456.server.net", "domain.com", "*.domain.com" ],
    "Blacklist": [ "*" ],
    "CaseSensitive": true,
    "BlacklistStatusCode": 403,
    "BlacklistFailureRating": 1,
    "BlacklistContinue": true,
    "NotMatchedStatusCode": 403,
    "NotMatchedFailureRating": 0,
    "NotMatchedContinue": true,
    "NotMatchedLogWarning": true
  },
```

#### Segment Filter

```json
  "SegmentFilteringMiddlewareOptions": {
    "FilterPriority": "Blacklist",
    "Whitelist": [ "*" ],
    "Blacklist": [ ".git", "cgi-bin", "cgi", "plugins", "fckeditor", "autodiscover", ".env", ".well-known", "HNAP1", "phpmyadmin", "phpunit", "windows", "..." ],
    "CaseSensitive": true,
    "BlacklistStatusCode": 403,
    "BlacklistFailureRating": 1,
    "BlacklistContinue": true,
    "NotMatchedStatusCode": 403,
    "NotMatchedFailureRating": 0,
    "NotMatchedContinue": true,
    "NotMatchedLogWarning": true,
    "UnreadableStatusCode": 403,
    "UnreadableFailureRating": 1,
    "UnreadableContinue": true
  },
```

#### Accept Language Filter

```json
{
  "AcceptLanguageFilteringMiddlewareOptions": {
    "Whitelist": ["en-US", "en-GB", "de-DE", "fr-FR"],
    "Blacklist": ["zh-CN", "ko-KR"],
    "DisallowedStatusCode": 403,
    "DisallowedFailureRating": 5,
    "ContinueOnDisallowed": false
  }
}
```

#### URL Filter

```csharp
services.AddRequestUrlFilteringMiddleware(options =>
{
    options.Whitelist = new[] { "/api/*", "/home/*" };
    options.Blacklist = new[] { "*.php*", "*sitemap.xml*", "*robots.txt*" };
    options.DisallowedStatusCode = 400;
    options.DisallowedFailureRating = 10;
    options.ContinueOnDisallowed = false;
});
```

#### HTTP Protocol Filter

```json
{
  "HttpProtocolFilteringMiddlewareOptions": {
    "Whitelist": [
      "HTTP/2",
      "HTTP/2.0",
      "HTTP/3",
      "HTTP/3.0"
    ],
    "Blacklist": [
      "HTTP/1.0",
      "HTTP/1.?"
    ],
    "DisallowedStatusCode": 426,
    "DisallowedFailureRating": 5,
    "ContinueOnDisallowed": false
  }
}
```

#### Segment Filter

```csharp
services.AddSegmentFilteringMiddleware(options =>
{
    options.Whitelist = new[] { "*" };
    options.Blacklist = new[] { 
        ".git", 
        "cgi-bin", 
        "plugins", 
        "phpmyadmin",
        ".env",
        ".well-known"
    };
    options.DisallowedStatusCode = 400;
    options.DisallowedFailureRating = 10;
});
```

## Failure Points System

The library includes a sophisticated failure tracking system that monitors and accumulates failure points for requests based on their IP addresses. This system persists failure data between application restarts and can be used to implement rate limiting and request blocking based on accumulated failures.

### Configuration

Configure the FailurePoints system in your `appsettings.json`:

```json
{
  "FailurePointsFilteringMiddlewareOptions": {
    "DumpFilePath": "failurepoints.json",
    "FailurePointsLimit": 50,
    "DisallowedStatusCode": 403,
    "ContinueOnDisallowed": false
  }
}
```

### How It Works

The system tracks failure points per IP address across all middleware:
- Each middleware can assign failure points for rule violations
- Points are persisted to disk and survive application restarts
- Requests are blocked when total points exceed the configured limit

### Implementation Strategies

The FailurePoints middleware can be positioned either early or late in the pipeline, enabling different security strategies:

#### Early Pipeline Position (Preemptive Blocking)

Block suspicious IPs immediately before they reach other middleware:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services with configuration from appsettings.json
builder.Services.AddFailurePointsFilteringMiddleware(builder.Configuration);
builder.Services.AddAcceptLanguageFilteringMiddleware(builder.Configuration);
builder.Services.AddRequestUrlFilteringMiddleware(builder.Configuration);

var app = builder.Build();

// Block suspicious IPs immediately
app.UseFailurePointsFilteringMiddleware();

// Other middleware only processes non-blocked requests
app.UseAcceptLanguageFilteringMiddleware();
app.UseRequestUrlFilteringMiddleware();

app.Run();
```

#### Late Pipeline Position (Accumulative Blocking)

Monitor and accumulate failures across middleware before blocking:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure services to continue on failure and accumulate points
builder.Services.AddAcceptLanguageFilteringMiddleware(options => 
{
    options.ContinueOnDisallowed = true;
    options.DisallowedFailureRating = 5;
});

builder.Services.AddRequestUrlFilteringMiddleware(options => 
{
    options.ContinueOnDisallowed = true;
    options.DisallowedFailureRating = 10;
});

// Configure FailurePoints with custom settings
builder.Services.AddFailurePointsFilteringMiddleware(options =>
{
    options.DumpFilePath = Path.Combine(AppContext.BaseDirectory, "failurepoints.json");
    options.FailurePointsLimit = 50;
    options.DisallowedStatusCode = StatusCodes.Status403Forbidden;
    options.ContinueOnDisallowed = false;
});

var app = builder.Build();

// Let other middleware accumulate failure points
app.UseAcceptLanguageFilteringMiddleware();
app.UseRequestUrlFilteringMiddleware();

// Block requests that have accumulated too many points
app.UseFailurePointsFilteringMiddleware();

app.Run();
```

#### Mixed Configuration Example

Using both appsettings and code configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add base configuration from appsettings.json
builder.Services.AddFailurePointsFilteringMiddleware(builder.Configuration);

// Add other middleware with mixed configuration
builder.Services.AddAcceptLanguageFilteringMiddleware(builder.Configuration, options => 
{
    // Override or add to appsettings configuration
    options.ContinueOnDisallowed = true;
    options.DisallowedFailureRating = 5;
});

builder.Services.AddRequestUrlFilteringMiddleware(options => 
{
    // Direct code configuration
    options.Whitelist = new[] { "/api/*", "/home/*" };
    options.ContinueOnDisallowed = true;
    options.DisallowedFailureRating = 10;
});

var app = builder.Build();

// Configure middleware pipeline
app.UseAcceptLanguageFilteringMiddleware();
app.UseRequestUrlFilteringMiddleware();
app.UseFailurePointsFilteringMiddleware();

app.Run();
```

This late-pipeline strategy allows:
- Monitoring of multiple violation types before taking action
- Accumulation of failure points across different middleware
- Final decision making based on total accumulated failures
- More detailed failure data collection for analysis

### Persisted Data Format

The failure points data will be automatically saved to the specified JSON file and will look something like this:

```json
{
  "SummaryBySource": {
    "192.168.1.1": [
      {
        "FailurePoint": 5,
        "FailureSource": "AcceptLanguageFilteringMiddleware"
      },
      {
        "FailurePoint": 10,
        "FailureSource": "RequestUrlFilteringMiddleware"
      }
    ]
  },
  "SummaryByIp": {
    "192.168.1.1": {
      "FailurePoint": 15
    }
  }
}
```

## Common Options

Most middleware filters support the following configuration options:

- `Whitelist`: Array of allowed patterns
- `Blacklist`: Array of blocked patterns
- `DisallowedStatusCode`: HTTP status code returned for blocked requests
- `DisallowedFailureRating`: Rating assigned to failed requests for tracking
- `ContinueOnDisallowed`: Whether to continue processing after a block

## License

This project is licensed under the terms specified in the repository.
