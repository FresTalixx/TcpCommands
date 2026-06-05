using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public class ChatEventHandlerServer
{
    public static async Task<bool> HandleLoginAsync(NetworkStream stream, string userFilePath)
    {
        var user = await NetworkHelper.ReceiveObjectAsync<User>(stream);
        if (user == null)
        {
            Console.WriteLine("Failed to receive user information.");
            return false;
        }
        var loginResult = await User.AuthenticateUserAsync(user, userFilePath);
        await NetworkHelper.SendObjectAsync(loginResult, stream);
        return loginResult.IsAuthenticated;
    }

    public static async Task<bool> HandleRegisterAsync(NetworkStream stream, string userFilePath)
    {
        var newUser = await NetworkHelper.ReceiveObjectAsync<User>(stream);
        if (newUser == null) { return false; }
        var registerResult = await User.RegisterUserAsync(newUser, userFilePath);
        await NetworkHelper.SendObjectAsync(registerResult, stream);
        return registerResult.IsRegistered;
    }

    public static async Task<List<string>> HandleGetUsersAsync(NetworkStream stream, string currentUserLogin, string userFilePath)
    {
        Console.WriteLine("Client requested user list.");
        //sending all users to client
        var userList = JsonSerializer.Deserialize<List<User>>(await File.ReadAllTextAsync(userFilePath));
        if (userList == null) { return new List<string>(); }
        var userListLogins = userList.Select(u => u.Login).Where(u => !string.IsNullOrEmpty(u) && u != currentUserLogin).ToList();
        await NetworkHelper.SendObjectAsync(userListLogins, stream);
        return userListLogins;
    }

    public static async Task<List<Message>> HandleGetChatHistoryAsync(
        NetworkStream stream,
        string currentUserLogin,
        string targetLogin,
        string chatMessagesFilePath)
    {
        if (!File.Exists(chatMessagesFilePath))
        {
            await File.WriteAllTextAsync(
                chatMessagesFilePath,
                "[]");
        }
        var messages = JsonSerializer.Deserialize<List<Message>>(await File.ReadAllTextAsync(chatMessagesFilePath));
        if (messages == null) { return new List<Message>(); }
        var filteredMessages =
        messages.Where(m =>

            (m.Sender == currentUserLogin &&
             m.Recipient == targetLogin)

             ||

            (m.Sender == targetLogin &&
             m.Recipient == currentUserLogin)

        )
        .OrderBy(m => m.SendingDate)
        .ToList();
        return filteredMessages;
    }
    public static async Task HandleSendMessage(NetworkStream stream, Message? message, string chatMessagesFilePath)
    {
        if (!File.Exists(chatMessagesFilePath))
        {
            await File.WriteAllTextAsync(
                chatMessagesFilePath,
                "[]");
        }
        var messages = JsonSerializer.Deserialize<List<Message>>(await File.ReadAllTextAsync(chatMessagesFilePath)) ?? new List<Message>();
        if (messages == null || message == null) { return; }
        messages.Add(message);
        await File.WriteAllTextAsync(chatMessagesFilePath, JsonSerializer.Serialize(messages));
    }

    public static async Task HandleGetNewMessagesAsync(
        NetworkStream stream,
        string login,
        string chatMessagesFilePath
        )
    {
        if (!File.Exists(chatMessagesFilePath))
        {
            Console.WriteLine("Chat messages file not found. Creating a new one.");
            await File.WriteAllTextAsync(
                chatMessagesFilePath,
                "[]");
        }

        var json =
            await File.ReadAllTextAsync(chatMessagesFilePath);

        var messages =
            JsonSerializer.Deserialize<List<Message>>(json)
            ?? new List<Message>();

        var newMessages =
            messages
            .Where(m =>
                m.Recipient == login &&
                !m.IsDelivered)
            .ToList();

        foreach (var msg in newMessages)
        {
            msg.IsDelivered = true;
        }

        await File.WriteAllTextAsync(
            chatMessagesFilePath,
            JsonSerializer.Serialize(
                messages,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        await NetworkHelper.SendObjectAsync(
            newMessages,
            stream);
    }
}




public class ChatEventHandlerClient
{
   public static async Task<List<string>?> GetUsersAsync(string address, int port, string login)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(address), port);

        using var stream = client.GetStream();

        await NetworkHelper.SendStringAsync("get_users", stream);
        await NetworkHelper.SendStringAsync(login, stream);

        return await NetworkHelper.ReceiveObjectAsync<List<string>>(stream);
    }

    public static async Task<List<Message>?> GetChatHistoryAsync(
    string address,
    int port,
    string login,
    string targetUser)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(address), port);

        using var stream = client.GetStream();

        await NetworkHelper.SendStringAsync("chat_history", stream);
        await NetworkHelper.SendStringAsync(login, stream);
        await NetworkHelper.SendStringAsync(targetUser, stream);

        return await NetworkHelper.ReceiveObjectAsync<List<Message>>(stream);
    }

    public static async Task SendMessageAsync(
    string address,
    int port,
    string login,
    string recipient,
    string text)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(address), port);

        using var stream = client.GetStream();

        await NetworkHelper.SendStringAsync("send_message", stream);
        await NetworkHelper.SendStringAsync(login, stream);

        var message = new Message
        {
            Sender = login,
            Recipient = recipient,
            Text = text,
            SendingDate = DateTime.Now
        };

        await NetworkHelper.SendObjectAsync(message, stream);
    }

    public static async Task PollMessagesAsync(
    string address,
    int port,
    string login,
    CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                using var client = new TcpClient();

                await client.ConnectAsync(IPAddress.Parse(address), port);

                using var stream = client.GetStream();

                // command
                await NetworkHelper.SendStringAsync("get_new_messages", stream);

                // identify user
                await NetworkHelper.SendStringAsync(login, stream);

                // receive messages
                var messages =
                    await NetworkHelper.ReceiveObjectAsync<List<Message>>(stream);

                if (messages != null)
                {
                    foreach (var msg in messages)
                    {
                        Console.WriteLine();
                        Console.WriteLine("New message received:");
                        Console.WriteLine($"[{msg.Sender}] {msg.Text}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Polling error: {ex.Message}");
            }
            
        }
    }

}
