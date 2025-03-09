# BlackBytesBox.Routed.RequestFilters

A collection of configurable middleware filters for ASP.NET Core applications. This library provides a set of reusable request filters that can be easily integrated into your web application's middleware pipeline.

## Overview

BlackBytesBox.Routed.RequestFilters contains various middleware filters that can be configured to handle HTTP requests in your ASP.NET Core applications. These filters can be used to implement common functionality such as request validation, transformation, and routing customization.

## Available Filters

- **AcceptLanguageFilteringMiddleware**: Filters requests based on Accept-Language header
- **RequestUrlFilteringMiddleware**: Filters requests based on URL patterns
- **HttpProtocolFilteringMiddleware**: Filters requests based on HTTP protocol version
- **SegmentFilteringMiddleware**: Filters requests based on URL segments
- **UserAgentFilteringMiddleware**: Filters requests based on User-Agent header
- **RemoteIPFilteringMiddleware**: Filters requests based on remote IP address
- **HostNameFilteringMiddleware**: Filters requests based on host name
- **DnsHostNameFilteringMiddleware**: Filters requests based on DNS hostname
- **HeaderValuesRequiredFilteringMiddleware**: Validates required HTTP header values
- **HeaderPresentsFilteringMiddleware**: Validates presence of required headers
- **FailurePointsFilteringMiddleware**: Manages request failure tracking

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

## Common Options

Most middleware filters support the following configuration options:

- `Whitelist`: Array of allowed patterns
- `Blacklist`: Array of blocked patterns
- `DisallowedStatusCode`: HTTP status code returned for blocked requests
- `DisallowedFailureRating`: Rating assigned to failed requests for tracking
- `ContinueOnDisallowed`: Whether to continue processing after a block

## License

This project is licensed under the terms specified in the repository.
