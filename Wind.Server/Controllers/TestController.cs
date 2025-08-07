using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wind.Core.Interfaces;

namespace Wind.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private readonly IMessageRouter _messageRouter;

    public TestController(ILogger<TestController> logger, IMessageRouter messageRouter)
    {
        _logger = logger;
        _messageRouter = messageRouter;
    }

    [HttpGet]
    public IActionResult Get()
    {
        _logger.LogInformation("Test API called");
        return Ok(new { Message = "Server is running", Status = "OK" });
    }

    [HttpPost("send-message")]
    public IActionResult SendMessage([FromBody] string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return BadRequest("Message cannot be empty");
        }

        // 注意：IMessageRouter接口只有RouteMessageAsync方法，且需要BaseMessage类型参数
        // 这里暂时注释掉，后续需要根据实际情况实现
        // _messageRouter.RouteMessageAsync(new BaseMessage { Content = message }, "system");
        return Ok(new { Status = "Message routed", Message = message });
    }
}