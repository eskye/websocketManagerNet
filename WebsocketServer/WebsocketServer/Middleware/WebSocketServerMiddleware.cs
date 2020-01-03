using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Newtonsoft.Json;

namespace WebsocketServer.Middleware
{
    public class WebSocketServerMiddleware
    {
        private readonly RequestDelegate _next; 

       private readonly WebSocketServerConnectionManager _connectionManager;
        public WebSocketServerMiddleware(RequestDelegate next, WebSocketServerConnectionManager connectionManager)
        {
             _next = next;
             _connectionManager = connectionManager;
        }

        public async Task InvokeAsync(HttpContext context)
        {
             if(context.WebSockets.IsWebSocketRequest)
                  {
                       WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                       Console.WriteLine("Websocket Connected");

                       string connID = _connectionManager.AddSocket(webSocket);
                        await SendConnIDAsync(webSocket, connID);
                       await ReceiveMessage(webSocket, async (result, buffer) =>{
                         if(result.MessageType == WebSocketMessageType.Text)
                         {
                              Console.WriteLine("Message Received: ");
                              Console.WriteLine($"Message: {Encoding.UTF8.GetString(buffer, 0, result.Count)}");
                              await RouteJSONMessageAsync(Encoding.UTF8.GetString(buffer, 0, result.Count));
                              return;
                         }
                         else if(result.MessageType == WebSocketMessageType.Close)
                         {

                             string id = _connectionManager.GetAllSockets().FirstOrDefault(s => s.Value == webSocket).Key;

                             Console.WriteLine("Receive close message");
                             _connectionManager.GetAllSockets().TryRemove(id, out WebSocket socket);

                             await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                             return;
                         }
                       });

                  }else{
                      await _next(context);
                  }
        }

        private async Task SendConnIDAsync(WebSocket socket, string connId){
            var buffer = Encoding.UTF8.GetBytes("ConnID: " + connId);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage){
            var buffer = new byte[1024 * 4];

            while(socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer), CancellationToken.None);
                handleMessage(result, buffer);

            }
        }

        public async Task RouteJSONMessageAsync(string message)
        {
            var routeObj = JsonConvert.DeserializeObject<ResponseModel>(message);
            if(Guid.TryParse(routeObj.To, out Guid  guidOutput))
            {
                 Console.WriteLine("Targeted");
                 var socket = _connectionManager.GetAllSockets().FirstOrDefault(s => s.Key == routeObj.To);

                 if(socket.Value is null)
                 {
                    Console.WriteLine("Invalid Recipient");
                 }

                  if(socket.Value.State == WebSocketState.Open)
                   {
                       await socket.Value.SendAsync(Encoding.UTF8.GetBytes(routeObj.Message), 
                       WebSocketMessageType.Text, true, CancellationToken.None);
                   }
            }
            else
            {
               Console.WriteLine("Broadcast ");
               foreach (var socket in _connectionManager.GetAllSockets())
               {
                   if(socket.Value.State == WebSocketState.Open)
                   {
                       await socket.Value.SendAsync(Encoding.UTF8.GetBytes(routeObj.Message),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                   }
               }
            }
        }
    }

    public class ResponseModel {
      public string To {get;set;}
      public string From {get;set;}
      public string Message {get;set;}
    }
}