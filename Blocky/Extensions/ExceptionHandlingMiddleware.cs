using System;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EonaCat.Logger;
using EonaCat.Logger.Extensions;
using Microsoft.AspNetCore.Http;

namespace EonaCat.Blocky.Extensions;
// Blocky
// Blocking domains the way you want it.
// Copyright EonaCat (Jeroen Saey) 2017-2023
// https://blocky.EonaCat.com

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
    }

    private async void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
        await Logger.LogAsync(e.Exception.FormatExceptionToMessage(), ELogType.ERROR).ConfigureAwait(false);
    }

    private async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        await Logger.LogAsync(e.ExceptionObject.ToString(), ELogType.ERROR).ConfigureAwait(false);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception).ConfigureAwait(false);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorCode = CalculateErrorCode(context.TraceIdentifier);
        var message =
            $"An unhandled exception occurred; please contact the help desk with the following error code: '{errorCode}'  [{context.TraceIdentifier}]";
        await Logger
            .LogAsync($"{errorCode}[{context.TraceIdentifier}]:{Environment.NewLine}{exception}", ELogType.CRITICAL)
            .ConfigureAwait(false);

        context.Response.ContentType = "text/plain";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        if (!context.Response.HasStarted) // Check if the response has not been sent to the client
        {
            await context.Response.WriteAsync(message).ConfigureAwait(false);
        }
    }

    private static string CalculateErrorCode(string traceIdentifier)
    {
        const int errorCodeLength = 6;
        const string codeValues = "BCDFGHJKLMNPQRSTVWXYZ";

        var hasher = MD5.Create();
        var stringBuilder = new StringBuilder(10);
        var traceBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(traceIdentifier));
        var codeValuesLength = codeValues.Length;

        for (var i = 0; i < errorCodeLength; i++) stringBuilder.Append(codeValues[traceBytes[i] % codeValuesLength]);

        return stringBuilder.ToString();
    }
}