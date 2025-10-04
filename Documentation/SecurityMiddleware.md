# SecurityMiddleware

ASP.NET Core middleware applying baseline security headers and (optionally) access validation / anti-abuse checks.

---
## Responsibilities
- Add standard headers: `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy` (configurable)
- Remove/server header masking to reduce fingerprinting
- Optional request validation (API key / signature / rate-limit) if implemented
- Delegate to next middleware upon success

---
## Basic Usage
```csharp
app.UseMiddleware<SecurityMiddleware>();
```

---
## Pseudo Flow
```csharp
public async Task InvokeAsync(HttpContext ctx)
{
    ApplyHeaders(ctx.Response.Headers);
    // Optional validation: if(!Validate(ctx)) { ctx.Response.StatusCode = 403; return; }
    await _next(ctx);
}
```

---
## Configuration Points
| Aspect | Setting |
|--------|---------|
| HSTS | `EnableHsts`, `MaxAge`, `IncludeSubDomains`, `Preload` |
| CSP | Policy string or builder delegate |
| Frame Options | DENY / SAMEORIGIN |
| Permissions Policy | Custom directives |

---
## Guidance
- Tune CSP progressively (start with `Report-Only`).
- Ensure HSTS only enabled after confirming site always served via HTTPS.

---
## Extension Ideas
| Feature | Idea |
|---------|------|
| Rate limiting | Integrate token bucket check |
| Request integrity | Add HMAC signature verification |
| Security headers report | Endpoint to list active policies |

---
## Limitations
- Does not replace WAF / IDS.
- Complex CSP building may require dedicated builder component.
