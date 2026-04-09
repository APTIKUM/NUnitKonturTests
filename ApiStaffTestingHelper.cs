using Microsoft.Extensions.Configuration;
using System.ComponentModel.Design;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NUnitKonturTests
{
    internal class ApiStaffTestingHelper
    {
        private IConfiguration _configuration;
        private string _authToken;

        public ApiStaffTestingHelper() 
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile("appsettings.Development.json", true);

            _configuration = builder.Build();
        }

        public static async Task<ApiStaffTestingHelper> CreateAsync()
        {
            var helper = new ApiStaffTestingHelper();
            await helper.AuthAsync();

            return helper;
        }

        public async Task<string> AuthAsync(string login, string password)
        {
            using var client = new HttpClient();

            var url = "https://staff-testing.testkontur.ru/api/v1/auth";


            var json = JsonSerializer.Serialize(new { email = login, password = password });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Ошибка аутентификации: {response.StatusCode}");
            }

            _authToken = await response.Content.ReadAsStringAsync();

            return _authToken;
        }

        public async Task<string> AuthAsync()
        {
            var login = _configuration["Auth:Login"];
            var password = _configuration["Auth:Password"];

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                throw new Exception("Нет логина или пароля в конфиге");
            }

            return await AuthAsync(login, password);
        }


        public async Task EditLikeCommentAsync(string commentId, bool likeStatus)
        {
            var url = $"https://staff-testing.testkontur.ru/api/v1/comments/{commentId}/like";

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _authToken);
            
            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var isLiked = JsonDocument.Parse(json).RootElement.GetProperty("isLiked").GetBoolean();

            if (isLiked != likeStatus)
            {
                request = new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };

                response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                json = await response.Content.ReadAsStringAsync();
                isLiked = JsonDocument.Parse(json).RootElement.GetProperty("isLiked").GetBoolean();
            }

            if (isLiked != likeStatus)
            {
                throw new Exception($"Не удалось установить лайк {likeStatus} для комментария {commentId}");
            }
        }
    }
}
