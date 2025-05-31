using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        string endpoint = config["AzureOpenAILLM:Endpoint"];
        string deployment = config["AzureOpenAILLM:Deployment"];
        string apiKey = config["AzureOpenAILLM:ApiKey"];

        IChatClient chatClient =
            new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deployment)
            .AsIChatClient();

        // Start the conversation with context for the AI model including learning context.
        List<Microsoft.Extensions.AI.ChatMessage> chatHistory = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, @"
            You are a friendly learning assistant dedicated to helping users understand complex topics and acquire new skills.
            When interacting with users, you should:
            
            1. Introduce yourself and offer a brief overview of your capabilities.
            2. Ask clarifying questions to better understand the user's learning goals.
            3. Provide clear, step-by-step explanations and practical examples.
            4. Reference additional resources or data (e.g., information from related databases or processing of vector data) when helpful.
            5. Conclude your responses by asking if there is anything else you can explain or help with.
            ")
        };

        // Loop to get user input and stream AI response.
        while (true)
        {
            // Get user prompt.
            Console.WriteLine("Your prompt:");
            string? userPrompt = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                continue;
            }

            // Add user prompt to the conversation.
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userPrompt));

            // RAG: Query the vector store using VectorEndpoints.
            List<VectorEndpoints.QueryResult> queryResults = await VectorEndpoints.QueryVectorsAsync(userPrompt, 3);
            string retrievedInfo = "Additional context retrieved from vector store:\n";
            foreach (var result in queryResults)
            {
                retrievedInfo += $"ID: {result.Id}, Content Snippet: {result.Text}\n";
            }

            // Append the retrieved info as additional context to the conversation.
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, retrievedInfo));

            // Stream the AI response and add it to chat history.
            Console.WriteLine("AI Response:");
            string response = "";
            await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory))
            {
                Console.Write(item.Text);
                response += item.Text;
            }
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, response));
            Console.WriteLine();
        }
    }
}