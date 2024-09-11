using Azure;
using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpUnitTestGeneratorExt.LLM
{
    public class AzureOpenAIClient
    {
        private OpenAIClient client;
        private string deploymentName;
        private readonly ChatRequestSystemMessage systemMessage = new ChatRequestSystemMessage("You are an exellent C# code analyzor.");
        public AzureOpenAIClient(string apiEndpoint, string apiKey, string deploymentName)
        {
            client = new OpenAIClient(
                new Uri(apiEndpoint),
                new AzureKeyCredential(apiKey),
                new OpenAIClientOptions(OpenAIClientOptions.ServiceVersion.V2023_06_01_Preview)
            );
            this.deploymentName = deploymentName;
        }

        public List<string> GetSimpleChatCompletions(string input, float temperature = 0.3f, int maxTokens = 390)
        {
            var options = new ChatCompletionsOptions
            {
                Messages = {
                    systemMessage,
                    new ChatRequestUserMessage(input)
                },
                Temperature = temperature,
                DeploymentName = deploymentName,
                ChoiceCount = 3,

                // NucleusSamplingFactor = (float)0.5,
                // FrequencyPenalty = 0,
                // PresencePenalty = 0,
                MaxTokens = Math.Max(maxTokens, 390)
            };
            var completionsResponse = client.GetChatCompletions(options);
            var completions = completionsResponse.Value.Choices.Select(choice => choice.Message.Content).ToList();
            return completions;
        }
    }
}
