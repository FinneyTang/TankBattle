using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Jev
{
    /// <summary>
    /// Helpers that build the three TypeSafe question primitives as plain dictionaries,
    /// ready to be serialized into the "questions" map of a System One request.
    /// </summary>
    public static class TypeSafeQuestions
    {
        public static Dictionary<string, object> Choice(object instructions, Dictionary<string, object> criteria)
        {
            return new Dictionary<string, object>
            {
                { "type", "choice" },
                { "instructions", instructions },
                { "criteria", criteria },
            };
        }

        public static Dictionary<string, object> Score(object instructions, IList<object> levels)
        {
            return new Dictionary<string, object>
            {
                { "type", "score" },
                { "instructions", instructions },
                { "criteria", levels },
            };
        }

        public static Dictionary<string, object> Noul(object instructions, Dictionary<string, object> criteria = null)
        {
            var q = new Dictionary<string, object>
            {
                { "type", "noul" },
                { "instructions", instructions },
            };
            if (criteria != null)
            {
                q["criteria"] = criteria;
            }
            return q;
        }
    }

    /// <summary>One answer in a System One response. Fields are populated according to <see cref="type"/>.</summary>
    public class TypeSafeAnswer
    {
        public string type;
        // choice
        public string choice;
        // choice + score
        public double? confidence;
        public Dictionary<string, double> probabilities;
        // score
        public double? score;
        public Dictionary<string, string> legend;
        // noul
        public double? noul;

        public float Confidence => (float)(confidence ?? 0.0);
    }

    public class TypeSafeUsage
    {
        public int input_tokens;
        public int output_tokens;
    }

    public class TypeSafeResponse
    {
        public string model;
        public Dictionary<string, TypeSafeAnswer> answers;
        public TypeSafeUsage usage;

        public TypeSafeAnswer GetAnswer(string questionId)
        {
            if (answers == null || string.IsNullOrEmpty(questionId))
            {
                return null;
            }
            answers.TryGetValue(questionId, out var a);
            return a;
        }
    }

    /// <summary>
    /// A single in-flight System One request. It is deliberately coroutine-free: the owner calls
    /// <see cref="Poll"/> once per frame, so the request keeps working while the owning tank is
    /// dead (inactive GameObjects stop coroutines) and is simply consumed later.
    /// </summary>
    public class TypeSafeRequest
    {
        private UnityWebRequest m_Request;
        private readonly UnityWebRequestAsyncOperation m_Operation;
        private readonly float m_StartTime;

        public string RequestJson { get; }
        public string ResponseJson { get; private set; }
        public bool IsDone { get; private set; }
        public bool Succeeded { get; private set; }
        public long HttpStatus { get; private set; }
        public string Error { get; private set; }
        public TypeSafeResponse Response { get; private set; }
        public float LatencyMs { get; private set; }

        public float ElapsedSeconds => IsDone ? LatencyMs * 0.001f : Time.realtimeSinceStartup - m_StartTime;
        public bool IsRateLimited => HttpStatus == 429 || HttpStatus == 529;

        internal TypeSafeRequest(UnityWebRequest request, string requestJson)
        {
            m_Request = request;
            RequestJson = requestJson;
            m_StartTime = Time.realtimeSinceStartup;
            m_Operation = request.SendWebRequest();
        }

        /// <summary>Returns true once the request has finished (successfully or not).</summary>
        public bool Poll()
        {
            if (IsDone)
            {
                return true;
            }
            if (m_Operation == null || !m_Operation.isDone)
            {
                return false;
            }
            Finish();
            return true;
        }

        public void Abort(string reason)
        {
            if (IsDone)
            {
                return;
            }
            LatencyMs = (Time.realtimeSinceStartup - m_StartTime) * 1000f;
            IsDone = true;
            Succeeded = false;
            Error = reason;
            try
            {
                m_Request?.Abort();
            }
            catch (Exception)
            {
                // Abort on an already-finished request is harmless.
            }
            Dispose();
        }

        private void Finish()
        {
            LatencyMs = (Time.realtimeSinceStartup - m_StartTime) * 1000f;
            IsDone = true;
            HttpStatus = m_Request.responseCode;
            ResponseJson = m_Request.downloadHandler != null ? m_Request.downloadHandler.text : string.Empty;

            if (m_Request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Response = JsonConvert.DeserializeObject<TypeSafeResponse>(ResponseJson);
                    Succeeded = Response != null && Response.answers != null;
                    if (!Succeeded)
                    {
                        Error = "response has no answers";
                    }
                }
                catch (Exception e)
                {
                    Succeeded = false;
                    Error = "failed to parse response: " + e.Message;
                }
            }
            else
            {
                Succeeded = false;
                var body = ResponseJson ?? string.Empty;
                if (body.Length > 300)
                {
                    body = body.Substring(0, 300) + "...";
                }
                Error = $"{m_Request.result} (HTTP {HttpStatus}): {m_Request.error} {body}";
            }
            Dispose();
        }

        private void Dispose()
        {
            m_Request?.Dispose();
            m_Request = null;
        }
    }

    /// <summary>Minimal client for POST https://api.typesafe.ai/v1/systemone.</summary>
    public class TypeSafeClient
    {
        private readonly string m_ApiKey;
        private readonly string m_Endpoint;
        private readonly string m_Model;
        private readonly int m_TimeoutSeconds;

        public bool HasApiKey => !string.IsNullOrEmpty(m_ApiKey);
        public string Model => m_Model;

        public TypeSafeClient(string apiKey, string endpoint, string model, float timeoutSeconds)
        {
            m_ApiKey = apiKey;
            m_Endpoint = endpoint;
            m_Model = model;
            // UnityWebRequest.timeout is whole seconds; keep it a bit above our own soft timeout.
            m_TimeoutSeconds = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds) + 1);
        }

        /// <summary>
        /// Sends state + questions. <paramref name="state"/> may be a string, a dictionary or any
        /// JSON-serializable object; <paramref name="questions"/> maps question ids to question dictionaries.
        /// </summary>
        public TypeSafeRequest Send(object state, Dictionary<string, object> questions)
        {
            var payload = new Dictionary<string, object>
            {
                { "state", state },
                { "model", m_Model },
                { "questions", questions },
            };
            var json = JsonConvert.SerializeObject(payload, Formatting.None);
            var request = new UnityWebRequest(m_Endpoint, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = m_TimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + m_ApiKey);
            return new TypeSafeRequest(request, json);
        }
    }
}
