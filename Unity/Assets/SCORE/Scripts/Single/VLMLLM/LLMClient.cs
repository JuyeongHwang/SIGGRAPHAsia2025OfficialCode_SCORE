using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Gemini (Google Generative Language API) requests shared by the DPS (VLM/LLM) and RES (FastVLM) connectors.
public static class LLMClient
{
    public const string GeminiBaseURL = "https://generativelanguage.googleapis.com/v1/models/";

    // Builds the POST request for one prompt (+ optional base64 PNG images).
    public static UnityWebRequest BuildRequest(string model, string apiKey, string prompt, List<string> images)
    {
        var parts = new List<object> { new { text = prompt } };
        if (images != null)
        {
            foreach (var img in images)
                parts.Add(new { inlineData = new { mimeType = "image/png", data = img } });
        }
        var body = new { contents = new object[] { new { parts = parts.ToArray() } } };

        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
        var request = new UnityWebRequest(GeminiBaseURL + model + ":generateContent?key=" + apiKey, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    // Sends the request and returns the plain-text answer ("" on failure).
    public static IEnumerator Send(string model, string apiKey, string prompt, List<string> images,
                                   Action<string> onText, string tag)
    {
        using (var request = BuildRequest(model, apiKey, prompt, images))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[{tag}] {model} Result={request.result}, Code={request.responseCode}, " +
                               $"Error={request.error}\nBody={request.downloadHandler?.text}");
                onText?.Invoke("");
                yield break;
            }
            onText?.Invoke(ExtractText(request.downloadHandler.text, tag));
        }
    }

    public static string ExtractText(string raw, string tag)
    {
        try
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return "";

            // generateContent returns one object, streamGenerateContent an array of chunks.
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
