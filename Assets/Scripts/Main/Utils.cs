using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Main
{
    public static class Utils
    {
        public static void PlayParticle(string resName, Vector3 pos)
        {
            Object obj = Resources.Load(resName);
            if(obj == null)
            {
                return;
            }
            GameObject effect = (GameObject)Object.Instantiate(obj);
            if(effect != null)
            {
                effect.transform.position = pos;
            }
        }
        public static Color GetTeamColor(ETeam t)
        {
            switch (t)
            {
                case ETeam.A:
                    return Color.red;
                case ETeam.B:
                    return Color.cyan;
                case ETeam.C:
                    return Color.green;
                case ETeam.D:
                    return Color.magenta;
            }
            return Color.white;
        }

        /*
         * curl http://localhost:1234/v1/chat/completions \
           -H "Content-Type: application/json" \
           -d '{
           "model": "qwen2.5-3b-instruct",
           "messages": [
           { "role": "system", "content": "Always answer in rhymes. Today is Thursday" },
           { "role": "user", "content": "What day is it today?" }
           ],
           "temperature": 0.7,
           "max_tokens": -1,
           "stream": false
           }'
         */
        [Serializable]
        public class Message
        {
            public string role;
            public string content;
        }
        
        [Serializable]
        public class LLMRequest
        {
            [Serializable]
            public class JsonSchemaDef
            {
                public string name;
                public bool strict;
                public JsonSchema schema;
            }
            
            [Serializable]
            public class JsonSchema
            {
                [Serializable]
                public class JsonSchemaPropertiesType
                {
                    public string type;
                }
                public string type;
                public Dictionary<string, JsonSchemaPropertiesType> properties;
                public string[] required;
            }
            
            [Serializable]
            public class ResponseFormat
            {
                public string type;
                public JsonSchemaDef json_schema;
            }
            
            public Message[] messages;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public ResponseFormat response_format;
            public float temperature;
        }
        
        [Serializable]
        public class LLMResponse
        {
            [Serializable]
            public class Choice
            {
                public int index;
                public Message message;
            }
            public Choice[] choices;
        }
        public static void LLMUserPrompt(string content, Action<string> callback = null)
        {
            if (!Match.instance.LLMSettingData.EnableLLM)
            {
                callback?.Invoke(string.Empty);
                return;
            }
            Match.instance.StartCoroutine(
                CallLLM(Match.instance.LLMSettingData.APIKey, content, callback));
        }
        private static IEnumerator CallLLM(
            string apiKey, string prompt, Action<string> callback)
        {
            var apiUrl = Match.instance.LLMSettingData.URL;
            if (string.IsNullOrEmpty(apiUrl))
            {
                callback?.Invoke(string.Empty);
                Debug.LogError("API URL is not set in the LLMSettingData.");
                yield break;
            }
            var req = new LLMRequest
            {
                messages = new Message[]
                {
                    new Message
                    {
                        role = "system",
                        content = Match.instance.LLMSettingData.SystemPrompt
                    },
                    new Message { role = "user", content = prompt }
                }
            };
            if (!string.IsNullOrEmpty(Match.instance.LLMSettingData.JsonScheme))
            {
                var respFormat = new LLMRequest.ResponseFormat
                {
                    type = "json_schema",
                    json_schema = new LLMRequest.JsonSchemaDef
                    {
                        name = "user_json_response",
                        strict = true,
                        schema = JsonConvert.DeserializeObject<LLMRequest.JsonSchema>(Match.instance.LLMSettingData.JsonScheme)
                    }
                };
                req.response_format = respFormat;
            }
            
            req.temperature = Match.instance.LLMSettingData.Temperature;
            
            var jsonBody = JsonConvert.SerializeObject(req);
            //Debug.Log("LLM Request: " + jsonBody);
            var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            var request = new UnityWebRequest(apiUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if(!string.IsNullOrEmpty(apiKey)) 
            {
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            }
            yield return request.SendWebRequest();

            var requestStr = string.Empty;
            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = request.downloadHandler.text;
                try
                {
                    var res = JsonConvert.DeserializeObject<LLMResponse>(response);
                    if (res.choices.Length > 0)
                    {
                        requestStr = res.choices[0].message.content;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            else
            {
                Debug.LogError("Request failed: " + request.error);
            }
            request.Dispose();
            callback?.Invoke(requestStr);
        }
    }
}
