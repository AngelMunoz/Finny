## Dev Proxy

Perla offers a simple reverse proxy implementation, to add a dev proxy, add the `devServer.proxy` node within `perla.json`, Perla will automatically register any mappings you have put in.

The mapings you can use are very simple at the moment they are just a `origin -> target` kind of mapping.

```json
{
  "devServer": {
    "proxy": {
      // matches a literal endpoint: https://my-api.com/api/configuration
      "/api/configuration": "https://my-api.com",
      // matches anything that comes after /api/: http://localhost:5000/v1/api/products
      "/api/{**catch-all}": "http://localhost:5000/v1"
    }
  }
}
```
