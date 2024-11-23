using Microsoft.AspNetCore.SignalR;
using ChatterBox.Models;
using System;
using System.Threading.Tasks;

namespace ChatterBox.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User.Identity.Name;

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            // For now we'll just send the message without saving to database
            await Clients.User(receiverId).SendAsync("ReceiveMessage", message);
            await Clients.Caller.SendAsync("MessageSent", message);
        }
    }
}