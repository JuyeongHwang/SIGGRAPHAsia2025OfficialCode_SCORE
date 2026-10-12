using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Backend used by the DPS (VLM/LLM) and RES (FastVLM) connectors.
// Gemini  : Google Generative Language API (paper setting).
// Ollama  : local Ollama server on each user's own machine (e.g. Qwen2.5-VL).
public enum LLMProvider { Gemini, Ollama }

public static class LLMClient
{
    public const string GeminiBaseURL = "https://generativelanguage.googleapis.com/v1/models/";
    public const string DefaultOllamaURL = "http://localhost:11434";

    // Environment overrides so a scene can be switched without touching serialized fields:
    //   SCORE_PROVIDER=ollama|gemini, SCORE_OLLAMA_URL=http://host:11434
    public static LLMProvider Resolve(LLMProvider serialized)
    {
        string env = Environment.GetEnvironmentVariable("SCORE_PROVIDER");
        if (string.IsNullOrEmpty(env)) return serialized;
        return env.Trim().ToLower() == "ollama" ? LLMProvider.Ollama : LLMProvider.Gemini;
    }

    public static string ResolveOllamaURL(string serialized)
    {
        string env = Environment.GetEnvironmentVariable("SCORE_OLLAMA_URL");
        if (!string.IsNullOrEmpty(env)) return env.TrimEnd('/');
        return string.IsNullOrEmpty(serialized) ? DefaultOllamaURL : serialized.TrimEnd('/');
    }

    // Builds the POST request for one prompt (+ optional base64 PNG images).
    public static UnityWebRequest BuildRequest(LLMProvider provider, string model, string apiKey,
                                               string ollamaURL, string prompt, List<string> images)
    {
        string url;
        object body;

        if (provider == LLMProvider.Gemini)
        {
            var parts = new List<object> { new { text = prompt } };
            if (images != null)
            {
                foreach (var img in images)
                    parts.Add(new { inlineData = new { mimeType = "image/png", data = img } });
            }
            body = new { contents = new object[] { new { parts = parts.ToArray() } } };
            url = GeminiBaseURL + model + ":generateContent?key=" + apiKey;
        }
        else
        {
            var message = new Dictionary<string, object> { { "role", "user" }, { "content", prompt } };
            if (images != null && images.Count > 0)
                message["images"] = images.ToArray();
            body = new
            {
                model = model,
                messages = new object[] { message },
                stream = false,
                think = false, // Qwen3(-VL) defaults to a long thinking trace; SCORE parses only the answer
                options = new { temperature = 0.7, num_ctx = 8192 } // images + SCORE prompt exceed the 4k default
            };
            url = ResolveOllamaURL(ollamaURL) + "/api/chat";
        }

        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
        var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (provider == LLMProvider.Ollama)
            request.timeout = 300; // local models can be slow on first load
        return request;
    }

    // Sends the request and returns the plain-text answer ("" on failure).
    public static IEnumerator Send(LLMProvider provider, string model, string apiKey, string ollamaURL,
                                   string prompt, List<string> images, Action<string> onText, string tag)
    {
        using (var request = BuildRequest(provider, model, apiKey, ollamaURL, prompt, images))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[{tag}] {provider} {model} Result={request.result}, Code={request.responseCode}, " +
                               $"Error={request.error}\nBody={request.downloadHandler?.text}");
                onText?.Invoke("");
                yield break;
            }
            onText?.Invoke(ExtractText(provider, request.downloadHandler.text, tag));
        }
    }

    public static string ExtractText(LLMProvider provider, string raw, string tag)
    {
        try
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return "";

            if (provider == LLMProvider.Ollama)
            {
                var obj = JObject.Parse(trimmed);
                string content = obj["message"]?["content"]?.ToString() ?? "";
                // Qwen3 thinking variants may prepend a <think>...</think> block.
                return System.Text.RegularExpressions.Regex.Replace(
                    content, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
            }

            // Gemini: generateContent returns one object, streamGenerateContent an array of chunks.
            var sb = new StringBuilder();
            var token = JToken.Parse(trimmed);
            var chunks = token is JArray arr ? (IEnumerable<JToken>)arr : new[] { token };
            foreach (var chunk in chunks)
            {
                if (chunk["error"] != null)
                {
                    Debug.LogError($"[{tag}] API Error {chunk["error"]?["code"]}: {chunk["error"]?["message"]}");
                    continue;
                }
                var parts = chunk["candidates"]?[0]?["content"]?["parts"] as JArray;
                if (parts == null) continue;
                foreach (var p in parts)
                    sb.Append(p?["text"]?.ToString());
            }
            return sb.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"[{tag}] parse error: {e.Message}\n{raw}");
            return "";
        }
    }
}
