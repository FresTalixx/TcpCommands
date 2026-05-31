using System.Net.Sockets;
using System.Text;

Console.WriteLine("Client");


while (true)
{
    Console.WriteLine("Input command: ");
    string command = Console.ReadLine()!;

    if (command.Trim().ToLower() == "exit")
        break;

    await CommunicateWithServerAsync(command);
}



async Task CommunicateWithServerAsync(string command)
{
    int port = 5000;
    TcpClient client = new TcpClient();
    await client.ConnectAsync("192.168.1.2", port);

    NetworkStream stream = client.GetStream();

    // Send command to server

    await NetworkHelper.SendStringAsync(command, stream);

    if (command.Trim().ToLower() == "auth")
    {
        Console.WriteLine("Input login: ");
        var login = Console.ReadLine()!;
        Console.WriteLine("Input password: ");
        var password = Console.ReadLine()!;

        var user = new User { Login = login, Password = password };
        await NetworkHelper.SendObjectAsync(user, stream);
    }

    else if (command.Trim().ToLower() == "file")
    {
        Console.WriteLine("Input file path: ");
        var filePath = Console.ReadLine()!;
        await NetworkHelper.SendFileAsync(filePath, stream);
    }

    var count = await NetworkHelper.ReceiveIntAsync(stream);
    var currentCommand = await NetworkHelper.ReceiveStringAsync(stream);

    

    // Receive response from server
    for (int i = 0; i < count; i++)
    {
        if (currentCommand.Trim().ToLower() == "auth")
        {
            var authResult = await NetworkHelper.ReceiveObjectAsync<AuthResult>(stream);
            Console.WriteLine("Authentication success: " + authResult?.IsAuthenticated);
            Console.WriteLine("Authentication result: " + authResult?.Message);
            break;
        }
        else if (currentCommand.Trim().ToLower() == "file")
        {
            var fileReceiveResult = await NetworkHelper.ReceiveObjectAsync<FileReceiveResult>(stream);
            Console.WriteLine("File receive success: " + fileReceiveResult?.IsSuccess);
            Console.WriteLine("File size: " + fileReceiveResult?.FileSize);
            Console.WriteLine("File path: " + fileReceiveResult?.FullPath);
            break;
        }

        var message = await NetworkHelper.ReceiveStringAsync(stream);
        Console.WriteLine("Received: " + message);
    }
    
    client.Close();
}




