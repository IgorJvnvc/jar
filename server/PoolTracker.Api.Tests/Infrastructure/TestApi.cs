using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PoolTracker.Api.Tests.Infrastructure;

internal static class TestApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static int emailCounter;

    public static async Task<TestAuthSession> RegisterAndLoginAsync(
        CustomWebApplicationFactory factory,
        string displayNamePrefix = "Player")
    {
        var unique = Interlocked.Increment(ref emailCounter);
        var displayName = $"{displayNamePrefix} {unique}";
        var email = $"{displayNamePrefix.ToLowerInvariant().Replace(" ", string.Empty)}{unique}@pool.test";
        const string password = "Password1";

        var client = factory.CreateApiClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName,
            email,
            password
        });

        await EnsureSuccessAsync(registerResponse);
        var auth = await DeserializeAsync<AuthResponseDto>(registerResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return new TestAuthSession(
            client,
            auth.AccessToken,
            auth.RefreshToken,
            auth.User.Id,
            auth.User.DisplayName,
            auth.User.Email,
            password);
    }

    public static async Task<HttpResponseMessage> GetAsync(TestAuthSession session, string path)
    {
        return await session.Client.GetAsync(path);
    }

    public static async Task<HttpResponseMessage> PostAsync(TestAuthSession session, string path, object payload)
    {
        return await session.Client.PostAsJsonAsync(path, payload);
    }

    public static async Task<HttpResponseMessage> PutAsync(TestAuthSession session, string path, object payload)
    {
        return await session.Client.PutAsJsonAsync(path, payload);
    }

    public static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
    {
        return await DeserializeAsync<T>(response);
    }

    public static async Task<ErrorEnvelope?> ReadErrorAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ErrorEnvelope>(content, JsonOptions);
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException(
            $"Expected success status but received {(int)response.StatusCode} ({response.StatusCode}). Body: {content}");
    }

    public static async Task EnsureStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException(
            $"Expected status {(int)expected} ({expected}) but received {(int)response.StatusCode} ({response.StatusCode}). Body: {content}");
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        if (payload is null)
        {
            throw new Xunit.Sdk.XunitException($"Response body could not be deserialized as {typeof(T).Name}.");
        }

        return payload;
    }
}
