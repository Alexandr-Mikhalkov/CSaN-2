using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    private static List<Socket> clients = new List<Socket>();
    private static Dictionary<Socket, (string Name, string ID)> clientInfo = new Dictionary<Socket, (string, string)>();
    private static object lockObject = new object();
    private const int MaxClients = 5;

    static void Main()
    {
        Console.WriteLine("Проверка занятых портов...");
        ShowUsedPorts();

        Console.Write("Введите IP-адрес сервера: ");
        string ipString = Console.ReadLine();
        Console.Write("Введите порт сервера: ");
        int port = int.Parse(Console.ReadLine());

        if (!IPAddress.TryParse(ipString, out IPAddress ipAddress))
        {
            Console.WriteLine("Ошибка: неверный IP-адрес.");
            return;
        }

        try
        {
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            serverSocket.Bind(endPoint);
            serverSocket.Listen(5);
            Console.WriteLine($"Сервер запущен на {ipAddress}:{port}");

            while (true)
            {
                if (clients.Count >= MaxClients)
                {
                    Console.WriteLine("Достигнут лимит в 5 пользователей! Ожидание свободного места...");
                    Thread.Sleep(1000);
                    continue;
                }

                Socket clientSocket = serverSocket.Accept();
                lock (lockObject)
                {
                    clients.Add(clientSocket);
                }
                Thread clientThread = new Thread(() => HandleClient(clientSocket));
                clientThread.Start();
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Ошибка сокета: {ex.Message}");
        }
    }

    static void HandleClient(Socket client)
    {
        try
        {
            byte[] buffer = new byte[1024];

            int nameBytes = client.Receive(buffer);
            string clientName = Encoding.UTF8.GetString(buffer, 0, nameBytes).Trim();

            string clientID = Guid.NewGuid().ToString();
            lock (lockObject)
            {
                clientInfo[client] = (clientName, clientID);
            }

            byte[] idBytes = Encoding.UTF8.GetBytes(clientID);
            client.Send(idBytes);

            Console.WriteLine($"{clientName} (ID: {clientID}) подключился.");

            while (true)
            {
                int bytesRead = client.Receive(buffer);
                if (bytesRead == 0) break;

                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (receivedData == "/users")
                {
                    string userList = "Подключенные пользователи:\n" + string.Join("\n", GetUserList());
                    byte[] userListBytes = Encoding.UTF8.GetBytes(userList);
                    client.Send(userListBytes);
                    continue;
                }

                if (receivedData.StartsWith("FILE:"))
                {
                    string[] headerParts = receivedData.Substring(5).Split(':');
                    if (headerParts.Length == 3 && int.TryParse(headerParts[2], out int fileSize))
                    {
                        string targetClientID = headerParts[0];
                        string fileName = headerParts[1];

                        Socket targetClientSocket = null;
                        lock (lockObject)
                        {
                            foreach (var clientEntry in clientInfo)
                            {
                                if (clientEntry.Value.ID == targetClientID)
                                {
                                    targetClientSocket = clientEntry.Key;
                                    break;
                                }
                            }
                        }

                        if (targetClientSocket != null)
                        {
                            Console.WriteLine($"Пересылаем файл {fileName} пользователю {targetClientID}...");

                            string fileHeader = $"FILE:{fileName}:{fileSize}\n";
                            targetClientSocket.Send(Encoding.UTF8.GetBytes(fileHeader));

                            byte[] fileBuffer = new byte[fileSize];
                            int totalBytesReceived = 0;

                            while (totalBytesReceived < fileSize)
                            {
                                int bytesReceived = client.Receive(fileBuffer, totalBytesReceived, fileSize - totalBytesReceived, SocketFlags.None);
                                totalBytesReceived += bytesReceived;
                            }

                            targetClientSocket.Send(fileBuffer);
                            Console.WriteLine($"Файл {fileName} успешно передан пользователю {targetClientID}.");
                        }
                        else
                        {
                            Console.WriteLine($"Ошибка: получатель с ID {targetClientID} не найден.");
                        }
                    }
                    continue;
                }

                Console.WriteLine($"{clientName}: {receivedData}");

                lock (lockObject)
                {
                    foreach (var clientSocket in clients)
                    {
                        if (clientSocket != client)
                        {
                            clientSocket.Send(Encoding.UTF8.GetBytes($"{clientName}: {receivedData}"));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            lock (lockObject)
            {
                clients.Remove(client);
                clientInfo.Remove(client);
            }
            client.Close();
        }
    }

    static void ShowUsedPorts()
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        IPEndPoint[] endPoints = properties.GetActiveTcpListeners();

        Console.WriteLine("Занятые порты:");
        foreach (var endPoint in endPoints)
        {
            Console.WriteLine($"{endPoint.Address}:{endPoint.Port}");
        }
        Console.WriteLine();
    }

    static List<string> GetUserList()
    {
        List<string> users = new List<string>();
        lock (lockObject)
        {
            foreach (var entry in clientInfo.Values)
            {
                users.Add($"{entry.Name} (ID: {entry.ID})");
            }
        }
        return users;
    }
}