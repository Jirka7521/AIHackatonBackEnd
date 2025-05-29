using System;
using System.Threading.Tasks;

namespace LLMcomunication
{
    class ConsolePrinter
    {
        public void PrintLine(string message = "")
        {
            Console.WriteLine(message);
        }

        public void Print(string message)
        {
            Console.Write(message);
        }

        public string ReadLine()
        {
            return Console.ReadLine();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var printer = new ConsolePrinter();
            LLMService llmService;

            try
            {
                llmService = new LLMService();
                printer.PrintLine("LLM Service initialized successfully with provided configuration.");
            }
            catch (Exception ex)
            {
                printer.PrintLine($"Critical error: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            printer.PrintLine("Enter your messages. Submit an empty line to exit.");

            while (true)
            {
                printer.Print("User: ");
                string input = printer.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    break;

                printer.Print("Assistant: ");
                try
                {
                    await llmService.GetChatResponseAsync(input, text => printer.Print(text));
                    printer.PrintLine();
                }
                catch (Exception ex)
                {
                    printer.PrintLine(ex.Message);
                }
            }
        }
    }
}