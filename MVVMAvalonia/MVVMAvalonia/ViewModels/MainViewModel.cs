using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVMAvalonia.Views;
using System.Collections;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Net.Sockets;

namespace MVVMAvalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        Messages_list = new ObservableCollection<Message_unit>();
        CallInit_list = new RelayCommand(Contact_Init);
        Contacts = new ObservableCollection<Contact>();
    }
    [ObservableProperty]
    private ObservableCollection<Contact> _contacts;
    [ObservableProperty]
    private ObservableCollection<Message_unit> _messages_list;
    [ObservableProperty]
    private int _contact_count;
    [ObservableProperty]
    private string _tag;
    [ObservableProperty]
    private string _username;
    private NetworkObject client;
    [ObservableProperty]
    private ProtocolHandler _protoUnit;
    [ObservableProperty]
    private MainView _mainView;
    public ICommand CallInit_list { get; set; }
    // Called on load
    private void Contact_Init()
    {
        Contacts.Add(new AddNew());
        Contact_count = Contacts.Count;
    }
    public void Contact_Add(Contact contact)
    {
        Contacts.Add(contact);
    }
    // method to set correct color of status
    public IBrush Get_Status_Brush(bool state)
    {
        if (state)
            return Brush.Parse("#00ff00");
        else return Brush.Parse("#ff0000");
    }
    public void Relay_Status_Users_Request()
    {
        // There might be code, but it requires filestream logic, which in
        // current kursach I will skip, cause this app already had enough features
    }
    // start on log in, start up client side
    public void Init_NetCode()
    {
        client = new NetworkObject(Username, Tag);
    }
    // start on mainview load, exchange references
    public void Get_Access_To_Proto()
    {
        ProtoUnit = client.External_Access();
        ProtoUnit.Get_VM_Reference(this);
    }
}
public class Message_unit : ObservableObject
{
    private string _message;
    public string Message
    {
        get { return _message; }
        set { SetProperty(ref _message, value); }
    }
    private HorizontalAlignment _alignment;
    public HorizontalAlignment Alignment
    {
        get { return _alignment; }
        set { SetProperty(ref _alignment, value); }
    }
    public Message_unit(string message, HorizontalAlignment alignment)
    {
        Message = message;
        Alignment = alignment;
    }
}
public class Contact : ObservableObject
{
    private string _name;
    private string _tag;
    private IBrush _status;
    public string Name
    {
        get { return _name; }
        set { SetProperty(ref _name, value); }
    }
    public string Tag
    {
        get { return _tag; }
        set { SetProperty(ref _tag, value); }
    }
    public IBrush Status
    {
        get { return _status; }
        set { SetProperty(ref _status, value); }
    }
}
// Unit to make determined and unique button in list
public class AddNew : Contact
{
    public AddNew()
    {
        Name = "Add";
        Status = Brush.Parse("#7f00ff");
    }
}

