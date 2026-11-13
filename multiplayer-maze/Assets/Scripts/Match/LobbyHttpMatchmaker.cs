using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Talks to the Node lobby (POST/GET /api/rooms, POST /api/rooms/:id/join).
/// Pass an empty base URL in a WebGL build: the page's own origin is used, so there is no CORS.
/// </summary>
public sealed class LobbyHttpMatchmaker : IMatchmaker
{
    private const int _TIMEOUT_SECONDS = 10;

    private readonly string _baseUrl;

    public LobbyHttpMatchmaker(string baseUrl = "")
    {
        _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
    }

    public async Task<MatchResult> CreateRoomAsync(string playerName, string roomName, int maxPlayers)
    {
        var fields = new List<string> { Field("playerName", playerName) };
        if (!string.IsNullOrEmpty(roomName))
            fields.Add(Field("roomName", roomName));
        if (maxPlayers > 0)
            fields.Add("\"maxPlayers\":" + maxPlayers);

        Response response = await SendAsync("POST", "/api/rooms", "{" + string.Join(",", fields) + "}");
        return ParseConnect(response);
    }

    public async Task<MatchResult> JoinRoomAsync(string roomId, string playerName)
    {
        string code = MatchValidation.NormalizeRoomId(roomId);
        if (code == null)
            return MatchResult.Failure(MatchErrors.InvalidRequest);

        Response response = await SendAsync("POST", "/api/rooms/" + code + "/join", "{" + Field("playerName", playerName) + "}");
        return ParseConnect(response);
    }

    public async Task<RoomListResult> ListRoomsAsync()
    {
        Response response = await SendAsync("GET", "/api/rooms", null);
        return ParseList(response.code, response.body, response.networkFailed);
    }

    // Parsing is public so it can be tested without a server.
    public static MatchResult ParseConnect(long code, string body, bool networkFailed)
        => ParseConnect(new Response { code = code, body = body, networkFailed = networkFailed });

    public static RoomListResult ParseList(long code, string body, bool networkFailed)
    {
        if (networkFailed)
            return RoomListResult.Failure(MatchErrors.Network);

        if (code != 200)
            return RoomListResult.Failure(ErrorFrom(code, body));

        ListDto dto = TryParse<ListDto>(body);
        if (dto == null)
            return RoomListResult.Failure(MatchErrors.Network);

        var rooms = new List<RoomInfo>();
        if (dto.rooms != null)
            foreach (RoomDto room in dto.rooms)
                rooms.Add(ToInfo(room));

        return RoomListResult.Success(rooms);
    }

    private static MatchResult ParseConnect(Response response)
    {
        if (response.networkFailed)
            return MatchResult.Failure(MatchErrors.Network);

        // 201 on create, 200 on join.
        if (response.code != 200 && response.code != 201)
            return MatchResult.Failure(ErrorFrom(response.code, response.body));

        ConnectDto dto = TryParse<ConnectDto>(response.body);
        if (dto == null || dto.room == null || string.IsNullOrEmpty(dto.token) || dto.port <= 0 || dto.port > ushort.MaxValue)
            return MatchResult.Failure(MatchErrors.Network);

        var ticket = new RoomTicket(dto.room.id, dto.host, (ushort)dto.port, dto.path, dto.secure, dto.token);
        return MatchResult.Success(ToInfo(dto.room), ticket);
    }

    private static string ErrorFrom(long code, string body)
    {
        if (code == 429)
            return MatchErrors.RateLimited;

        // The lobby answers its own failures as { "error": "room_full" }.
        ErrorDto dto = TryParse<ErrorDto>(body);
        if (dto != null && !string.IsNullOrEmpty(dto.error) && dto.error.IndexOf(' ') < 0)
            return dto.error;

        return code == 400 ? MatchErrors.InvalidRequest : MatchErrors.Network;
    }

    private static RoomInfo ToInfo(RoomDto room)
        => new RoomInfo(room.id, room.name, room.players, room.maxPlayers, room.state);

    private static T TryParse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string Field(string key, string value)
        => "\"" + key + "\":\"" + Escape(value) + "\"";

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c == '"' || c == '\\')
                builder.Append('\\').Append(c);
            else if (c < ' ')
                builder.Append(' ');
            else
                builder.Append(c);
        }

        return builder.ToString();
    }

    private Task<Response> SendAsync(string method, string path, string json)
    {
        var completion = new TaskCompletionSource<Response>();

        var request = new UnityWebRequest(_baseUrl + path, method)
        {
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = _TIMEOUT_SECONDS
        };

        if (json != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        operation.completed += _ =>
        {
            // A 4xx/5xx is a ProtocolError with a body we still want to read; only
            // a request that never reached the server counts as a network failure.
            bool networkFailed = request.result == UnityWebRequest.Result.ConnectionError;

            completion.TrySetResult(new Response
            {
                code = request.responseCode,
                body = request.downloadHandler.text,
                networkFailed = networkFailed
            });

            request.Dispose();
        };

        return completion.Task;
    }

    private struct Response
    {
        public long code;
        public string body;
        public bool networkFailed;
    }

    [Serializable]
    private sealed class RoomDto
    {
        public string id;
        public string name;
        public int players;
        public int maxPlayers;
        public string state;
    }

    [Serializable]
    private sealed class ConnectDto
    {
        public RoomDto room;
        public string host;
        public int port;
        public bool secure;
        public string path;
        public string token;
    }

    [Serializable]
    private sealed class ListDto
    {
        public RoomDto[] rooms;
    }

    [Serializable]
    private sealed class ErrorDto
    {
        public string error;
    }
}
