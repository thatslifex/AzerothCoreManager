using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using System.Threading;

namespace AzerothCoreManager.Services;

public class SoapService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Sends a GM command to the worldserver via SOAP.
    /// Returns (success, responseText).
    /// </summary>
    public async Task<(bool Success, string Response)> SendCommandAsync(
        string host, int port, string username, string password,
        string command, CancellationToken ct = default)
    {
        var soapXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<SOAP-ENV:Envelope
    xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/""
    xmlns:SOAP-ENC=""http://schemas.xmlsoap.org/soap/encoding/""
    xmlns:xsi=""http://www.w3.org/1999/XMLSchema-instance""
    xmlns:xsd=""http://www.w3.org/1999/XMLSchema""
    xmlns:ns1=""urn:AC"">
    <SOAP-ENV:Body>
        <ns1:executeCommand>
            <command>{EscapeXml(command)}</command>
        </ns1:executeCommand>
    </SOAP-ENV:Body>
</SOAP-ENV:Envelope>";

        var content = new StringContent(soapXml, Encoding.UTF8, "text/xml");

        var authBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(authBytes));

        try
        {
            var url = $"http://{host}:{port}/";
            var response = await _http.PostAsync(url, content, ct);
            var responseXml = await response.Content.ReadAsStringAsync(ct);

            return ParseSoapResponse(responseXml);
        }
        catch (OperationCanceledException)
        {
            return (false, "Command timed out.");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests SOAP connectivity without sending a command.
    /// </summary>
    public async Task<bool> TestConnectionAsync(string host, int port, string username, string password, CancellationToken ct = default)
    {
        var (success, _) = await SendCommandAsync(host, port, username, password, "server info", ct);
        return success;
    }

    private static (bool Success, string Response) ParseSoapResponse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace soapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace ns = "urn:AC";

            var fault = doc.Descendants(soapEnv + "Fault").FirstOrDefault();
            if (fault is not null)
            {
                var faultString = fault.Element("faultstring")?.Value ?? "Unknown SOAP fault";
                return (false, faultString);
            }

            var responseElem = doc.Descendants(ns + "executeCommandResponse").FirstOrDefault();
            if (responseElem is not null)
            {
                var result = responseElem.Element("result")?.Value;
                if (result is not null)
                    return (true, result.Trim());
            }

            var anyResult = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "result")?.Value;
            if (anyResult is not null)
                return (true, anyResult.Trim());

            return (false, "No result element in SOAP response.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse SOAP response: {ex.Message}");
        }
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
