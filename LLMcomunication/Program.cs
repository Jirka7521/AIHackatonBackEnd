using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Azure;
using Azure.AI.OpenAI;
using System.IO;

try
{
    // Build configuration from appsettings.json.
    IConfiguration config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    // Retrieve secure settings from configuration.
    string endpointStr = config["AzureOpenAI:Endpoint"];
    string apiKey = config["AzureOpenAI:ApiKey"];
    string deploymentName = config["AzureOpenAI:DeploymentName"];

    // Test/log configuration values (avoid exposing sensitive data in production).
    Console.WriteLine("Configuration Values:");
    Console.WriteLine(string.IsNullOrWhiteSpace(endpointStr)
        ? "AzureOpenAI:Endpoint not set"
        : $"AzureOpenAI:Endpoint is set to {endpointStr}");
    Console.WriteLine(string.IsNullOrWhiteSpace(apiKey)
        ? "AzureOpenAI:ApiKey not set"
        : $"AzureOpenAI:ApiKey is set ({(apiKey.Length > 4 ? $"{apiKey.Substring(0, 4)}..." : "value present")})");
    Console.WriteLine(string.IsNullOrWhiteSpace(deploymentName)
        ? "AzureOpenAI:DeploymentName not set"
        : $"AzureOpenAI:DeploymentName is set to {deploymentName}");

    if (string.IsNullOrWhiteSpace(endpointStr) ||
        string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(deploymentName))
    {
        throw new Exception("Missing configuration values. Please set AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, and AzureOpenAI:DeploymentName in appsettings.json.");
    }

    if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out var endpoint))
    {
        throw new Exception("The provided endpoint is not a valid URI.");
    }

    // Initialize the Azure OpenAI client.
    AzureOpenAIClient azureClient = new(
        endpoint,
        new AzureKeyCredential(apiKey));
    ChatClient chatClient = azureClient.GetChatClient(deploymentName);

    // Starting conversation with a system message that sets the context.
    List<ChatMessage> messages = new List<ChatMessage>
    {
        new SystemChatMessage("You are a helpful assistant.")
    };

    Console.WriteLine("Enter your messages. Submit an empty line to exit.");

    while (true)
    {
        Console.Write("User: ");
        string input = Console.ReadLine();

        // Exit the chat if the user inputs an empty message.
        if (string.IsNullOrWhiteSpace(input))
            break;

        // Add the user's message to the conversation history.
        messages.Add(new UserChatMessage(input));

        Console.Write("Assistant: ");
        try
        {
            var response = chatClient.CompleteChatStreaming(messages);

            // Buffer to capture the complete assistant's answer.
            string completeResponse = string.Empty;

            foreach (StreamingChatCompletionUpdate update in response)
            {
                foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                {
                    Console.Write(updatePart.Text);
                    completeResponse += updatePart.Text;
                }
            }
            Console.WriteLine();

            // Add the assistant's reply to the conversation history.
            messages.Add(new AssistantChatMessage(completeResponse));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while processing the request: {ex.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Critical error: {ex.Message}");
    Environment.Exit(1);
}