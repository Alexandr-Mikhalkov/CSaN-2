using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Client
{
    static void Main()
    {
        Console.Write("Введите IP-адрес сервера: ");
        string ipString = Console.ReadLine();
        Console.Write("Введите порт сервера: ");
        int port = int.Parse(Console.ReadLine());

        Console.Write("Введите ваше имя: ");
        string clientName = Console.ReadLine();

        if (!IPAddress.TryParse(ipString, out IPAddress ipAddress))
        {
            Console.WriteLine("Ошибка: неверный IP-адрес.");
            return;
        }

        try
        {
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            clientSocket.Connect(endPoint);
            Console.WriteLine($"Подключено к {ipAddress}:{port}");

            byte[] nameBytes = Encoding.UTF8.GetBytes(clientName);
            clientSocket.Send(nameBytes);

            byte[] idBuffer = new byte[1024];
            int idBytes = clientSocket.Receive(idBuffer);
            string clientId = Encoding.UTF8.GetString(idBuffer, 0, idBytes);
            Console.WriteLine($"Ваш ID: {clientId}");

            ShowCommands();

            Thread receiveThread = new Thread(() => ReceiveData(clientSocket, clientName));
            receiveThread.Start();

            while (true)
            {
                string message = Console.ReadLine();

                if (message == "/id")
                {
                    Console.WriteLine($"Ваш ID: {clientId}");
                }
                else if (message.StartsWith("/file "))
                {
                    string[] parts = message.Split(' ');

                    if (parts.Length >= 3 && parts[0] == "/file")
                    {
                        string targetID = parts[1];
                        string filePath = string.Join(" ", parts.Skip(2));

                        if (!File.Exists(filePath))
                        {
                            Console.WriteLine("Ошибка: Файл не найден!");
                            continue;
                        }

                        SendFile(clientSocket, targetID, filePath);
                    }
                    else
                    {
                        Console.WriteLine("Ошибка: Неправильный формат команды! Используйте: /file <ID> <ПУТЬ_К_ФАЙЛУ>");
                    }
                }
                else
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    clientSocket.Send(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static void ReceiveData(Socket clientSocket, string clientName)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRead = clientSocket.Receive(buffer);
                if (bytesRead == 0) break;

                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (receivedData.StartsWith("FILE:"))
                {
                    string[] headerParts = receivedData.Substring(5).Split(':');
                    if (headerParts.Length == 2 && int.TryParse(headerParts[1], out int fileSize))
                    {
                        string fileName = headerParts[0];

                        string userFolder = Path.Combine("received_files", clientName);
                        Directory.CreateDirectory(userFolder);

                        string filePath = Path.Combine(userFolder, fileName);

                        Console.WriteLine($"Получение файла {fileName} ({fileSize} байт)...");

                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            int remainingBytes = fileSize;
                            while (remainingBytes > 0)
                            {
                                int readSize = Math.Min(remainingBytes, buffer.Length);
                                int bytesReceived = clientSocket.Receive(buffer, 0, readSize, SocketFlags.None);
                                fileStream.Write(buffer, 0, bytesReceived);
                                remainingBytes -= bytesReceived;
                            }
                        }

                        Console.WriteLine($"Файл {fileName} сохранен в {filePath}");
                    }
                }
                else
                {
                    Console.WriteLine($"\n{receivedData}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении данных: {ex.Message}");
        }
    }

    static void SendFile(Socket clientSocket, string targetID, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Файл не найден!");
                return;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);

            string header = $"FILE:{targetID}:{fileName}:{fileData.Length}";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header + "\n");

            clientSocket.Send(headerBytes);
            clientSocket.Send(fileData);

            Console.WriteLine($"Файл {fileName} отправлен пользователю с ID {targetID}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке файла: {ex.Message}");
        }
    }

    static void ShowCommands()
    {
        Console.WriteLine("\nДоступные команды:");
        Console.WriteLine("   /id     - Показать ваш ID");
        Console.WriteLine("   /users  - Показать список всех подключенных пользователей");
        Console.WriteLine("   /file id <путь> - Отправить файл пользователю");
        Console.WriteLine("   Введите сообщение, чтобы отправить его в чат.");
    }
}