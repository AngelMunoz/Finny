## Dev Proxy

Perla offers a simple proxy implementation if you place a `proxy-config.json` next to `perla.json`, Perla will automatically register any mappings you have done to it.

The mapings you can use are very simple at the moment they are just a `origin -> target` kind of mapping.

```json
{
  // matches a literal endpoint
  "/api/configuration": "https://my-api.com/configuration"
  // matches anything that comes after /v1/api/
  "/v1/api/{**catch-all}": "http://localhost:5000/api",
  // matches anything that comes after /vnext/api/
  "/vnext/api/{**catch-all}": "https://dev.my-api.com:7000/api/vnext"
}
```
