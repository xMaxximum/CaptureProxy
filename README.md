# CaptureProxy

## Table of Contents

-   [Overview](#overview)
-   [Features](#features)
-   [Prerequisites](#prerequisites)
-   [Installation](#installation)
-   [Getting Started](#getting-started)
    -   [Setting Up the Proxy Server](#setting-up-the-proxy-server)
    -   [Event Handling: BeforeTunnelEstablish](#event-handling-beforetunnelestablish)
    -   [Event Handling: BeforeRequest](#event-handling-beforerequest)
    -   [Event Handling: BeforeHeaderResponse](#event-handling-beforeheaderresponse)
    -   [Event Handling: BeforeBodyResponse](#event-handling-beforebodyresponse)
-   [Contribution](#contribution)
-   [Bug Report](#bug-report)
-   [License](#license)

## Overview

CaptureProxy is a C# library designed to support the creation of a high-performance proxy server, utilizing .NET 8 and relying solely on .NET's built-in libraries.

## Features

-   **Easy HTTP Proxy Setup**:
    Quickly set up and start an HTTP proxy server with just a few lines of code.

-   **Request and Response Interception**:
    Intercept both requests and responses, allowing deep inspection and modification of headers and body content.

-   **Event-Driven Architecture**:

    -   `BeforeTunnelEstablish`: Control how tunnels are established, redirect requests, or forward traffic to another proxy.
    -   `BeforeRequest`: Capture and modify request headers and body before forwarding.
    -   `BeforeHeaderResponse`: Inspect and modify response headers before it is processed.
    -   `BeforeBodyResponse`: Inspect and modify the response body before it is processed.

-   **Custom Responses**:
    Create and return custom responses without forwarding the request to the target server.

-   **Packet Capture Control**:
    Enable or disable packet capture dynamically for advanced use cases like header manipulation and body inspection.

-   **Upstream Proxy Support**:
    Forward requests to an upstream HTTP proxy with optional authentication.

-   **Modify Traffic Flow**:
    Redirect or abort requests, modify headers, or inject custom content into traffic.

-   **Flexible Usage**:
    Integrate seamlessly into .NET projects targeting .NET 8.0 or later.

## Installation

To install the CaptureProxy library, you can use NuGet:

```bash
dotnet add package CaptureProxy
```

Alternatively, you can add the library via your IDE’s package manager or download it directly from the [NuGet Gallery](https://www.nuget.org/packages/CaptureProxy/).

## Getting Started

### Setting Up the Proxy Server

The main component in the library is `HttpProxy`, which allows you to setup HTTP proxy on a specified port.

Here’s a quick example of how to initialize and start an HTTP Proxy on port `8877`.

```C#
using CaptureProxy;

// Initialize the proxy server on port 8877
var httpProxy = new HttpProxy(8877);

// Start the proxy server
httpProxy.Start();

// Do something while the proxy is running...

// Stop the proxy server
httpProxy.Stop();

// Dispose of the proxy resources when done
httpProxy.Dispose();
```

### Event Handling: `BeforeTunnelEstablish`

CaptureProxy provides an event, `BeforeTunnelEstablish`, that allows you to customize and control how the proxy handles establishing tunnels for traffic. This event is especially useful if you want to:

-   Enable capture flag to inspect or modify request/response on `BeforeRequest`, `BeforeHeaderResponse` and `BeforeBodyResponse`events.
-   Set a custom target host and port.
-   Abort the connection based on certain conditions.
-   Forward traffic to an upstream proxy.

```C#
using CaptureProxy;
using CaptureProxy.MyEventArgs;

// Subscribe to the BeforeTunnelEstablish event
httpProxy.Events.BeforeTunnelEstablish += Events_BeforeTunnelEstablish;

// Event handler for BeforeTunnelEstablish
private void Events_BeforeTunnelEstablish(object? sender, BeforeTunnelEstablishEventArgs e)
{
    // Enable packet capture to allow modifying request and response
    e.PacketCapture = true;

    // Optionally, set a custom host and port for where the request should be forwarded
    e.Host = "localhost";
    e.Port = 80;

    // Abort the connection immediately if certain conditions are met
    e.Abort = true;

    // If you need to forward the request to an upstream HTTP proxy
    e.UpstreamProxy = new UpstreamHttpProxy
    {
        Host = "proxy.example.com",
        Port = 3128,
        User = "user", // Optional username for authentication
        Pass = "pass", // Optional password for authentication
    };
}
```

### Event Handling: `BeforeRequest`

The `BeforeRequest` event allows you to intercept requests, examine or modify headers and body content, and even return a custom response without sending the request to the target server.

```C#
using CaptureProxy;
using CaptureProxy.MyEventArgs;
using CaptureProxy.HttpIO;
using System.Net;

// Subscribe to the BeforeTunnelEstablish event
httpProxy.Events.BeforeTunnelEstablish += Events_BeforeTunnelEstablish;

// Subscribe to the BeforeRequest event
httpProxy.Events.BeforeRequest += Events_BeforeRequest;

// Event handler for BeforeTunnelEstablish
private void Events_BeforeTunnelEstablish(object? sender, BeforeTunnelEstablishEventArgs e)
{
    // Enable packet capture to allow modifying request and response
    // Without this, the BeforeRequest event will not trigger
    e.PacketCapture = true;
}

// Event handler for BeforeRequest
private void Events_BeforeRequest(object? sender, BeforeRequestEventArgs e)
{
    // Get header value by name, returns string or null
    var cookie = e.Request.Headers.GetFirstValue("Cookie");

    // Get header value by name, returns List<string>
    var cookies = e.Request.Headers.GetValues("Cookie");

    // Add multiple headers to the request
    e.Request.Headers.Add("X-Custom-Header", "Custom Header Value");

    // Add or replace a header in the request
    e.Request.Headers.AddOrReplace("X-Custom-Header", "Custom Header Value");

    // Get the request body as a string (UTF-8 encoding)
    var body = Encoding.UTF8.GetString(e.Request.Body);

    // Return a custom response without forwarding the request to the server
    e.Response = new HttpResponse(httpProxy);
    e.Response.StatusCode = HttpStatusCode.OK;
    e.Response.Headers.AddOrReplace("X-Custom-Header", "Custom Header Value");
    e.Response.SetBody("<h1>Hello World!</h1>");
}
```

### Event Handling: `BeforeHeaderResponse`

The `BeforeHeaderResponse` event allows you to intercept and modify the response headers before it has been sent to client. This event is useful when you need to modify headers, add custom headers, or inspect response headers base on request information. However, **do not modify** `e.Response.Body` at this stage, as it may cause unintended effects.

```C#
using CaptureProxy;
using CaptureProxy.MyEventArgs;
using CaptureProxy.HttpIO;
using System.Net;

// Subscribe to the BeforeTunnelEstablish event
httpProxy.Events.BeforeTunnelEstablish += Events_BeforeTunnelEstablish;

// Subscribe to the BeforeHeaderResponse event
httpProxy.Events.BeforeHeaderResponse += Events_BeforeHeaderResponse;

// Event handler for BeforeTunnelEstablish
private void Events_BeforeTunnelEstablish(object? sender, BeforeTunnelEstablishEventArgs e)
{
    // Enable packet capture to allow modifying request and response
    // Without this, the BeforeHeaderResponse event will not trigger
    e.PacketCapture = true;
}

// Event handler for BeforeHeaderResponse
private void Events_BeforeHeaderResponse(object? sender, BeforeHeaderResponseEventArgs e)
{
    // Do not modify the response body in this event.
    // e.Response.Body will always return null here as the body has not been processed yet.

    // Access request information, similar to the BeforeRequest event
    var request = e.Request;

    // Get response header value by name, returns string or null
    var cookie = e.Response.Headers.GetFirstValue("Set-Cookie");

    // Get response header values by name, returns List<string>
    var cookies = e.Response.Headers.GetValues("Set-Cookie");

    // Add multiple headers to the response
    e.Response.Headers.Add("X-Custom-Header", "Custom Header Value");

    // Add or replace a header in the response
    e.Response.Headers.AddOrReplace("X-Custom-Header", "Custom Header Value");

    // Replace a status code in the response
    e.Response.StatusCode = HttpStatusCode.Forbidden;

    // Set this flag to true if you want to view or modify the response body
    // Without this, the BeforeBodyResponse event will not trigger
    e.CaptureBody = true;
}
```

### Event Handling: `BeforeBodyResponse`

The `BeforeBodyResponse` event allows you to capture and modify the response body. This event is triggered before the body has been sent to client, and it's where you can modify the response content safely. However, **do not modify** `e.Response.Headers` at this stage, as it may cause unintended effects.

```C#
using CaptureProxy;
using CaptureProxy.MyEventArgs;
using CaptureProxy.HttpIO;
using System.Net;

// Subscribe to the BeforeTunnelEstablish event
httpProxy.Events.BeforeTunnelEstablish += Events_BeforeTunnelEstablish;

// Subscribe to the BeforeHeaderResponse event
httpProxy.Events.BeforeHeaderResponse += Events_BeforeHeaderResponse;

// Subscribe to the BeforeBodyResponse event
httpProxy.Events.BeforeBodyResponse += Events_BeforeBodyResponse;

// Event handler for BeforeTunnelEstablish
private void Events_BeforeTunnelEstablish(object? sender, BeforeTunnelEstablishEventArgs e)
{
    // Enable packet capture to allow modifying request and response
    // Without this, the BeforeHeaderResponse event will not trigger
    e.PacketCapture = true;
}

// Event handler for BeforeHeaderResponse
private void Events_BeforeHeaderResponse(object? sender, BeforeHeaderResponseEventArgs e)
{
    // Set this flag to true if you want to view or modify the response body
    // Without this, the BeforeBodyResponse event will not trigger
    e.CaptureBody = true;

    // Replace a status code in the response
    e.Response.StatusCode = HttpStatusCode.Forbidden;
}

// Event handler for BeforeBodyResponse
private void Events_BeforeBodyResponse(object? sender, BeforeBodyResponseEventArgs e)
{
    // Do not modify e.Response.Headers in this event,
    // as it could cause unintended side effects

    // Get the response body as a string (UTF-8 encoding)
    var body = Encoding.UTF8.GetString(e.Response.Body);

    // Modify the response body
    e.Response.SetBody("<h1>Forbidden by CaptureProxy!</h1>");
}
```

## Contribution

I warmly welcome contributions from the community. Feel free to create issues and pull requests; I will review them as soon as possible.

## Bug Report

If you discover any bugs, please report them on the [issue tracker](https://github.com/mrcyclo/CaptureProxy/issues).

## License

This project is licensed under the [MIT](LICENSE.TXT) license.
