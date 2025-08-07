using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Wind.Core.Interfaces;
using Wind.Shared.Protocols;

namespace Wind.Server.Hubs;

public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly IMessageRouter _messageRouter;
    private readonly IProtocolParser _protocolParser;

    public GameHub(ILogger<GameHub> logger, IMessageRouter messageRouter, IProtocolParser protocolParser)
    {
        _logger = logger;
        _messageRouter = messageRouter;
        _protocolParser = protocolParser;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        if (exception != null)
        {
            _logger.LogError(exception, "Error during disconnection for client: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string playerId, string message)
    {
        _logger.LogInformation("Player {PlayerId} sent message: {Message}", playerId, message);
        var chatMessage = new ChatMessage
        {
            Sender = playerId,
            Content = message,
            Timestamp = DateTime.UtcNow
        };
        await _messageRouter.RouteMessageAsync(chatMessage, Context.ConnectionId);
        await Clients.All.SendAsync("ReceiveMessage", playerId, message);
    }

    public async Task UpdatePosition(string playerId, float x, float y, float z)
    {
        _logger.LogInformation("Player {PlayerId} updated position: ({X}, {Y}, {Z})", playerId, x, y, z);
        var positionMessage = new PositionUpdateMessage
        {
            PlayerId = playerId,
            X = x,
            Y = y,
            Z = z,
            Timestamp = DateTime.UtcNow
        };
        await _messageRouter.RouteMessageAsync(positionMessage, Context.ConnectionId);
        await Clients.Others.SendAsync("PlayerPositionUpdated", playerId, x, y, z);
    }

    public async Task ProcessMessage(string jsonMessage)
    {
        var message = _protocolParser.ParseMessage(jsonMessage);
        if (message != null)
        {
            await _messageRouter.RouteMessageAsync(message, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("Failed to parse message: {JsonMessage}", jsonMessage);
        }
    }
}