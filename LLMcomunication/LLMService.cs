// File: LLMService.cs
// Description: Provides functionality to communicate with Azure OpenAI service using a streaming chat interface.
// It sets up the configuration, initializes the chat client, and handles the conversation with a user.
//
// Author: [Your Name]
// Date: [Current Date]
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Azure;
using Azure.AI.OpenAI;

namespace LLMcomunication
{
    // LLMService encapsulates communication with the Azure OpenAI chat service.
    public class LLMService
    {
        private readonly ChatClient _chatClient;
        public List<ChatMessage> Conversation { get; private set; }

        // Constructor initializes configuration, validates settings, and sets up the chat client.
        public LLMService()
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

            // Validate configuration values.
            if (string.IsNullOrWhiteSpace(endpointStr) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(deploymentName))
            {
                // Throw exception if any configuration value is missing.
                throw new Exception("Missing configuration values. Please set AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, and AzureOpenAI:DeploymentName in appsettings.json.");
            }

            // Validate that the endpoint is a valid URI.
            if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out var endpoint))
            {
                throw new Exception("The provided endpoint is not a valid URI.");
            }

            // Initialize the Azure OpenAI client with the given endpoint and API key.
            AzureOpenAIClient azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
            _chatClient = azureClient.GetChatClient(deploymentName);

            // Starting conversation with a system message that sets the context.
            Conversation = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant.")
            };
        }

        // GetChatResponseAsync asynchronously processes user input, streams the assistant's response, and returns the complete reply.
        public async Task<string> GetChatResponseAsync(string userInput, Action<string> streamOutput)
        {
            // Add the user's message to the conversation history.
            Conversation.Add(new UserChatMessage(userInput));

            string completeResponse = string.Empty;
            try
            {
                // Stream the assistant's response from the chat client.
                await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(Conversation))
                {
                    // Process each part of the response and stream it to the client.
                    foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                    {
                        streamOutput(updatePart.Text);
                        completeResponse += updatePart.Text;
                    }
                }
            }
            catch (Exception ex)
            {
                // Wrap and rethrow any exception with additional context.
                throw new Exception("An error occurred while processing the request: " + ex.Message);
            }

            // Add the complete assistant response to the conversation history.
            Conversation.Add(new AssistantChatMessage(completeResponse));
            return completeResponse;
        }
    }
}
}