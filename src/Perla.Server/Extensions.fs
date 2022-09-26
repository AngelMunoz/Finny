[<AutoOpen>]
module Perla.Lib.Extensions

open System.Runtime.CompilerServices
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers

/// Taken from https://github.com/giraffe-fsharp/Giraffe/blob/71ef664f7a6276b1f7cc548189c54dccf633898c/src/Giraffe/HttpContextExtensions.fs#L21
/// Licensed under: https://github.com/giraffe-fsharp/Giraffe/blob/71ef664f7a6276b1f7cc548189c54dccf633898c/LICENSE
[<Extension>]
type HttpContextExtensions() =

  /// <summary>
  /// Gets an instance of `'T` from the request's service container.
  /// </summary>
  /// <returns>Returns an instance of `'T`.</returns>
  [<Extension>]
  static member GetService<'T>(ctx: HttpContext) =
    let t = typeof<'T>

    match ctx.RequestServices.GetService t with
    | null -> raise (exn t.Name)
    | service -> service :?> 'T

  /// <summary>
  /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger{T}" /> from the request's service container.
  ///
  /// The type `'T` should represent the class or module from where the logger gets instantiated.
  /// </summary>
  /// <returns> Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger{T}" />.</returns>
  [<Extension>]
  static member GetLogger<'T>(ctx: HttpContext) = ctx.GetService<ILogger<'T>>()

  /// <summary>
  /// Gets an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/> from the request's service container.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="categoryName">The category name for messages produced by this logger.</param>
  /// <returns>Returns an instance of <see cref="Microsoft.Extensions.Logging.ILogger"/>.</returns>
  [<Extension>]
  static member GetLogger(ctx: HttpContext, categoryName: string) =
    let loggerFactory = ctx.GetService<ILoggerFactory>()
    loggerFactory.CreateLogger categoryName

  /// <summary>
  /// Sets the HTTP status code of the response.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="httpStatusCode">The status code to be set in the response. For convenience you can use the static <see cref="Microsoft.AspNetCore.Http.StatusCodes"/> class for passing in named status codes instead of using pure int values.</param>
  [<Extension>]
  static member SetStatusCode(ctx: HttpContext, httpStatusCode: int) =
    ctx.Response.StatusCode <- httpStatusCode

  /// <summary>
  /// Adds or sets a HTTP header in the response.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="key">The HTTP header name. For convenience you can use the static <see cref="Microsoft.Net.Http.Headers.HeaderNames"/> class for passing in strongly typed header names instead of using pure `string` values.</param>
  /// <param name="value">The value to be set. Non string values will be converted to a string using the object's ToString() method.</param>
  [<Extension>]
  static member SetHttpHeader(ctx: HttpContext, key: string, value: obj) =
    ctx.Response.Headers.[key] <- StringValues(value.ToString())

  /// <summary>
  /// Sets the Content-Type HTTP header in the response.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="contentType">The mime type of the response (e.g.: application/json or text/html).</param>
  [<Extension>]
  static member SetContentType(ctx: HttpContext, contentType: string) =
    ctx.SetHttpHeader(HeaderNames.ContentType, contentType)

  /// <summary>
  /// Writes a byte array to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="bytes">The byte array to be send back to the client.</param>
  /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
  [<Extension>]
  static member WriteBytesAsync(ctx: HttpContext, bytes: byte[]) =
    task {
      ctx.SetHttpHeader(HeaderNames.ContentLength, bytes.Length)

      if ctx.Request.Method <> HttpMethods.Head then
        do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length)

      return Some ctx
    }

  /// <summary>
  /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP Content-Length header accordingly.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="str">The string value to be send back to the client.</param>
  /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
  [<Extension>]
  static member WriteStringAsync(ctx: HttpContext, str: string) =
    ctx.WriteBytesAsync(Encoding.UTF8.GetBytes str)

  /// <summary>
  /// Writes an UTF-8 encoded string to the body of the HTTP response and sets the HTTP `Content-Length` header accordingly, as well as the `Content-Type` header to `text/plain`.
  /// </summary>
  /// <param name="ctx">The current http context object.</param>
  /// <param name="str">The string value to be send back to the client.</param>
  /// <returns>Task of Some HttpContext after writing to the body of the response.</returns>
  [<Extension>]
  static member WriteTextAsync(ctx: HttpContext, str: string) =
    ctx.SetContentType "text/plain; charset=utf-8"
    ctx.WriteStringAsync str
