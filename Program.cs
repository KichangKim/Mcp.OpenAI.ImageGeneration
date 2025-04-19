using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OpenAI.Images;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class OpenAITools
{
    [McpServerTool, Description("Creates an image given a prompt.")]
    public static async Task<string> CreateImage(
        [Description("A text description of the desired image(s). The maximum length is 1000 characters for dall-e-2 and 4000 characters for dall-e-3.")]
        string prompt,
        [Description("The path of the generated image. Must be absolute path.")]
        string outputPath,
        [Description("The model to use for image generation. Must be dall-e-2 or dall-e-3. Defaults to dall-e-3.")]
        string model = "dall-e-3",
        [Description("The quality of the image that will be generated. hd creates images with finer details and greater consistency across the image. This param is only supported for dall-e-3. Must be hd or standard. Defaults to standard.")]
        string quality = "standard",
        [Description("The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024 for dall-e-2. Must be one of 1024x1024, 1792x1024, or 1024x1792 for dall-e-3 models. Defaults to 1024x1024.")]
        string size = "1024x1024",
        [Description("The style of the generated images. Must be one of vivid or natural. Vivid causes the model to lean towards generating hyper-real and dramatic images. Natural causes the model to produce more natural, less hyper-real looking images. This param is only supported for dall-e-3. Defaults to vivid.")]
        string style = "vivid")
    {
        try
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var client = new ImageClient(model, apiKey);
            var options = new ImageGenerationOptions()
            {
                ResponseFormat = GeneratedImageFormat.Bytes,
            };

            switch (quality)
            {
                case "hd": options.Quality = GeneratedImageQuality.High; break;
                case "standard": options.Quality = GeneratedImageQuality.Standard; break;
                default: throw new ArgumentException("Invalid argument 'quality'.");
            }

            switch (size)
            {
                case "256x256": options.Size = GeneratedImageSize.W256xH256; break;
                case "512x512": options.Size = GeneratedImageSize.W512xH512; break;
                case "1024x1024": options.Size = GeneratedImageSize.W1024xH1024; break;
                case "1792x1024": options.Size = GeneratedImageSize.W1792xH1024; break;
                case "1024x1792": options.Size = GeneratedImageSize.W1024xH1792; break;
                default: throw new ArgumentException("Invalid argument 'size'.");
            }

            switch (style)
            {
                case "vivid": options.Style = GeneratedImageStyle.Vivid; break;
                case "natural": options.Style = GeneratedImageStyle.Natural; break;
                default: throw new ArgumentException("Invalid argument 'style'.");
            }

            var response = await client.GenerateImageAsync(prompt, options);

            using (var stream = new FileStream(outputPath, FileMode.Create))
            {
                response.Value.ImageBytes.ToStream().CopyTo(stream);
            }

            return $"Image is generated to {outputPath}";
        }
        catch (Exception e)
        {
            return $"An error occurred: {e}";
        }
    }
}