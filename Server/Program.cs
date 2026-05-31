/*
 * Клієнт та сервер
 * 
 * Клієнт запитує у користувача команду (add, subtract, square)
 * запитує 2 числа або 1 число (залежно від команди)
 * відправляє команду та числа на сервер
 * сервер розуміє скільки чисел прийшло та яку команду виконати
 * повертає результат клієнту
 * клієнт показує результат користувачу
 * 
 * 
 * Додати команду auth
 * запитує логін та пароль
 * формуємо об'єкт User { string Login, string Password }
 * відправляємо команду та об'єкт на сервер
 * сервер відповідає об'єктом AuthResult { bool IsAuthenticated, string Message }
 * правильний пароль 1234, усі інші - неправильні
 * клієнт показує повідомлення про результат аутентифікації
 * 
 * 
 * Додати команду file
 * Запитує шлях до файлу на клієнті
 * Відправляє команду та файл на сервер
 * Сервер приймає файл та зберігає його у папці "ReceivedFiles"
 * відправляє повідомлення про успішне отримання файлу у вигляді об'єкта 
 * FileReceiveResult { bool IsSuccess, long FileSize, string FullPath }
 * Клієнт показує повідомлення про результат отримання файлу та його розмір
 * 
 */

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Server");

int port = 5000;
TcpListener server = new TcpListener(IPAddress.Any, port);


server.Start();

while (true)
{
    TcpClient client = await server.AcceptTcpClientAsync();
    Console.WriteLine("Client connected");
    _ = HandleClient(client);
}


async Task HandleClient(TcpClient client)
{
    NetworkStream stream = client.GetStream();
    // Отримання даних від клієнта
    var command = await NetworkHelper.ReceiveStringAsync(stream);
    Console.WriteLine("Received: " + command);
    // Відправка відповіді клієнту

    var commandParts = command.Split(',');
    await NetworkHelper.SendIntAsync(commandParts.Length, stream);
    await NetworkHelper.SendStringAsync(command, stream);
    foreach ( var commandPart in commandParts )
    {
        var message = commandPart.Trim().Split(' ');
        var messageCommand = message[0].Trim().ToLower();

        Console.WriteLine("Processing command: " + messageCommand);

        if (messageCommand == "add")
        {
            if (message.Length < 3)
            {
                var errorResponse = "Not enough arguments for add command";
                await NetworkHelper.SendStringAsync(errorResponse, stream);
                continue;
            }
            else if (message.Length > 3)
            {
                var errorResponse = "Too many arguments for add command";
                await NetworkHelper.SendStringAsync(errorResponse, stream);
                continue;
            }
            long.TryParse(message[1], out var firstNum);
            long.TryParse(message[2], out var secondNum);

            var sum = firstNum + secondNum;
            var messageToSend = $"Sum of {firstNum} and {secondNum} is {sum}";
            await NetworkHelper.SendStringAsync(messageToSend, stream);
        }
        else if (messageCommand == "subtract")
        {
            if (message.Length < 3)
            {
                var errorResponse = "Not enough arguments for subtract command";
                await NetworkHelper.SendStringAsync(errorResponse, stream);
                continue;
            }
            else if (message.Length > 3)
            {
                var errorResponse = "Too many arguments for subtract command";
                await NetworkHelper.SendStringAsync(errorResponse, stream);
                continue;
            }

            long.TryParse(message[1], out var firstNum);
            long.TryParse(message[2], out var secondNum);

            var diff = firstNum - secondNum;
            var messageToSend = $"Difference of {firstNum} and {secondNum} is {diff}";
            await NetworkHelper.SendStringAsync(messageToSend, stream);
        }
        else if (messageCommand == "square")
        {
            if (message.Length < 2)
            {
                var errorResponse = "Not enough arguments for square command";
                await NetworkHelper.SendStringAsync(errorResponse, stream);
                continue;
            }
            else if (message.Length > 2)
            {
                var errorResponse = "Too many arguments for square command";
                await NetworkHelper.SendStringAsync(errorResponse, stream);
                continue;
            }

            long.TryParse(message[1], out var num);
            var square = num * num;
            var messageToSend = $"Square of {num} is {square}";
            await NetworkHelper.SendStringAsync(messageToSend, stream);
        }
        else if (messageCommand == "auth")
        {
            var user = await NetworkHelper.ReceiveObjectAsync<User>(stream);

            if (user != null && user.Password == "1234")
            {
                var authResult = new AuthResult { IsAuthenticated = true, Message = "Authentication successful" };
                await NetworkHelper.SendObjectAsync(authResult, stream);
            }
            else
            {
                var authResult = new AuthResult { IsAuthenticated = false, Message = "Authentication failed" };
                await NetworkHelper.SendObjectAsync(authResult, stream);
            }

        }
        else if (messageCommand == "file")
        {
            var saveDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ReceivedFiles");
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }

            var result = await NetworkHelper.ReceiveFileAsync(saveDirectory, stream);

            var fileSize = result.Item2;
            var fullPath = result.Item1;
            var fileReceiveResult = new FileReceiveResult
            {
                IsSuccess = true,
                FileSize = fileSize,
                FullPath = fullPath
            };
            await NetworkHelper.SendObjectAsync(fileReceiveResult, stream);
        }
        else
        {
            var errorResponse = "Unknown command";
            await NetworkHelper.SendStringAsync(errorResponse, stream);
        }
    }
    

    client.Close();
    Console.WriteLine("Client disconnected");
}

