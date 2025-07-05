using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Media;
using MVVMAvalonia.ViewModels;
using System.Text.RegularExpressions;

namespace MVVMAvalonia.Views;

public partial class MainView : UserControl
{
    // Viewmodel reference
    MainViewModel vm;
    int contact_count;
    bool[] LoadedContacts;
    int index;
    string StrRegex = @"([A-Fa-f0-9]{8})-";
    string PermutRegex = "([0-9-])";
    Regex validString;
    public MainView()
    {
        InitializeComponent();
        StartUp();
        validString = new Regex(StrRegex);
    }
    // Choice of algorithm mode
    private void ComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if(ComboDropDown.SelectedIndex == 1 ) { KeyStack.IsVisible = false; }
        else { KeyStack.IsVisible = true; }
    }
    // Parody on loading screen to make transition between views smoother
    private async void StartUp()
    {
        await Task.Delay(1500);
        LoadedContacts = new bool[contact_count];
        mainWin.Effect = BlurEffect.Parse("blur(0)");
        mainWin.IsHitTestVisible = true;
    }
    // Add to chat self submitted message
    public void ChatBox_add(bool Own_message, string messag)
    {
        vm.Messages_list.Add(new Message_unit(messag, Avalonia.Layout.HorizontalAlignment.Right));
    }
    // Get self submitted message, ignor empty input
    public void SubmitMessage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MessageTextBox.Text != "")
        {
            vm.ProtoUnit.Lock_Other_Client();
            ChatBox_add(true, MessageTextBox.Text);
            vm.ProtoUnit.Send_Message_To_Relay(MessageTextBox.Text);
            MessageTextBox.Text = "";
        }
    }
    // Update contact list onLoad event and update every 30 sec, also get viewmodel reference
    private async void UserControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        vm = (this.DataContext as MainViewModel);
        vm.MainView = this;
        vm.Get_Access_To_Proto();
        vm.CallInit_list.Execute(null);
        contact_count = vm.Contact_count;
        while (true)
        {
            vm.Relay_Status_Users_Request();
            await Task.Delay(30000);
        }
    }
    // current "window" manager
    private void ListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        index = contact_listbox.SelectedIndex;
        if (index == 0)
        {
            Blank_Background.IsVisible = false;
            Chat.IsVisible = false;
            Settings.IsVisible = false;
            Add_contact.IsVisible = true;
        }
        else if (index >= 0)
        {
            if (LoadedContacts[index] == true)
            {
                Blank_Background.IsVisible = false;
                Add_contact.IsVisible = false;
                Settings.IsVisible = false;
                Chat.IsVisible = true;
            }
            else
            {
                Blank_Background.IsVisible = false;
                Chat.IsVisible = false;
                Add_contact.IsVisible = false;
                Settings.IsVisible = true;
            }
        }

    }
    // Set up Algorithm variables before dialog start
    private void End_Setting_up(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Input_validation(keybox1.Text))
            if (Input_validation(keybox2.Text))
                if (Input_validation(keybox3.Text))
                    if (Input_validation(keybox4.Text))
                    {
                        LoadedContacts[index] = true;
                        Settings.IsVisible = false;
                        Chat.IsVisible = true;
                        
                    }
    }
    // Name says a lot
    private bool Input_validation(string check)
    {
        SSSSS.Content = check;
        if(validString.IsMatch(check))
            return true;
        else return false;
    }
    public void Update_LoadCont_Array()
    {
        contact_count++;
        bool[] temp = LoadedContacts;
        LoadedContacts = new bool[contact_count];
        for (int i = 0; i < temp.Length; i++)
        {
            LoadedContacts[i] = temp[i];
        }
    }
    // Adding new user to list and requesting Relay to find it
    private void Add_User_N(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        vm.ProtoUnit.Request_User_Connection(tagUser.Text, txtUser.Text);
        Add_contact.IsVisible = false; Blank_Background.IsVisible = true;
        contact_listbox.SelectedIndex = -1;
    }
}