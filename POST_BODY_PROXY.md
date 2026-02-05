# POST Body Proxy Issue

## Summary

POST requests (or any request with a body) sent to virtual model runners are not proxied to the backend model endpoint. The request hangs indefinitely and eventually times out. GET requests (no body) work correctly.

## Root Cause

The issue originates in **WatsonWebserver**'s `HttpRequest.DataAsBytes` property (in the HttpListener-based implementation at `WatsonWebserver/HttpRequest.cs`):

```csharp
public override byte[] DataAsBytes
{
    get
    {
        if (_DataAsBytes != null) return _DataAsBytes;
        if (Data != null && ContentLength > 0)
        {
            _DataAsBytes = ReadStreamFully(Data);
            return _DataAsBytes;
        }
        return null;
    }
}
```

The `ContentLength > 0` gate causes the property to return `null` without reading the stream when:

- `ContentLength` is `-1` (i.e. `HttpListenerRequest.ContentLength64` returns `-1` when the `Content-Length` header is absent, such as with chunked transfer encoding)
- `ContentLength` is `0` (edge cases)

Additionally, `ReadStreamFully(Stream input)` reads the `HttpListenerRequest.InputStream` **synchronously**, which can block the calling thread when invoked from an async context.

The same issue exists in `DataAsString`, which shares the `_DataAsBytes` backing field and the same `ContentLength > 0` check.

## How It Manifested in Conductor

Conductor's `ProxyController` was changed from reading the request body via a direct async stream read:

```csharp
// OLD (worked) - read the raw Data stream with async CopyToAsync, no ContentLength check
byte[] requestBody = null;
if (ctx.Request.Data != null)
{
    requestBody = await ReadStreamToEndAsync(ctx.Request.Data);
}
```

to using the `DataAsBytes` property:

```csharp
// NEW (broken) - DataAsBytes returns null when ContentLength <= 0
byte[] requestBody = ctx.Request.DataAsBytes;
```

This change was part of a commit that also added response headers (`X-Conductor-Vmr-Id`, `X-Conductor-Endpoint-Id`, `X-Conductor-Model-Name`) and proxy headers (`X-Forwarded-For`, `X-Forwarded-Host`, `X-Forwarded-Proto`).

## Current Workaround in Conductor

A `RequestContext.Data` property and `RequestContext.ReadStreamAsync` static method were added to pre-read the body from the raw `Data` stream (using async `CopyToAsync`) at the DefaultRoute entry point, bypassing `DataAsBytes` entirely. The pre-read bytes are passed through to `ProxyController.HandleRequest` via the `RequestContext`.

## Fix

The fix belongs in **WatsonWebserver** and **SwiftStack**:

- **WatsonWebserver**: `DataAsBytes` (and `DataAsString`) must reliably read the request body regardless of `ContentLength` value. This includes handling chunked transfer encoding and cases where `Content-Length` is absent. The read should also be async-safe.
- **SwiftStack**: Pick up the updated WatsonWebserver dependency.

## Cleanup in Conductor After Library Fix

Once WatsonWebserver and SwiftStack are updated so that `DataAsBytes` works correctly for all request types, the following can be removed or simplified in Conductor:

1. **`RequestContext.ReadStreamAsync(Stream)`** - Remove this static method; it exists solely to work around the `DataAsBytes` bug.
2. **`System.IO` and `System.Threading.Tasks` imports in `RequestContext.cs`** - Remove; only needed for `ReadStreamAsync`.
3. **DefaultRoute body pre-reading in `ConductorServer.cs`** - Simplify to use `ctx.Request.DataAsBytes` directly instead of manually reading the stream:
   ```csharp
   // After fix, simplify to:
   requestContext.Data = ctx.Request.DataAsBytes;
   ```
4. **`RequestContext.Data` property** - Keep. Pre-reading the body at the edge into `RequestContext.Data` and passing it downstream is the right pattern regardless; it just won't need a manual stream read anymore.