class NetworkObject
{
    string host = "127.0.0.1";
    int port = 8888;
    string? message;
    TcpClient client;
    string? userName;
    string? Tag;
    StreamReader? Reader = null;
    StreamWriter? Writer = null;
    ProtocolHandler proto;
    public NetworkObject(string name, string tag)
    {
        userName = name;
        Tag = tag;
        StartUp();
    }
    // Initialize all needed
    async void StartUp()
    {
        client = new TcpClient();
        try
        {
            client.Connect(host, port);
            Reader = new StreamReader(client.GetStream());
            Writer = new StreamWriter(client.GetStream());
            if (Writer is null || Reader is null) return;
            proto = new ProtocolHandler(Writer);
            Task.Run(() => ReceiveMessageAsync(Reader, Writer, proto));
            await SendLoginAsync(Writer);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    // provide access to ProtocolHandler object
    public ProtocolHandler External_Access()
    {
        return proto;
    }
    // Send login data to server
    async Task SendLoginAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync(userName);
        await writer.FlushAsync();
        await writer.WriteLineAsync(Tag);
        await writer.FlushAsync();
    }
    // Repeatable receive
    async Task ReceiveMessageAsync(StreamReader reader, StreamWriter writer, ProtocolHandler proto)
    {
        
        while (true)
        {
            try
            {
                message = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(message)) continue;
                else proto.Disassemble(ref message);
                await writer.FlushAsync();
            }
            catch
            {
                break;
            }
        }
    }
}
public class ProtocolHandler
{
    bool lock_state = false;
    int token;
    StreamWriter writer;
    Alg Encryptor;
    MainViewModel vm;
    // in contact list
    int Target_Index;
    public ProtocolHandler(StreamWriter source)
    {
        writer = source;
    }
    public void Disassemble(ref string message)
    {
        switch (message[0..3])
        {
            case "202":
                lock_state = true; break;
            case "201":
                lock_state = false;
                Receive_Message(message[3..]); break;
            case "103": Console.WriteLine("Update users list"); break;
            case "002": Close_Session(message[3..7], message[7..]); break;
            case "006": Accept_User_Request(message[3..7], message[7..]); break;
            case "004": Add_Contact_On_Acceptance(message[3..7], message[7..]); break;
            case "150":
                Receive_Token(message); break;
            default: Console.WriteLine("Handler ignored message"); break;
        }
    }
    private void Close_Session(string tag, string username)
    {
        vm.Contacts[Target_Index].Status = vm.Get_Status_Brush(false);
    }
    public void Get_VM_Reference(MainViewModel viewmodel)
    {
        vm = viewmodel;
    }
    private void Add_Contact_On_Acceptance(string tag, string username)
    {
        vm.Contact_Add(new Contact { Tag = tag, Name = username, Status = vm.Get_Status_Brush(true)});
        Target_Index = vm.Contacts.Count - 1;
        vm.MainView.Update_LoadCont_Array();
    }
    private void Receive_Message(string message)
    {
        string messag = Encryptor.Decrypt(message[16..]);
        vm.Messages_list.Add(new Message_unit(messag, Avalonia.Layout.HorizontalAlignment.Left));
    }
    public void Request_User_Connection(string tag, string username)
    {
        string message = "003" + tag + username;
        writer.WriteLineAsync(message);
        writer.FlushAsync();
    }
    public void Lock_Other_Client()
    {
        writer.WriteLineAsync("202");
        writer.FlushAsync();
    }
    private void Accept_User_Request(string tag, string username)
    {
        bool accept = true;
        if (accept) writer.WriteLineAsync("007" + tag + username);
        else writer.WriteLineAsync("REJECTED");
        writer.FlushAsync();
    }
    public void Send_Message_To_Relay(string message)
    {
        if (lock_state != true)
            writer.WriteLineAsync("200" + Encryptor.Encrypt(message));
        writer.FlushAsync();
    }
    public void Request_Users_Status()
    {
        writer.WriteLineAsync("102");
        writer.FlushAsync();
    }
    private void Receive_Token(string message)
    {
        token = Convert.ToInt32(message[3..7]);
        Encryptor = new Alg(token);
    }
}
class minor
{
    byte[][] minors = new byte[10][];
    int[] padding = new int[9];
    public void initialize()
    {
        int j = 0;
        for (int i = 0; i < 10; i++)
        {
            minors[i] = new byte[12];
            for (int k = 0; k < 12; k++, j++) minors[i][k] = System.Convert.ToByte(j);
        }
        for (int i = 0; i < 8; i++) padding[i] = 0;
        padding[8] = 0;
    }
    public byte[][] getminor()
    {
        return minors;
    }
    public int[] getpadding()
    {
        return padding;
    }
}
class mergedkey
{
    // merge key, session token and parts of minors
    // may be available to configure
    byte[] merged = new byte[128];
    byte[] inline_minors = new byte[16];
    int minor_count;
    byte[] token = new byte[4];
    int current_cycle = 0;
    // On algorithm initialize state
    public void Set_session_token(int session_token) { token = BitConverter.GetBytes(session_token); }
    public void Set_current_minors_count(int minors) { minor_count = minors; }
    public void mergedata(byte[] key)
    {
        merged = key;
        for (int i = 0; i < 32; i++)
        {
            merged[i * 4] ^= inline_minors[(i + 3) % 16];
            merged[i * 4 + 1] ^= token[i % 4];
            merged[i * 4 + 2] ^= inline_minors[i % 16];
            merged[i * 4 + 3] ^= token[(i + 2) % 4];
        }
        inline_minors[0] ^= token[2];
        inline_minors[8] ^= token[0];
        inline_minors[5] ^= token[3];
        inline_minors[15] ^= token[2];
        inline_minors[12] ^= token[1];
        inline_minors[3] ^= token[0];
        inline_minors[11] ^= token[3];
        current_cycle++;
    }
    // take full one permutation block here, then it will be xor'ed in some way with key
    public void Convert_minors_inline(byte[][] minors)
    {
        BitArray bit = new BitArray(inline_minors);
        for (int i = 0; i < 10; i++)
            for (int k = 0; k < minors[i].Length; k++)
                if (minors[i][k] != 0)
                    bit[minors[i][k]] = true;
        bit.CopyTo(inline_minors, 0);
    }
    // update after each message
    public void update_merged()
    {
        for (int i = 0; i < 32; i++)
        {
            merged[i * 4] ^= inline_minors[(i + 3) % 16];
            merged[i * 4 + 1] ^= token[i % 4];
            merged[i * 4 + 2] ^= inline_minors[i % 16];
            merged[i * 4 + 3] ^= token[(i + 2) % 4];
        }
        inline_minors[(0 + current_cycle) % 16] ^= token[2];
        inline_minors[(8 + current_cycle) % 16] ^= token[0];
        inline_minors[(5 + current_cycle) % 16] ^= token[3];
        inline_minors[(15 + current_cycle) % 16] ^= token[2];
        inline_minors[(12 + current_cycle) % 16] ^= token[1];
        inline_minors[(3 + current_cycle) % 16] ^= token[0];
        inline_minors[(11 + current_cycle) % 16] ^= token[3];
        current_cycle++;
    }
    public byte[] Get_merged_key()
    {
        return merged;
    }
}
class keyStruct
{
    byte[] keys = new byte[128];
    public byte[] Get_key_bytes()
    {
        return keys;
    }
    public void Set_key(ulong key, int ind)
    {
        byte[] convert = BitConverter.GetBytes(key);
        for (int i = 0; i < 8; i++)
            keys[ind + i] = convert[i];
    }
}
class Alg
{
    mergedkey Merged_Key;
    // current version of algorithm doesn't provide multiple permutation blocks
    minor[] minors;
    keyStruct assembledkey;
    byte[] bytes;
    public Alg(int Session_Token)
    {
        Merged_Key = new mergedkey();
        minors = new minor[1];
        for (int i = 0; i < 1; i++) { minors[i] = new minor(); }
        minors[0].initialize();
        assembledkey = new keyStruct();
        bytes = new byte[128];
        // in right order
        StartUp();
        Merged_Key.Set_session_token(Session_Token);
        Merged_Key.Convert_minors_inline(minors[0].getminor());
        Merged_Key.mergedata(assembledkey.Get_key_bytes());
    }
    private void StartUp()
    {
        ulong[] key = new ulong[16];
        key[0] = 10000000000000000000;
        for (int i = 1; i < 16; i++) key[i] = 10700250000056020040;
        for (int i = 0; i < 16; i++)
            assembledkey.Set_key(key[i], i);
    }
    public string Encrypt(string message)
    {
        byte[] str = System.Text.Encoding.UTF8.GetBytes(message);
        for (int i = 0; i < str.Length; i++) { bytes[i] = str[i]; }
        for (int i = str.Length; i < 128; i++) { bytes[i] = 0; }
        xor(assembledkey.Get_key_bytes());
        permutate(bytes, minors[0].getminor(), minors[0].getpadding());
        xor(Merged_Key.Get_merged_key());
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
    public string Decrypt(string message)
    {
        byte[] str = System.Text.Encoding.UTF8.GetBytes(message);
        for (int i = 0; i < str.Length; i++) { bytes[i] = str[i]; }
        for (int i = str.Length; i < 128; i++) { bytes[i] = 0; }
        xor(Merged_Key.Get_merged_key());
        PermutateReversion(bytes, minors[0].getminor(), minors[0].getpadding());
        xor(assembledkey.Get_key_bytes());
        string dec = System.Text.Encoding.UTF8.GetString(bytes);
        string Refined = "";
        for (int i = 0; i < dec.Length; i++)
        {
            if (dec[i] != '\0') { Refined += dec[i]; }
            else break;
        }
        return Refined;
    }
    private void xor(byte[] key)
    {
        for (int i = 0; i < 128; i++)
            bytes[i] ^= key[i];
    }
    private void permutate(byte[] suk, byte[][] minors, int[] padding)
    {
        int firstblock = -2, secondblock = -1;
        // clock sequence
        for (int clock = 0; clock < 12; clock++)
        {
            if (clock / 4 == 0)
            {
                firstblock += 2;
                secondblock += 2;
            }
            else if (clock / 4 == 1)
            {
                firstblock = 0; secondblock = 2;
                if (clock == 5)
                {
                    firstblock = 1; secondblock = 4;
                }
                else if (clock == 6)
                {
                    firstblock = 3; secondblock = 6;
                }
                else if (clock == 7)
                {
                    firstblock = 5; secondblock = 7;
                }
            }
            else if (clock / 4 == 2)
            {
                firstblock = 0; secondblock = 7;
                if (clock == 9)
                {
                    firstblock = 1; secondblock = 6;
                }
                else if (clock == 10)
                {
                    firstblock = 2; secondblock = 3;
                }
                else if (clock == 11)
                {
                    firstblock = 4; secondblock = 5;
                }
            }
            // 8 segments ||\\//--     |\|/\-/-    |/\\--/|
            byte[] sub = new byte[16];
            byte[] sublink = new byte[16];
            sub = suk[(firstblock * 16)..(firstblock * 16 + 16)];
            sublink = suk[(secondblock * 16)..(secondblock * 16 + 16)];
            BitArray bits = new BitArray(sub);
            BitArray bits2 = new BitArray(sublink);
            bool temp;
            // bit exchange in segments
            int position = 0;
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < minors[i].Length; j++, position++)
                {
                    temp = bits[position];
                    bits[position] = bits2[minors[i][j]];
                    bits2[minors[i][j]] = temp;
                }
                if (i != 9)
                    position += padding[i];
            }
            bits.CopyTo(suk, firstblock * 16);
            bits2.CopyTo(suk, secondblock * 16);
        }
    }
    private void PermutateReversion(byte[] received, byte[][] minors, int[] padding)
    {
        int firstblock = 0, secondblock = 0;
        // clock sequence
        for (int clock = 11; clock >= 0; clock--)
        {
            if (clock == 3) { firstblock = 6; secondblock = 7; }
            if (clock / 3 == 0)
            {
                firstblock -= 2;
                secondblock -= 2;
            }
            else if (clock / 4 == 1)
            {
                firstblock = 0; secondblock = 2;
                if (clock == 5)
                {
                    firstblock = 1; secondblock = 4;
                }
                else if (clock == 6)
                {
                    firstblock = 3; secondblock = 6;
                }
                else if (clock == 7)
                {
                    firstblock = 5; secondblock = 7;
                }
            }
            else if (clock / 4 == 2)
            {
                firstblock = 0; secondblock = 7;
                if (clock == 9)
                {
                    firstblock = 1; secondblock = 6;
                }
                else if (clock == 10)
                {
                    firstblock = 2; secondblock = 3;
                }
                else if (clock == 11)
                {
                    firstblock = 4; secondblock = 5;
                }
            }
            // 8 segments ||\\//--     |\|/\-/-    |/\\--/| in reverse order
            byte[] sub = new byte[16];
            byte[] sublink = new byte[16];
            sub = received[(firstblock * 16)..(firstblock * 16 + 16)];
            sublink = received[(secondblock * 16)..(secondblock * 16 + 16)];
            BitArray bits = new BitArray(sub);
            BitArray bits2 = new BitArray(sublink);
            bool temp;
            // bit exchange in segments
            int position = 0;
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < minors[i].Length; j++, position++)
                {
                    temp = bits[position];
                    bits[position] = bits2[minors[i][j]];
                    bits2[minors[i][j]] = temp;
                }
                if (i != 9)
                    position += padding[i];
            }
            bits.CopyTo(received, firstblock * 16);
            bits2.CopyTo(received, secondblock * 16);
        }
    }
}