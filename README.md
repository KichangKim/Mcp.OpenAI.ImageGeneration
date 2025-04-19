# MCP OpenAI Image Generation
MCP server for OpenAI image generation API. This makes AI agent can generate image to specified path.

## Installation
1. Make sure .NET SDK is installed. If not, you can download from https://dotnet.microsoft.com/download 
2. Clone this project. (replace [PROJECT PATH] to yours)
   ```
   git clone https://github.com/KichangKim/Mcp.OpenAI.ImageGeneration.git [PROJECT PATH]
   ```
3. Add MCP server settings to your AI agent application. (replace [PROJECT PATH] and [YOUR_OPENAPI_KEY] to yours)
   ```json
   {
     "servers": {
       "mcp-openai-image-generation": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "[PROJECT PATH]"
         ],
         "env": {
           "OPENAI_API_KEY": "[YOUR_OPENAPI_KEY]"
         }
       }
     }
   }
   ```

## Available Tools
- CreateImage : Creates an image given a prompt using https://platform.openai.com/docs/api-reference/images/create
