using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DevOpsTp.Api;

[ApiController]
[Route("api/security-demo")]
public class SecurityDemoController : ControllerBase
{
    [HttpGet("open-redirect")]
    public IActionResult OpenRedirect([FromQuery] string url)
    {
        return Redirect(url);
    }

    [HttpGet("command")]
    public IActionResult Command([FromQuery] string command)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c " + command,
            RedirectStandardOutput = true
        });

        var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;

        return Content(output, "text/plain");
    }

    [HttpGet("file")]
    public IActionResult FileRead([FromQuery] string path)
    {
        var content = System.IO.File.ReadAllText(path);

        return Content(content, "text/plain");
    }

    [HttpGet("xss")]
    public IActionResult ReflectedHtml([FromQuery] string input)
    {
        return Content($"<html><body>{input}</body></html>", "text/html");
    }
}
