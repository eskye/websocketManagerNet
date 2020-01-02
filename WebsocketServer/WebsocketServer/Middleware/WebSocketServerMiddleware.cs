using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebsocketServer.Middleware
{
    public class WebSocketServerMiddleware
    {
        private readonly RequestDelegate _next; 

        public WebSocketServerMiddleware(RequestDelegate next)
        {
             _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
             if(context.WebSockets.IsWebSocketRequest)
                  {
                       WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                       Console.WriteLine("Websocket Connected");

                       await ReceiveMessage(webSocket, async (result, buffer) =>{
                         if(result.MessageType == WebSocketMessageType.Text){
                              Console.WriteLine("Message Received");
                              Console.WriteLine($"Message: {Encoding.UTF8.GetString(buffer, 0, result.Count)}");
                              return;
                         }else if(result.MessageType == WebSocketMessageType.Close){
                             Console.WriteLine("Receive close message");
                             return;
                         }
                       });

                  }else{
                      await _next(context);
                  }
        }

        private async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage){
            var buffer = new byte[1024 * 4];

            while(socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer), CancellationToken.None);
                handleMessage(result, buffer);

            }
        }
    }
}