using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OpenAI.Images;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

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
builder.Services.AddSingleton(_ =>
{
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Bearer",
        Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    return httpClient;
});

await builder.Build().RunAsync();

[McpServerToolType]
public static class OpenAITools
{
    [McpServerTool, Description("Creates an image given a prompt.")]
    public static async Task<string> CreateImage(
        HttpClient httpClient,
        [Description("A text description of the desired image. The maximum length is 32000 characters.")]
        string prompt,
        [Description("The path of the generated image. Must be absolute path.")]
        string outputPath,
        [Description("Allows to set transparency for the background of the generated image. Must be one of transparent, opaque or auto (default value). When auto is used, the model will automatically determine the best background for the image. If transparent, the output format needs to support transparency, so it should be set to either png (default value) or webp.")]
        string background = "auto",
        [Description("Control the content-moderation level for image. Must be either low for less (default value) restrictive filtering or auto.")]
        string moderation = "less",
        [Description("The compression level (0-100%) for the generated image. This parameter is only supported for the webp or jpeg output formats, and defaults to 100.")]
        int outputCompression = 100,
        [Description("The format in which the generated image is returned. Must be one of png, jpeg, or webp. Defaults to png.")]
        string outputFormat = "png",
        [Description("The quality of the image that will be generated. Must be one of auto (default value), high, medium or low.")]
        string quality = "auto",
        [Description("The size of the generated images. Must be one of 1024x1024, 1536x1024 (landscape), 1024x1536 (portrait), or auto (default value).")]
        string size = "auto")
    {
        try
        {
            var apiEndpoint = "https://api.openai.com/v1/images/generations";
            var model = "gpt-image-1";

            var parameters = new Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["background"] = background,
                ["model"] = model,
                ["moderation"] = moderation,
                ["output_compression"] = outputCompression,
                ["output_format"] = outputFormat,
                ["quality"] = quality,
                ["size"] = size,
            };

            var postContent = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(apiEndpoint, postContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                using (var stream = new FileStream(outputPath, FileMode.Create))
                {
                    var bytes = jsonDocument.RootElement
                        .GetProperty("data")[0]
                        .GetProperty("b64_json")
                        .GetBytesFromBase64();
                    stream.Write(bytes);
                }

                return $"Image is generated to {outputPath}";
            }
            else
            {
                var responseString = await response.Content.ReadAsStringAsync();

                return $"An error occurred: {responseString}";
            }
        }
        catch (Exception e)
        {
            return $"An error occurred: {e}";
        }
    }

    [McpServerTool, Description("Creates an edited or extended image given image and a prompt. You can edit or modify image by using this tool.")]
    public static async Task<string> CreateImageEdit(
        HttpClient httpClient,
        [Description("The path of the image to edit. Must be a png, webp, or jpg file less than 25MB.")]
        string inputPath,
        [Description("A text description of the desired image. The maximum length is 32000 characters.")]
        string prompt,
        [Description("The path of the generated image. Must be absolute path.")]
        string outputPath,
        [Description("The path of an additional image whose fully transparent areas (e.g. where alpha is zero) indicate where image should be edited. Must be a valid PNG file, less than 4MB, and have the same dimensions as image. Optional.")]
        string maskPath = "",
        [Description("The quality of the image that will be generated. Must be one of auto (default value), high, medium or low.")]
        string quality = "auto",
        [Description("The size of the generated images. Must be one of 1024x1024, 1536x1024 (landscape), 1024x1536 (portrait), or auto (default value).")]
        string size = "auto")
    {
        try
        {
            var apiEndpoint = "https://api.openai.com/v1/images/edits";
            var model = "gpt-image-1";
            
            using var postContent = new MultipartFormDataContent();

            using var inputStream = new FileStream(inputPath, FileMode.Open);
            using var maskStream = !string.IsNullOrEmpty(maskPath) ? new FileStream(maskPath, FileMode.Open) : null;

            postContent.Add(new StreamContent(inputStream), "image", Path.GetFileName(inputPath));

            if (maskStream != null)
            {
                postContent.Add(new StreamContent(maskStream), "mask", Path.GetFileName(maskPath));
            }

            postContent.Add(new StringContent(prompt), "prompt");
            postContent.Add(new StringContent(model), "model");
            postContent.Add(new StringContent(quality), "quality");
            postContent.Add(new StringContent(size), "size");

            var response = await httpClient.PostAsync(apiEndpoint, postContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                using (var stream = new FileStream(outputPath, FileMode.Create))
                {
                    var bytes = jsonDocument.RootElement
                        .GetProperty("data")[0]
                        .GetProperty("b64_json")
                        .GetBytesFromBase64();
                    stream.Write(bytes);
                }

                return $"Edited image is generated to {outputPath}";
            }
            else
            {
                var responseString = await response.Content.ReadAsStringAsync();

                return $"An error occurred: {responseString}";
            }
        }
        catch (Exception e)
        {
            return $"An error occurred: {e}";
        }
    }

    [McpServerTool, Description("Creates an image given a prompt using legacy models.")]
    public static async Task<string> CreateImageLegacy(
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