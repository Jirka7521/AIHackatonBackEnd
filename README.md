# AIHackatonBackEnd

A streamlined C# console application that enables real-time communication with Azure OpenAI's GPT models. Built for learning AI interactions, this project showcases streaming chat capabilities with Azure OpenAI Service.

This backend service allows you to:
- Chat with AI in real-time through a simple console interface
- See responses appear instantly as they're generated
- Maintain context throughout your conversation
- Connect to Azure OpenAI using your own configuration

## Quick Start

1. Make sure you have:
   - .NET 6.0+
   - Azure OpenAI account
   - GPT model deployment

2. Set up your configuration:
   {
   "AzureOpenAI":
      {
         "Endpoint": "https://<your-resource-name>.openai.azure.com/",
         "ApiKey": "<your-api-key>"
         "DeploymentName": "<your-deployment-name>"
      }
   }

3. Run the application:
   ```bash
   dotnet run
   ```

4. Start chatting! Type your messages and press Enter. Submit an empty line to exit.

## Core Components

- **Console Interface**: Handles user input/output
- **LLM Service**: Manages Azure OpenAI communication
- **Configuration**: Flexible settings via appsettings.json

## Technical Stack

- C# / .NET 6.0+
- Azure.AI.OpenAI SDK
- Microsoft.Extensions.Configuration

## Getting Help

If you encounter issues:
1. Verify your Azure OpenAI credentials
2. Ensure your model deployment is active
3. Check your network connection

## License

Educational use only. Created for AI development learning purposes.