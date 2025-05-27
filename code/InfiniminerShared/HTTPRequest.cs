using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;

namespace Infiniminer
{
    public static class HttpRequest
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<string> PostAsync(string url, Dictionary<string, string> parameters)
        {
            try
            {
                using var content = new FormUrlEncodedContent(parameters ?? new Dictionary<string, string>());
                using var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Request error", ex);
            }
        }

        public static async Task<string> GetAsync(string url, Dictionary<string, string> parameters)
        {
            try
            {
            string paramString = EncodeParameters(parameters);
                string fullUrl = string.IsNullOrEmpty(paramString) ? url : $"{url}?{paramString}";
                
                using var response = await client.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Request error", ex);
            }
        }

        // Synchronous wrappers for backward compatibility
        public static string Post(string url, Dictionary<string, string> parameters)
        {
            return PostAsync(url, parameters).GetAwaiter().GetResult();
            }

        public static string Get(string url, Dictionary<string, string> parameters)
        {
            return GetAsync(url, parameters).GetAwaiter().GetResult();
        }

        public static string EncodeParameters(Dictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0) return "";

            var queryParams = new List<string>();
            foreach (var kvp in parameters)
            {
                queryParams.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
            }
            return string.Join("&", queryParams);
        }
    }
}
