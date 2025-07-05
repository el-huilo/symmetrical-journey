using System;
using System.Net;
using System.Net.Sockets;

ServerObject server = new ServerObject();// создаем сервер
await server.ListenAsync(); // запускаем сервер

class ServerObject
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // сервер для прослушивания
    List<ClientObject> clients = new List<ClientObject>(); // все подключения
    protected internal void RemoveConnection(string id)
    {
        // получаем по id закрытое подключение
        ClientObject? client = clients.FirstOrDefault(c => c.Id == id);
        // и удаляем его из списка подключений
        if (client != null) clients.Remove(client);
        client?.Close();
    }
    // прослушивание входящих подключений
    protected internal async Task ListenAsync()
    {
        try
        {
            tcpListener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (true)
            {
                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                ClientObject clientObject = new ClientObject(tcpClient, this);
                clients.Add(clientObject);
                Task.Run(clientObject.ProcessAsync);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            Disconnect();
        }
    }

    // трансляция сообщения подключенным клиентам
    protected internal async Task RerouteMessageAsync(string message, int index)
    {

        await clients[index].Writer.WriteLineAsync(message); //передача данных
        await clients[index].Writer.FlushAsync();
    }
    // отключение всех клиентов
    protected internal void Disconnect()
    {
        foreach (var client in clients)
        {
            client.Close(); //отключение клиента
        }
        tcpListener.Stop(); //остановка сервера
    }
    protected internal bool Search_User(string tag, string username, ClientObject source)
    {
        for(int i = 0; i < clients.Count; i++)
        {
            if (clients[i].Tag == tag)
                if (clients[i].Username == username)
                {
                    source.Target_Index = i;
                    return true;
                }
        }
        return false;
    }
}
class ClientObject
{
    protected internal string Id { get; } = Guid.NewGuid().ToString();
    protected internal StreamWriter Writer { get; }
    protected internal StreamReader Reader { get; }
    TcpClient client;
    ServerObject server;
    protected internal string Tag { get; set; }
    protected internal string Username { get; set; }
    protected internal int Target_Index { get; set; }
    public ClientObject(TcpClient tcpClient, ServerObject serverObject)
    {
        client = tcpClient;
        server = serverObject;
        // получаем NetworkStream для взаимодействия с сервером
        var stream = client.GetStream();
        Reader = new StreamReader(stream);
        Writer = new StreamWriter(stream);
    }

    public async Task ProcessAsync()
    {
        try
        {
            // получаем имя пользователя
            Username = await Reader.ReadLineAsync();
            string? message = $"{Username} вошел в чат";
            Tag = await Reader.ReadLineAsync();
            // посылаем сообщение о входе в чат всем подключенным пользователям
            Console.WriteLine(message);
            // в бесконечном цикле получаем сообщения от клиента
            ProtocolHandler proto = new ProtocolHandler(server, this, Writer);
            while (true)
            {
                try
                {
                    message = await Reader.ReadLineAsync();
                    if (message == null) continue;
                    Console.WriteLine(message);
                    proto.Disassemble(ref message);
                    await server.RerouteMessageAsync(message, Target_Index);
                    Console.WriteLine(message);
                }
                catch
                {
                    message = "002" + Tag + Username;
                    Console.WriteLine(message);
                    //await server.RerouteMessageAsync(message, Target_Index);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            // в случае выхода из цикла закрываем ресурсы
            server.RemoveConnection(Id);
        }
    }
    // закрытие подключения
    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        client.Close();
    }
}
class ProtocolHandler
{
    string token;
    ServerObject server;
    ClientObject client;
    StreamWriter writer;
    public ProtocolHandler(ServerObject RelaySource, ClientObject ClientSource, StreamWriter WriterSource)
    {
        server = RelaySource;
        client = ClientSource;
        writer = WriterSource;
    }
    public void Disassemble(ref string message)
    {
        string Message_Id = message[0..3];
        switch (message[0..3])
        {
            case "202": message = Lock_Reroute();
                Console.WriteLine("other client locked"); break;
            case "200": message = Send_Message_To_Client(message);
                Console.WriteLine("message sent"); break;
            case "003":
                message = Request_search(message);
                Console.WriteLine("Search requested"); break;
            case "007": Console.WriteLine("Request Get"); message = Accepted_User_Connection(message[3..7], message[7..]); break;
            case "REJECTED": break;
            case "102": Request_Users_Status("1"); break;
            default: Console.WriteLine("Handler ignored message"); break;
        }
    }
    private string Request_search(string message)
    {
        server.Search_User(message[3..7], message[7..], client);
        return Reroute_Search_Request(client.Tag, client.Username);
    }
    private string Reroute_Search_Request(string tag, string username)
    {
        // Itself data
        return "006" + tag + username;
    }
    public string Accepted_User_Connection(string tag, string username)
    {
        server.Search_User(tag, username, client);
        // Send Accept itself
        Tkn(username, tag);
        writer.WriteLineAsync(Send_Session_Token());
        writer.FlushAsync();
        string message = "004" + client.Tag + client.Username;
        writer.WriteLineAsync(message);
        writer.FlushAsync();
        server.RerouteMessageAsync(Send_Session_Token(), client.Target_Index);
        // Give handler send it to other client
        message = "004" + tag + username;
        return message;
    }
    private string Lock_Reroute()
    {
        return "202";
    }
    public string Send_Message_To_Client(string message)
    {
        return "201" + message[3..];
    }
    public void Request_Users_Status(string UsersState)
    {
        writer.WriteLineAsync("103" + UsersState);
        writer.FlushAsync();
    }
    public string Send_Session_Token()
    {
        return "150" + token;
    }
    private void Tkn(string username, string tag)
    {
        Session_Token sessionToken = new Session_Token();
        token = sessionToken.Generate_Token(client.Username, username, client.Tag, tag);
    }
}
class Session_Token
{
    public string Generate_Token(string user1, string user2, string tag1, string tag2)
    {
        byte[] block1 = System.Text.Encoding.UTF8.GetBytes(user1);
        byte[] block2 = System.Text.Encoding.UTF8.GetBytes(user2);
        byte[] block3 = System.Text.Encoding.UTF8.GetBytes(tag1);
        byte[] block4 = System.Text.Encoding.UTF8.GetBytes(tag2);
        int token = 115123;
        for (int i = 0; i < 4; i++)
        {
            token -= (block4[i] + block1[i]) + (block3[i] / 3);
            token ^= 2372  * (block3[i] + block2[i]);
        }
        return Convert.ToString(token);
    }
}