using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Azure;
using Azure.AI.OpenAI;

namespace LLMcomunication
{
    public class LLMService
    {
        private readonly ChatClient _chatClient;
        public List<ChatMessage> Conversation { get; private set; }

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
                throw new Exception("Missing configuration values. Please set AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, and AzureOpenAI:DeploymentName in appsettings.json.");
            }

            if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out var endpoint))
            {
                throw new Exception("The provided endpoint is not a valid URI.");
            }

            // Initialize the Azure OpenAI client.
            AzureOpenAIClient azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey));
            _chatClient = azureClient.GetChatClient(deploymentName);

            // Starting conversation with a system message that sets the context.
            Conversation = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant.")
            };
        }

        // Asynchronously processes the user input, streams the assistant response, and returns the complete answer.
        public async Task<string> GetChatResponseAsync(string userInput, Action<string> streamOutput)
        {
            // Add the user's message to the conversation history.
            Conversation.Add(new UserChatMessage(userInput));

            string completeResponse = string.Empty;
            try
            {
                await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(Conversation))
                {
                    foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                    {
                        streamOutput(updatePart.Text);
                        completeResponse += updatePart.Text;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while processing the request: " + ex.Message);
            }

            // Add the assistant's reply to the conversation history.
            Conversation.Add(new AssistantChatMessage(completeResponse));
            return completeResponse;
        }
    }
}