using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace CSharpUnitTestGeneratorExt.LLM
{
	public class GithubCopilotClient
	{
		private static string accessToken = "";

		public async Task<List<string>> GetSimpleChatCompletions(string input)
		{
			if (File.Exists("access_token.txt"))
			{
				accessToken = File.ReadAllText("access_token.txt");
			}
			else
			{
				await AuthorizeGithubAsync();
			}

			if (string.IsNullOrEmpty(accessToken))
			{
				MessageBox.Show("Failed to authorize Github Copilot.");
				return new List<string>();
			}
			sharedClient.BaseAddress = new Uri("https://copilot-proxy.githubusercontent.com/v1/engines/copilot-codex/completions");
			sharedClient.DefaultRequestHeaders.Accept.Clear();
			sharedClient.DefaultRequestHeaders.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));
			sharedClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
			var body = new StringContent(@"{""prompt"":""" + input + @""",""max_tokens"":1000}");

			var resp = await sharedClient.PostAsync("https://api.github.com/user", body);
			var content = await resp.Content.ReadAsStringAsync();
			var json = JsonConvert.DeserializeObject<JObject>(content);
			var completions = json["choices"].Select(choice => choice["text"].ToString()).ToList();
			return completions;
		}

		private static readonly HttpClient sharedClient = new HttpClient
		{
			BaseAddress = new Uri("https://github.com/")
		};

		private async Task<JObject> GrantAccessAsync()
		{
			sharedClient.DefaultRequestHeaders.Accept.Clear();
			sharedClient.DefaultRequestHeaders.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));
			sharedClient.DefaultRequestHeaders.Add("editor-version", "Neovim/0.6.1");
			sharedClient.DefaultRequestHeaders.Add("editor-plugin-version", "copilot.vim/1.16.0");
			sharedClient.DefaultRequestHeaders.Add("content-type", "application/json");
			sharedClient.DefaultRequestHeaders.Add("user-agent", "GithubCopilot/1.155.0");
			sharedClient.DefaultRequestHeaders.Add("accept-encoding", "gzip,deflate,br");

			var body = new StringContent(@"{{""client_id"":""Iv1.b507a08c87ecfe98"",""scope"":""read:user""}}");

			var resp = await sharedClient.PostAsync("login/device/code", body);
			// parse response as object
			var content = await resp.Content.ReadAsStringAsync();
			// parse to json
			var json = JsonConvert.DeserializeObject<JObject>(content);
			var url = json["verification_uri"].ToString();
			// open browser
			System.Diagnostics.Process.Start(url);
			MessageBox.Show("Please use this code to authorize: " + json["user_code"].ToString());
			return json;
		}

		private async Task AuthorizeGithubAsync()
		{
			var json = await GrantAccessAsync();

			var device_code = json["device_code"].ToString();
			var body = new StringContent($@"{{""client_id"":""Iv1.b507a08c87ecfe98"",""device_code"":""{device_code}"",""grant_type"":""urn:ietf:params:oauth:grant-type:device_code""}}");
			var accessToken = "";
			while (true)
			{
				Thread.Sleep(5000);
				var resp = await sharedClient.PostAsync("login/oauth/access_token", body);
				var content = await resp.Content.ReadAsStringAsync();
				var token = JsonConvert.DeserializeObject<JObject>(content);
				if (token.ContainsKey("access_token"))
				{
					accessToken = token["access_token"].ToString();
					break;
				}
			}
			// write to file
			File.WriteAllText("access_token.txt", accessToken);
		}
	}
}
